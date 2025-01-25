using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Cliptok.Events
{
    public class MockDiscordMessage
    {
        public MockDiscordMessage() { }

        public MockDiscordMessage(DiscordMessage baseMessage)
        {
            Attachments = baseMessage.Attachments;
            Author = baseMessage.Author;
            BaseMessage = baseMessage;
            Channel = baseMessage.Channel;
            ChannelId = baseMessage.ChannelId;
            Content = baseMessage.Content;
            Embeds = baseMessage.Embeds;
            Id = baseMessage.Id;
            JumpLink = baseMessage.JumpLink;
            MentionedUsers = baseMessage.MentionedUsers;
            MentionedUsersCount = baseMessage.MentionedUsers.Count;
            MessageSnapshots = baseMessage.MessageSnapshots;
            Reactions = baseMessage.Reactions;
            ReferencedMessage = baseMessage.ReferencedMessage;
            Stickers = baseMessage.Stickers;
            Timestamp = baseMessage.Timestamp;
        }
        
        public MockDiscordMessage(IReadOnlyList<DiscordAttachment> attachments = default, DiscordUser author = default, DiscordChannel channel = default, ulong channelId = default, string content = default, IReadOnlyList<DiscordEmbed> embeds = default, ulong id = default, Uri jumpLink = default, IReadOnlyList<DiscordUser> mentionedUsers = default, int mentionedUsersCount = default, IReadOnlyList<DiscordMessageSnapshot> messageSnapshots = default, IReadOnlyList<DiscordReaction> reactions = default, DiscordMessage referencedMessage = default, IReadOnlyList<DiscordMessageSticker> stickers = default, DateTimeOffset? timestamp = default)
        {
            Attachments = attachments;
            Author = author;
            Channel = channel;
            ChannelId = channelId;
            Content = content;
            Embeds = embeds;
            Id = id;
            JumpLink = jumpLink;
            MentionedUsers = mentionedUsers;
            MentionedUsersCount = mentionedUsersCount;
            MessageSnapshots = messageSnapshots;
            Reactions = reactions;
            ReferencedMessage = referencedMessage;
            Stickers = stickers;
            Timestamp = timestamp;
        }
        
        public IReadOnlyList<DiscordAttachment> Attachments { get; }
        public DiscordUser Author { get; }
        public DiscordMessage BaseMessage { get; }
        public DiscordChannel Channel { get; }
        public ulong ChannelId { get; }
        public string Content { get; }
        public IReadOnlyList<DiscordEmbed> Embeds { get; }
        public ulong Id { get; }
        public Uri JumpLink { get; set; }
        public IReadOnlyList<DiscordUser> MentionedUsers { get; }
        public int MentionedUsersCount { get; }
        public IReadOnlyList<DiscordMessageSnapshot> MessageSnapshots { get; }
        public IReadOnlyList<DiscordReaction> Reactions { get; set; }
        public DiscordMessage ReferencedMessage { get; set; }
        public IReadOnlyList<DiscordMessageSticker> Stickers { get; set; }
        public DateTimeOffset? Timestamp { get; }

        public async Task DeleteAsync()
        {
            // If we have a DiscordMessage to work with, call its delete method.
            // Otherwise, fetch the message ourselves and delete it.

            if (BaseMessage is not null)
            {
                await BaseMessage.DeleteAsync();
            }
            else
            {
                var baseMessage = await Channel.GetMessageAsync(Id);
                await baseMessage.DeleteAsync();
            }
        }
    }
}