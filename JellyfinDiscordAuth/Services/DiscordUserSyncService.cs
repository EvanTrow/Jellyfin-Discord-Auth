using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using JellyfinDiscordAuth.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace JellyfinDiscordAuth.Services
{
    /// <summary>
    /// Service responsible for synchronizing Discord roles with Jellyfin user permissions.
    /// </summary>
    public class DiscordUserSyncService
    {
        private readonly IUserManager _userManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<DiscordUserSyncService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscordUserSyncService"/> class.
        /// </summary>
        /// <param name="userManager">The user manager.</param>
        /// <param name="libraryManager">The library manager.</param>
        /// <param name="logger">The logger.</param>
        public DiscordUserSyncService(
            IUserManager userManager,
            ILibraryManager libraryManager,
            ILogger<DiscordUserSyncService> logger)
        {
            _userManager = userManager;
            _libraryManager = libraryManager;
            _logger = logger;
        }

        private PluginConfiguration Configuration =>
            DiscordAuthPlugin.Instance?.Configuration
            ?? throw new InvalidOperationException("Plugin instance is not available.");

        /// <summary>
        /// Applies Discord roles to a Jellyfin user, updating permissions and library access.
        /// </summary>
        /// <param name="discordUser">The Discord guild user.</param>
        public async Task ApplyDiscordRolesAsync(SocketGuildUser discordUser)
        {
            try
            {
                var savedDiscordUser = Configuration.DiscordUserData?.FirstOrDefault(x => x.Value.Id == discordUser.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)) ?? default;

                var discordUserRoles = discordUser.Roles.Where(r => !r.IsEveryone);
                User? user = savedDiscordUser.Key.ToString() == "00000000-0000-0000-0000-000000000000"
                    ? _userManager.GetUserByName(discordUser.Username)
                    : _userManager.GetUserById(savedDiscordUser.Key) ?? _userManager.GetUserByName(discordUser.Username);

                if (user == null)
                {
                    return;
                }

                _logger.LogInformation("{Username} ({UserId}) updated.", discordUser.Username, discordUser.Id);

                if (!discordUserRoles.Any())
                {
                    user.SetPermission(PermissionKind.IsDisabled, true);
                }
                else
                {
                    var adminRoleId = (Configuration.AdminRoleId ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(adminRoleId))
                    {
                        bool isAdmin = discordUserRoles.Any(r => string.Equals(r.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), adminRoleId, StringComparison.Ordinal));
                        user.SetPermission(PermissionKind.IsAdministrator, isAdmin);
                        user.SetPermission(PermissionKind.EnableContentDeletion, isAdmin);
                        user.SetPermission(PermissionKind.EnableAllFolders, isAdmin);
                        user.SetPreference(PreferenceKind.EnabledFolders, isAdmin ? [] : GetLibraryAccess(discordUserRoles));
                    }
                    else
                    {
                        user.SetPreference(PreferenceKind.EnabledFolders, GetLibraryAccess(discordUserRoles));
                    }

                    user.SetPermission(PermissionKind.IsDisabled, false);
                }

                await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating user folder access from discord update event.");
            }
        }

        /// <summary>
        /// Handles a user joining the Discord server.
        /// </summary>
        /// <param name="user">The guild user.</param>
        public async Task HandleUserJoinedAsync(SocketGuildUser user)
        {
            try
            {
                _logger.LogInformation("{Username} ({UserId}) joined the server. Adding default roles.", user.Username, user.Id);

                var defaultRoleIds = Configuration.LibraryRoleMappings
                    .Where(m => m.UseDefaultAssign && !string.IsNullOrWhiteSpace(m.RoleId))
                    .Select(m => m.RoleId.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                foreach (string role in defaultRoleIds)
                {
                    try
                    {
                        _logger.LogInformation("Assigning default role {Role} to {Username} ({UserId}).", role, user.Username, user.Id);
                        ulong roleId = ulong.Parse(role, System.Globalization.CultureInfo.InvariantCulture);
                        var guild = user.Guild;
                        if (guild.Roles.Any(r => r.Id == roleId))
                        {
                            await user.AddRoleAsync(roleId).ConfigureAwait(false);
                        }
                        else
                        {
                            _logger.LogWarning("Role {RoleId} not found in server {ServerId}.", roleId, guild.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred while adding default role {Role} to new user.", role);
                    }
                }

                try
                {
                var savedDiscordUser = Configuration.DiscordUserData?.FirstOrDefault(x => x.Value.Id == user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)) ?? default;
                    if (savedDiscordUser.Key.ToString() != "00000000-0000-0000-0000-000000000000")
                    {
                        User? jellyfinUser = _userManager.GetUserById(savedDiscordUser.Key);
                        if (jellyfinUser != null)
                        {
                            _logger.LogInformation("{Username} ({UserId}) rejoined the server. Enabling Jellyfin user.", user.Username, user.Id);
                            jellyfinUser.SetPermission(PermissionKind.IsDisabled, false);
                            await _userManager.UpdateUserAsync(jellyfinUser).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while enabling the Jellyfin user.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing user join.");
            }
        }

        /// <summary>
        /// Handles a user leaving the Discord server.
        /// </summary>
        /// <param name="user">The Discord user.</param>
        public async Task HandleUserLeftAsync(SocketUser user)
        {
            try
            {
                _logger.LogInformation("{Username} ({UserId}) left the server. Disabling Jellyfin user.", user.Username, user.Id);

                var savedDiscordUser = Configuration.DiscordUserData?.FirstOrDefault(x => x.Value.Id == user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)) ?? default;
                if (savedDiscordUser.Key.ToString() != "00000000-0000-0000-0000-000000000000")
                {
                    User? jellyfinUser = _userManager.GetUserById(savedDiscordUser.Key);
                    if (jellyfinUser != null)
                    {
                        jellyfinUser.SetPermission(PermissionKind.IsDisabled, true);
                        await _userManager.UpdateUserAsync(jellyfinUser).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while disabling the Jellyfin user.");
            }
        }

        /// <summary>
        /// Gets the list of library IDs the user has access to based on their Discord roles.
        /// </summary>
        /// <param name="discordUserRoles">The Discord roles.</param>
        /// <returns>Array of library IDs.</returns>
        public string[] GetLibraryAccess(IEnumerable<SocketRole> discordUserRoles)
        {
            var libraries = _libraryManager.GetVirtualFolders();
            var mappings = Configuration.LibraryRoleMappings;
            var libraryAccess = new List<string>();

            if (mappings != null && mappings.Count > 0)
            {
                var roleIds = discordUserRoles.Select(r => r.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToHashSet(StringComparer.Ordinal);
                foreach (var mapping in mappings)
                {
                    if (string.IsNullOrWhiteSpace(mapping.LibraryId) || string.IsNullOrWhiteSpace(mapping.RoleId))
                    {
                        continue;
                    }

                    if (roleIds.Contains(mapping.RoleId) && libraries.Any(l => l.ItemId == mapping.LibraryId))
                    {
                        libraryAccess.Add(mapping.LibraryId);
                    }
                }

                return libraryAccess.Distinct(StringComparer.Ordinal).ToArray();
            }

            foreach (var library in libraries)
            {
                if (discordUserRoles.Any(r => r.Name == library.Name))
                {
                    libraryAccess.Add(library.ItemId);
                }
            }

            return libraryAccess.ToArray();
        }
    }
}
