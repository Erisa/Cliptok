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
            public CachedDiscordSticker Sticker { get; set; }
        }

        public class CachedDiscordUser
        {
            public ulong Id { get; set; }
            public string Username { get; set; }
            public string DisplayName { get; set; }
            public string AvatarUrl { get; set; }
            public bool IsBot { get; set; }
            public List<BulkMessageLogStore> BulkMessageLogs { get; set; }
        }

        public class CachedDiscordSticker
        {
            public ulong Id { get; set; }
            public string Url { get; set; }
            public string Name { get; set; }
        }

        public class BulkMessageLogStore
        {
            public int Id { get; set; }
            public string PasteUrl { get; set; }
            public string DiscordUrl { get; set; }
            public DateTime CreatedAt { get; set; }
            public List<CachedDiscordUser> Users { get; set; }
        }
    }
}
