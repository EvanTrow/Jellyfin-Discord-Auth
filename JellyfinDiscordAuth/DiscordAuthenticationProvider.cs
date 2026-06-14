using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Authentication;

namespace JellyfinDiscordAuth
{
    /// <summary>
    /// Authentication provider for Discord OAuth users.
    /// Rejects password-based login since authentication is handled via Discord OAuth2.
    /// </summary>
    public class DiscordAuthenticationProvider : IAuthenticationProvider
    {
        /// <inheritdoc />
        public string Name => "Discord";

        /// <inheritdoc />
        public bool IsEnabled => true;

        /// <inheritdoc />
        public Task<ProviderAuthenticationResult> Authenticate(string username, string password)
        {
            throw new AuthenticationException("This account uses Discord authentication. Please log in via the Discord button.");
        }

        /// <inheritdoc />
        public bool HasPassword(User user) => false;

        /// <inheritdoc />
        public Task ChangePassword(User user, string newPassword)
        {
            throw new AuthenticationException("Password changes are not supported for Discord-authenticated users.");
        }
    }
}
