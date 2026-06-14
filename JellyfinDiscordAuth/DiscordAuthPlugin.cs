using System;
using System.Collections.Generic;
using JellyfinDiscordAuth.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace JellyfinDiscordAuth
{
    /// <summary>
    /// Discord authentication plugin entrypoint.
    /// </summary>
    public class DiscordAuthPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly ILogger<DiscordAuthPlugin> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscordAuthPlugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{DiscordAuthPlugin}"/> interface.</param>
        public DiscordAuthPlugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            ILogger<DiscordAuthPlugin> logger)
            : base(applicationPaths, xmlSerializer)
        {
            _logger = logger;
            Instance = this;

            _logger.LogInformation("Discord Plugin initialized.");
        }

        /// <inheritdoc />
        public override string Name => "Discord-Auth";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("359a7d2a-1c54-4e70-abbb-01bc73f098cf");

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static DiscordAuthPlugin? Instance { get; private set; }

        /// <inheritdoc />
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
    }
}
