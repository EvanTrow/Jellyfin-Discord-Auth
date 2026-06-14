using System;

namespace JellyfinDiscordAuth.Services
{
    /// <summary>
    /// Represents a temporary authentication session for the Discord OAuth flow.
    /// </summary>
    public class DiscordAuthSession
    {
        /// <summary>
        /// Gets or sets the temporary exchange code presented to the frontend.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Jellyfin access token.
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Jellyfin user identifier.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the Jellyfin server identifier.
        /// </summary>
        public string ServerId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the expiration time.
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
