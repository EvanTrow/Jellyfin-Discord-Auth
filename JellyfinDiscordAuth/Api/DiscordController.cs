using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using JellyfinDiscordAuth.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace JellyfinDiscordAuth.Api
{
    [ApiController]
    [Route("DiscordAuth")]
    public class DiscordController : ControllerBase
    {
        private readonly IUserManager _userManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ISessionManager _sessionManager;
        private readonly IAuthorizationContext _authContext;
        private readonly ILogger<DiscordController> _logger;
        private readonly ICryptoProvider _cryptoProvider;
        private readonly IProviderManager _providerManager;
        private readonly IServerConfigurationManager _serverConfigurationManager;

        // handles the Discord OAuth2
        public DiscordController(
            ILogger<DiscordController> logger,
            ISessionManager sessionManager,
            IUserManager userManager,
            ILibraryManager libraryManager,
            IAuthorizationContext authContext,
            ICryptoProvider cryptoProvider,
            IProviderManager providerManager,
            IServerConfigurationManager serverConfigurationManager)
        {
            _sessionManager = sessionManager;
            _userManager = userManager;
            _libraryManager = libraryManager;
            _authContext = authContext;
            _cryptoProvider = cryptoProvider;
            _logger = logger;

            _providerManager = providerManager;
            _serverConfigurationManager = serverConfigurationManager;
        }

        // Generate the Discord OAuth2 authorize URL and redirect the user to Discord.
        [HttpGet("Login")]
        public IActionResult Login()
        {
            // Get the full request URL
            var request = HttpContext.Request;
            var redirectUri = $"{request.Scheme}://{request.Host}/DiscordAuth/Callback";

            // 1. Retrieve config (ClientId, RedirectUri, etc.)
            var config = DiscordAuthPlugin.Instance.Configuration;
            // 2. Build the authorize URL with the necessary query params
            // example: https://discord.com/oauth2/authorize?response_type=code&client_id=4984984984984984984984&scope=identify%20guilds.join&state=9844984wefw9e8f4984984894wef&redirect_uri=https%3A%2F%2Fnicememe.website&prompt=consent&integration_type=0
            string authorizeUrl = $"https://discord.com/oauth2/authorize?response_type=code&client_id={config.ClientId}&scope=identify%20email&redirect_uri={Uri.EscapeDataString(redirectUri)}&prompt=consent";
            // 3. Redirect to Discord
            return Redirect(authorizeUrl);
        }

        // The Discord OAuth2 callback endpoint.
        [HttpGet("Callback")]
        [Produces(MediaTypeNames.Application.Json)]
        public async Task<IActionResult> Callback([FromQuery] string code)
        {
            try
            {
                // Get the full request URL
                var request = HttpContext.Request;
                var redirectUri = $"{request.Scheme}://{request.Host}/DiscordAuth/Callback";

                _logger.LogInformation("Discord OAuth2 callback with code: {code}", code);

                // 1. Retrieve Discord config from PluginConfiguration:
                var config = DiscordAuthPlugin.Instance.Configuration;

                // 2. Exchange code for an access token:
                using (var httpClient = new HttpClient())
                {
                    var tokenResponse = await httpClient.PostAsync(
                        "https://discord.com/api/oauth2/token",
                        new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            { "client_id", config.ClientId },
                            { "client_secret", config.ClientSecret },
                            { "grant_type", "authorization_code" },
                            { "code", code },
                            { "redirect_uri", redirectUri }
                        }));
                    var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                    dynamic tokenData = JsonConvert.DeserializeObject(tokenJson);
                    string accessToken = tokenData.access_token;

                    // 3. Use the access token to fetch user info from Discord:
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    var userResponse = await httpClient.GetAsync("https://discord.com/api/users/@me");
                    var userJson = await userResponse.Content.ReadAsStringAsync();
                    DiscordUser userData = JsonConvert.DeserializeObject<DiscordUser>(userJson);
                    string username = userData?.Global_name ?? userData.Username;
                    _logger.LogInformation("Discord user data: {userJson}", userJson);

                    SocketGuildUser discordUser = null;
                    IEnumerable<SocketRole> discordUserRoles = Enumerable.Empty<SocketRole>();
                    if (!string.IsNullOrWhiteSpace(config.ServerId.Trim()))
                    {
                        try
                        {
                            var server = DiscordAuthPlugin.Client.GetGuild(ulong.Parse(config.ServerId));
                            await server.DownloadUsersAsync();
                            discordUser = server.Users.FirstOrDefault(u => u.Id == ulong.Parse(userData.Id));
                            if (discordUser == null)
                                return Content(HtmlError("You must be a member of the server to access Jellyfin."), MediaTypeNames.Text.Html);
                            discordUserRoles = discordUser.Roles.Where(r => !r.IsEveryone);
                            if (discordUserRoles.Count() == 0)
                                return Content(HtmlError($"You must have a role other than @everyone to access Jellyfin. Roles: {string.Join(", ", discordUserRoles.Select(r => r.Name))}"), MediaTypeNames.Text.Html);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error while fetching server users and roles");
                            return Problem("Something went wrong while fetching server users and roles. Please try again later.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Server ID is not set in the plugin configuration, skipping role check");
                    }

                    // 4. Match or create a Jellyfin user (by Discord ID or username), then log them in:
                    var savedDiscordUser = config?.DiscordUserData?.FirstOrDefault(x => x.Value.Id == userData.Id) ?? default;
                    // Check if the user exists in Jellyfin:
                    User user = savedDiscordUser.Key.ToString() == "00000000-0000-0000-0000-000000000000" ?
                        _userManager.GetUserByName(username) ?? null :
                        _userManager.GetUserById(savedDiscordUser.Key) ?? _userManager.GetUserByName(username) ?? null;
                    if (user == null)
                    {
                        try
                        {
                            _logger.LogInformation("SSO user {Username} doesn't exist, creating...", username);
                            user = await _userManager.CreateUserAsync(username).ConfigureAwait(false);
                            user.AuthenticationProviderId = GetType().FullName;
                            user.Password = _cryptoProvider.CreatePasswordHash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))).ToString();
                            await _userManager.UpdateUserAsync(user).ConfigureAwait(false);

                            _logger.LogInformation("Discord user {Username} not found in config, creating...", username);
                            savedDiscordUser = new KeyValuePair<Guid, DiscordUser>(user.Id, userData);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error creating user {Username}", username);
                            return Problem("Something went wrong");
                        }
                    }
                    else
                    {
                        try
                        {
                            _logger.LogInformation("Discord user {Username} found in config, updating...", username);
                            savedDiscordUser = new KeyValuePair<Guid, DiscordUser>(user.Id, userData);

                            user = _userManager.GetUserById(savedDiscordUser.Key);
                            user.Username = username;
                            await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error updateing user {Username}", username);
                            return Problem("Something went wrong");
                        }
                    }

                    // apply folder access from discord roles
                    await DiscordAuthPlugin.Instance.ApplyDiscordRoles(discordUser);

                    // Save the Discord avatar URL:
                    try
                    {
                        var avatarUrl = $"https://cdn.discordapp.com/avatars/{userData.Id}/{userData.Avatar}.png";

                        using var client = new HttpClient();
                        var avatarResponse = await client.GetAsync(avatarUrl);

                        if (!avatarResponse.Content.Headers.TryGetValues("content-type", out var contentTypeList))
                        {
                            throw new Exception("Cannot get Content-Type of image : " + avatarUrl);
                        }

                        var contentType = contentTypeList.First();
                        if (!contentType.StartsWith("image"))
                        {
                            throw new Exception("Content type of avatar URL is not an image, got :  " + contentType);
                        }

                        var extension = contentType.Split("/").Last();
                        var stream = await avatarResponse.Content.ReadAsStreamAsync();
                        if (user != null)
                        {
                            var userDataPath = Path.Combine(_serverConfigurationManager.ApplicationPaths.UserConfigurationDirectoryPath, user.Username);
                            if (user.ProfileImage is not null)
                            {
                                await _userManager.ClearProfileImageAsync(user).ConfigureAwait(false);
                            }

                            user.ProfileImage = new ImageInfo(Path.Combine(userDataPath, "profile" + extension));
                            await _providerManager.SaveImage(stream, contentType, user.ProfileImage.Path)
                                .ConfigureAwait(false);
                            await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e.Message);
                    }

                    // Update the DiscordUserData dictionary in the config:
                    config.DiscordUserData[savedDiscordUser.Key] = savedDiscordUser.Value;
                    DiscordAuthPlugin.Instance.SaveConfiguration();

                    // 5. Authenticate the user:
                    var authRequest = new AuthenticationRequest();
                    try
                    {
                        authRequest.UserId = user.Id;
                        authRequest.Username = user.Username;
                        authRequest.App = "Discord";
                        authRequest.AppVersion = "1.0.0.0";
                        authRequest.DeviceId = Guid.NewGuid().ToString();
                        authRequest.DeviceName = "Discord OAuth2";
                        var authenticationResult = await _sessionManager.AuthenticateDirect(authRequest).ConfigureAwait(false);
                        _logger.LogInformation("Auth request created...");

                        var html = $@"
                            <html>
                                <head>
                                    <script>
                                        var userId = 'user-' + '{authenticationResult.User.Id}' + '-' + '{authenticationResult.User.ServerId}';
                                        var user = {{
                                            Id: '{authenticationResult.User.Id}',
                                            ServerId: '{authenticationResult.User.ServerId}',
                                            EnableAutoLogin: true
                                        }};
                                        localStorage.setItem(userId, JSON.stringify(user));
                                        var jfCreds = JSON.parse(localStorage.getItem('jellyfin_credentials')) || {{ Servers: [{{}}] }};
                                        jfCreds['Servers'][0]['AccessToken'] = '{authenticationResult.AccessToken}';
                                        jfCreds['Servers'][0]['UserId'] = '{authenticationResult.User.Id}';
                                        localStorage.setItem('jellyfin_credentials', JSON.stringify(jfCreds));
                                        localStorage.setItem('enableAutoLogin', 'true');
                                        window.location.replace('/web/index.html');
                                    </script>
                                </head>
                                <body>
                                    <h1>Authenticating...</h1>
                                </body>
                            </html>";
                        return Content(html, MediaTypeNames.Text.Html);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error authenticating user {Username}", username);
                        return Problem("Something went wrong");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Discord OAuth2 callback");
                return Problem("Something went wrong");
            }
        }

        private string HtmlError(string message)
        {
            return $@"
                <html>
                    <head>
                        <title>Error</title>
                        <script>
                            window.onload = function () {{
                                alert('{message}');
                                window.location.href = '/web/index.html';
                            }};
                        </script>
                    </head>
                    <body>
                        <h1>Error</h1>
                        <p>{message}</p>
                    </body>
                </html>";
        }
    }
}
