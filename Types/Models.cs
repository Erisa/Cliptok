using Microsoft.EntityFrameworkCore;

namespace Cliptok.Types
{
    public class Models
    {
        public class CachedDiscordMessage
        {
            public ulong Id { get; set; }
            public ulong ChannelId { get; set; }
            public string Content { get; set; }
            public DateTime Timestamp { get; set; }

            public List<string>? AttachmentURLs { get; set; }

            public CachedDiscordUser User { get; set; }
        }

        public class CachedDiscordUser
        {
            public ulong Id { get; set; }
            public string Username { get; set; }
            public string DisplayName { get; set; }
            public string AvatarUrl { get; set; }
            public bool IsBot { get; set; }
        }
    }
}
