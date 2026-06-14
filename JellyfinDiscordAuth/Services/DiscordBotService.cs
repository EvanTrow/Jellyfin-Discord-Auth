using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JellyfinDiscordAuth.Services
{
    /// <summary>
    /// Hosted service that manages the Discord bot lifecycle.
    /// </summary>
    public class DiscordBotService : IHostedService, IAsyncDisposable, IDisposable
    {
        private readonly ILogger<DiscordBotService> _logger;
        private readonly DiscordUserSyncService _syncService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscordBotService"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="syncService">The user sync service.</param>
        public DiscordBotService(
            ILogger<DiscordBotService> logger,
            DiscordUserSyncService syncService)
        {
            _logger = logger;
            _syncService = syncService;

            if (DiscordAuthPlugin.Instance != null)
            {
                DiscordAuthPlugin.Instance.ConfigurationChanged += OnConfigurationChanged;
            }
        }

        /// <summary>
        /// Gets the Discord socket client.
        /// </summary>
        public DiscordSocketClient? Client { get; private set; }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await StartDiscordBotAsync().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await StopDiscordBotAsync().ConfigureAwait(false);
        }

        private async void OnConfigurationChanged(object? sender, BasePluginConfiguration e)
        {
            _logger.LogInformation("Configuration changed. Restarting Discord bot.");
            await StopDiscordBotAsync().ConfigureAwait(false);
            await StartDiscordBotAsync().ConfigureAwait(false);
        }

        private async Task StartDiscordBotAsync()
        {
            var config = DiscordAuthPlugin.Instance?.Configuration;
            if (config == null)
            {
                _logger.LogWarning("Plugin configuration is not available; skipping Discord bot start.");
                return;
            }

            if (string.IsNullOrWhiteSpace(config.BotToken))
            {
                _logger.LogWarning("Bot token is not configured; skipping Discord bot start.");
                return;
            }

            try
            {
                await StopDiscordBotAsync().ConfigureAwait(false);

                var socketConfig = new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.Guilds | GatewayIntents.GuildMembers
                };

                Client = new DiscordSocketClient(socketConfig);
                Client.Log += OnDiscordLog;

                await Client.LoginAsync(TokenType.Bot, config.BotToken).ConfigureAwait(false);
                await Client.StartAsync().ConfigureAwait(false);

                Client.Ready += OnReady;
                Client.LoggedOut += OnLoggedOut;
                Client.Disconnected += OnDisconnected;
                Client.JoinedGuild += OnJoinedGuild;
                Client.UserJoined += OnUserJoined;
                Client.UserLeft += OnUserLeft;
                Client.GuildMemberUpdated += OnGuildMemberUpdated;

                _logger.LogInformation("Discord Socket Client started.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while starting the Discord bot.");
            }
        }

        private async Task StopDiscordBotAsync()
        {
            if (Client == null)
            {
                return;
            }

            try
            {
                Client.Ready -= OnReady;
                Client.LoggedOut -= OnLoggedOut;
                Client.Disconnected -= OnDisconnected;
                Client.JoinedGuild -= OnJoinedGuild;
                Client.UserJoined -= OnUserJoined;
                Client.UserLeft -= OnUserLeft;
                Client.GuildMemberUpdated -= OnGuildMemberUpdated;

                await Client.LogoutAsync().ConfigureAwait(false);
                await Client.StopAsync().ConfigureAwait(false);
                Client.Dispose();
                Client = null;

                _logger.LogInformation("Discord Socket Client stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while stopping the Discord bot.");
            }
        }

        private Task OnReady()
        {
            _logger.LogInformation("Connected. Listing guilds...");

            if (Client != null)
            {
                foreach (var guild in Client.Guilds)
                {
                    _logger.LogInformation("Guild: {GuildName} ({GuildId})", guild.Name, guild.Id);
                }
            }

            return Task.CompletedTask;
        }

        private Task OnLoggedOut()
        {
            _logger.LogInformation("Logged out.");
            return Task.CompletedTask;
        }

        private Task OnDisconnected(Exception exception)
        {
            _logger.LogInformation("Disconnected: {Message}", exception.Message);
            return Task.CompletedTask;
        }

        private Task OnJoinedGuild(SocketGuild guild)
        {
            _logger.LogInformation("Joined guild: {GuildName} ({GuildId})", guild.Name, guild.Id);
            return Task.CompletedTask;
        }

        private async Task OnUserJoined(SocketGuildUser user)
        {
            await _syncService.HandleUserJoinedAsync(user).ConfigureAwait(false);
        }

        private async Task OnUserLeft(SocketGuild guild, SocketUser user)
        {
            await _syncService.HandleUserLeftAsync(user).ConfigureAwait(false);
        }

        private async Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser user)
        {
            await _syncService.ApplyDiscordRolesAsync(user).ConfigureAwait(false);
        }

        private Task OnDiscordLog(LogMessage message)
        {
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    _logger.LogError(message.Exception, "[{Severity}] {Source}: {Message}", message.Severity, message.Source, message.Message);
                    break;
                case LogSeverity.Warning:
                    _logger.LogWarning("[{Severity}] {Source}: {Message}", message.Severity, message.Source, message.Message);
                    break;
                case LogSeverity.Info:
                    _logger.LogInformation("[{Severity}] {Source}: {Message}", message.Severity, message.Source, message.Message);
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    _logger.LogDebug("[{Severity}] {Source}: {Message}", message.Severity, message.Source, message.Message);
                    break;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (DiscordAuthPlugin.Instance != null)
            {
                DiscordAuthPlugin.Instance.ConfigurationChanged -= OnConfigurationChanged;
            }

            // Do NOT block here. IHostedService.StopAsync is already called by the host before dispose.
            // Fire-and-forget any remaining cleanup to avoid deadlocks.
            if (Client != null)
            {
                var client = Client;
                Client = null;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await client.LogoutAsync().ConfigureAwait(false);
                        await client.StopAsync().ConfigureAwait(false);
                        client.Dispose();
                    }
                    catch
                    {
                        // Best effort cleanup.
                    }
                });
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (DiscordAuthPlugin.Instance != null)
            {
                DiscordAuthPlugin.Instance.ConfigurationChanged -= OnConfigurationChanged;
            }

            await StopDiscordBotAsync().ConfigureAwait(false);
        }
    }
}
