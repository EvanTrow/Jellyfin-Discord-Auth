using JellyfinDiscordAuth.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace JellyfinDiscordAuth
{
    /// <summary>
    /// Registers Discord Auth plugin services.
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton<DiscordUserSyncService>();
            serviceCollection.AddSingleton<DiscordBotService>();
            serviceCollection.AddHostedService(sp => sp.GetRequiredService<DiscordBotService>());

            serviceCollection.AddSingleton<DiscordAuthSessionService>();
            serviceCollection.AddHostedService(sp => sp.GetRequiredService<DiscordAuthSessionService>());

            serviceCollection.AddScoped<DiscordOAuthService>();
            serviceCollection.AddHttpClient();
            serviceCollection.AddSingleton<IStartupFilter, ScriptInjectorStartup>();
        }
    }
}
