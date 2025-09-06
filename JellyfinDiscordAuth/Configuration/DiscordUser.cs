using System;

namespace JellyfinDiscordAuth.Configuration
{
    // Discord user data.
    public class DiscordUser
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string? Global_name { get; set; }
        public long Discriminator { get; set; }
        public string Avatar { get; set; }
        public string Email { get; set; }
    }
}
