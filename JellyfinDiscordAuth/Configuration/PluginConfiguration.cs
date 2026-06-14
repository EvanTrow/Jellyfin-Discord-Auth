using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using MediaBrowser.Model.Plugins;

namespace JellyfinDiscordAuth.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            // sets default options
            ServerUrl = string.Empty;
            ClientId = string.Empty;
            ClientSecret = string.Empty;
            BotToken = string.Empty;
            ServerId = string.Empty;
            DefaultRoles = string.Empty;
            AdminRoleId = string.Empty;
            LibraryRoleMappings = new List<LibraryRoleMapping>();
            DiscordUserData = new SerializableDictionary<Guid, DiscordUser>();
        }

        public string ServerUrl { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string BotToken { get; set; }
        public string ServerId { get; set; }
        public string DefaultRoles { get; set; }
        public string AdminRoleId { get; set; }
        public List<LibraryRoleMapping> LibraryRoleMappings { get; set; }

        // Links the Jellyfin user to the Discord user
        [XmlElement("DiscordUserData")]
        public SerializableDictionary<Guid, DiscordUser> DiscordUserData { get; set; }
    }
}
