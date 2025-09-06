using System;
using System.Xml.Serialization;
using MediaBrowser.Model.Plugins;

namespace JellyfinDiscordAuth.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            // sets default options
            ClientId = string.Empty;
            ClientSecret = string.Empty;
            BotToken = string.Empty;
            ServerId = string.Empty;
            DefaultRoles = string.Empty;
            DiscordUserData = new SerializableDictionary<Guid, DiscordUser>();
        }

        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string BotToken { get; set; }
        public string ServerId { get; set; }
        public string DefaultRoles { get; set; }

        // Links the Jellyfin user to the Discord user
        [XmlElement("DiscordUserData")]
        public SerializableDictionary<Guid, DiscordUser> DiscordUserData { get; set; }
    }
}
