using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JellyfinDiscordAuth.Services
{
    /// <summary>
    /// Manages temporary authentication sessions for the Discord OAuth flow.
    /// Sessions expire automatically after a short duration.
    /// </summary>
    public class DiscordAuthSessionService : IHostedService
    {
        private readonly ILogger<DiscordAuthSessionService> _logger;
        private readonly ConcurrentDictionary<string, DiscordAuthSession> _sessions = new();
        private Timer? _cleanupTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscordAuthSessionService"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public DiscordAuthSessionService(ILogger<DiscordAuthSessionService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Creates a new temporary session and returns the exchange code.
        /// </summary>
        /// <param name="accessToken">The Jellyfin access token.</param>
        /// <param name="userId">The Jellyfin user id.</param>
        /// <param name="serverId">The Jellyfin server id.</param>
        /// <param name="username">The username.</param>
        /// <returns>The temporary exchange code.</returns>
        public string CreateSession(string accessToken, Guid userId, string serverId, string username)
        {
            var code = Guid.NewGuid().ToString("N");
            var session = new DiscordAuthSession
            {
                Code = code,
                AccessToken = accessToken,
                UserId = userId,
                ServerId = serverId,
                Username = username,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
            };

            _sessions.TryAdd(code, session);
            _logger.LogInformation("Created Discord auth session for user {Username} ({UserId}).", username, userId);
            return code;
        }

        /// <summary>
        /// Retrieves and removes a session by its exchange code.
        /// </summary>
        /// <param name="code">The exchange code.</param>
        /// <returns>The session, or null if not found or expired.</returns>
        public DiscordAuthSession? RetrieveAndRemoveSession(string code)
        {
            if (!_sessions.TryRemove(code, out var session))
            {
                return null;
            }

            if (session.ExpiresAt < DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Discord auth session {Code} has expired.", code);
                return null;
            }

            return session;
        }

        /// <summary>
        /// Cleans up expired sessions.
        /// </summary>
        private void CleanupExpiredSessions()
        {
            var now = DateTimeOffset.UtcNow;
            var expiredKeys = _sessions
                .Where(kvp => kvp.Value.ExpiresAt < now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _sessions.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired Discord auth sessions.", expiredKeys.Count);
            }
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cleanupTimer = new Timer(
                _ => CleanupExpiredSessions(),
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1));

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cleanupTimer?.Dispose();
            _sessions.Clear();
            return Task.CompletedTask;
        }
    }
}
