using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Jellyfin.Data;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using JellyfinDiscordAuth.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JellyfinDiscordAuth
{
    public class DiscordAuthPlugin : BasePlugin<PluginConfiguration>, IPlugin, IHasWebPages, IDisposable
    {
        public static DiscordSocketClient Client;
        private readonly IUserManager _userManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<DiscordAuthPlugin> _logger;

        public DiscordAuthPlugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            IUserManager userManager,
            ILibraryManager libraryManager,
            ILogger<DiscordAuthPlugin> logger) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;

            _userManager = userManager;
            _libraryManager = libraryManager;
            _logger = logger;

            StartDiscordBot().Wait();

            ConfigurationChanged += (sender, args) =>
            {
                _logger.LogInformation("Configuration changed. Restarting Discord Plugin.");

                StopDiscordBot().Wait();
                StartDiscordBot().Wait();
            };

            _logger.LogInformation("Discord Plugin initialized.");
        }

        public override string Name => "Discord-Auth";
        public override Guid Id => Guid.Parse("359a7d2a-1c54-4e70-abbb-01bc73f098cf");
        public static DiscordAuthPlugin Instance { get; private set; }

        // load pages from the embedded resource
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return
            [
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html",
                },
            ];
        }

        public async Task StartDiscordBot()
        {
            try
            {
                var config = new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.Guilds | GatewayIntents.GuildMembers
                };
                Client = new DiscordSocketClient(config);
                Client.Log += Log;

                await Client.LoginAsync(TokenType.Bot, DiscordAuthPlugin.Instance.Configuration.BotToken);
                await Client.StartAsync();

                Client.Ready += () =>
                {
                    _logger.LogInformation("Connected. Listing guilds...");

                    foreach (var guild in Client.Guilds)
                    {
                        _logger.LogInformation($"Guild: {guild.Name} ({guild.Id})");
                    }

                    return Task.CompletedTask;
                };

                Client.LoggedOut += () =>
                {
                    _logger.LogInformation("Logged out.");
                    return Task.CompletedTask;
                };

                Client.Disconnected += (e) =>
                {
                    _logger.LogInformation($"Disconnected: {e.Message}");
                    return Task.CompletedTask;
                };

                Client.JoinedGuild += (guild) =>
                {
                    _logger.LogInformation($"Joined guild: {guild.Name} ({guild.Id})");
                    return Task.CompletedTask;
                };

                Client.UserJoined += DiscordUserJoined;
                Client.UserLeft += DiscordUserLeft;
                Client.GuildMemberUpdated += async (Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser user) => await ApplyDiscordRoles(user);

                _logger.LogInformation("Discord Socket Client Service started.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while starting the Discord bot.");
            }
        }

        private async Task DiscordUserJoined(SocketGuildUser user)
        {
            try
            {
                _logger.LogInformation($"{user.Username} ({user.Id}) joined the server. Adding default roles.");

                string[] defaultRoles = Configuration.DefaultRoles.Split(',')
                    .Select(r => r.Trim()) // remove leading/trailing whitespace
                    .Where(r => r.Length > 0).ToArray(); // remove empty strings
                foreach (string role in defaultRoles)
                {
                    try
                    {
                        ulong roleId = ulong.Parse(role);
                        if (Client.GetGuild(ulong.Parse(Configuration.ServerId)).Roles.Any(r => r.Id == roleId))
                        {
                            await user.AddRoleAsync(roleId);
                        }
                        else
                        {
                            _logger.LogWarning($"Role {roleId} not found in server {Configuration.ServerId}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"An error occurred while adding default role {role} to new user.");
                    }
                }

                try
                {
                    // enabled jellyfin user if exists
                    var savedDiscordUser = Configuration?.DiscordUserData?.FirstOrDefault(x => x.Value.Id == user.Id.ToString()) ?? default;
                    if (savedDiscordUser.Key.ToString() != "00000000-0000-0000-0000-000000000000")
                    {
                        User jellyfinUser = _userManager.GetUserById(savedDiscordUser.Key);
                        if (jellyfinUser != null)
                        {
                            _logger.LogInformation($"{user.Username} ({user.Id}) rejoined the server. Enabling Jellyfin user.");
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

        private async Task DiscordUserLeft(SocketGuild guild, SocketUser user)
        {
            try
            {
                _logger.LogInformation($"{user.Username} ({user.Id}) left the server. Disabling Jellyfin user.");
                // disabled jellyfin user if exists
                var savedDiscordUser = Configuration?.DiscordUserData?.FirstOrDefault(x => x.Value.Id == user.Id.ToString()) ?? default;
                if (savedDiscordUser.Key.ToString() != "00000000-0000-0000-0000-000000000000")
                {
                    User jellyfinUser = _userManager.GetUserById(savedDiscordUser.Key);
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

        public async Task ApplyDiscordRoles(SocketGuildUser discordUser)
        {
            try
            {
                var savedDiscordUser = Configuration?.DiscordUserData?.FirstOrDefault(x => x.Value.Id == discordUser.Id.ToString()) ?? default;

                IEnumerable<SocketRole> discordUserRoles = discordUser.Roles.Where(r => !r.IsEveryone);
                User user = savedDiscordUser.Key.ToString() == "00000000-0000-0000-0000-000000000000" ?
                    _userManager.GetUserByName(discordUser.Username) ?? null :
                    _userManager.GetUserById(savedDiscordUser.Key) ?? _userManager.GetUserByName(discordUser.Username) ?? null;
                if (user != null)
                {
                    _logger.LogInformation($"{discordUser.Username} ({discordUser.Id}) updated.");

                    if (discordUserRoles.Count() == 0)
                    {
                        // disable user if they don't have access
                        user.SetPermission(PermissionKind.IsDisabled, true);
                    }
                    else
                    {
                        bool isAdmin = discordUserRoles.Any(r => r.Name == "Admin");
                        user.SetPermission(PermissionKind.IsAdministrator, isAdmin);
                        user.SetPermission(PermissionKind.EnableContentDeletion, isAdmin);
                        user.SetPermission(PermissionKind.EnableAllFolders, isAdmin);
                        user.SetPreference(PreferenceKind.EnabledFolders, isAdmin ? [] : GetLibraryAccess(discordUserRoles));
                        user.SetPermission(PermissionKind.IsDisabled, false);
                    }
                    await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating user folder access from discord update event.");
            }
        }

        public string[] GetLibraryAccess(IEnumerable<SocketRole> discordUserRoles)
        {
            var libraries = _libraryManager.GetVirtualFolders();
            List<string> libraryAccess = new List<string>();
            foreach (var library in libraries)
            {
                if (discordUserRoles.Any(r => r.Name == library.Name))
                {
                    libraryAccess.Add(library.ItemId);
                }
            }
            return libraryAccess.ToArray();
        }

        public async Task StopDiscordBot()
        {
            if (Client != null)
            {
                await Client.LogoutAsync();
                await Client.StopAsync();
                Client.Dispose();
            }
        }

        private Task Log(LogMessage message)
        {
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    _logger.LogError(message.Exception, $"[{message.Severity,8}] {message.Source}: {message.Message}");
                    break;
                case LogSeverity.Warning:
                    _logger.LogWarning($"[{message.Severity,8}] {message.Source}: {message.Message}");
                    break;
                case LogSeverity.Info:
                    _logger.LogInformation($"[{message.Severity,8}] {message.Source}: {message.Message}");
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    _logger.LogDebug($"[{message.Severity,8}] {message.Source}: {message.Message}");
                    break;
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing Discord Plugin.");
            StopDiscordBot().Wait();
        }
    }
}
