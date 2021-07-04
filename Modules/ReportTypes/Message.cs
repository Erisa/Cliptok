using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Cliptok.Modules
{
    /// <summary>
    /// Message report type.
    /// Store important information such as the author, content and timestamp.
    /// </summary>
    class MessageReportInfo : ReportInfo
    {
        public MessageReportInfo()
        {
        }

        public MessageReportInfo(DiscordMessage message)
        {
            Message = message;
            if (Message != null)
            {
                _Author = message.Author;
                MessageContentSaved = message.Content;
                MessageTimestampSaved = message.Timestamp;
                MessageTimestampEditSaved = (DateTimeOffset)(message.EditedTimestamp != null ? message.EditedTimestamp : MessageTimestampSaved);
            }
        }

        [OnSerializing()]
        internal void WriteVolatile(StreamingContext context)
        {
            if (Message != null)
            {
                AuthorID = Message.Author.Id;
                MessageID = Message.Id;
                ChannelID = Message.Channel.Id;
            }
        }

        public override async Task ReadInfo(DiscordGuild guild)
        {
            try
            {
                DiscordChannel channel = await Program.discord.GetChannelAsync(ChannelID);
                Message = await channel.GetMessageAsync(MessageID);
                _Author = Message.Author;
            }
            catch (Exception)
            {
                _Author = await Program.discord.GetUserAsync(AuthorID);
            }
        }

        public override DiscordEmbedBuilder GenerateEmbed()
        {
            string Username = null;
            string AvatarUrl = null;
            if (Author != null)
            {
                Username = Author.Username;
                AvatarUrl = Author.AvatarUrl;
            }

            return new DiscordEmbedBuilder()
                   .WithAuthor(Username, ContextLink, AvatarUrl)
                   .WithDescription(MessageContentSaved)
                   .WithTimestamp(MessageTimestampSaved);
        }

        /// <summary>
        /// Can report again only if the message has been edited.
        /// </summary>
        /// <returns></returns>
        public override bool CanReportAfterReview()
        {
            if (Message != null && Message.EditedTimestamp != null)
            {
                // allow report if the message was modified
                return Message.EditedTimestamp != MessageTimestampEditSaved;
            }

            return false;
        }

        public override IReportedContent GetReportedContent()
        {
            return new ReportedMessage(Message);
        }

        /// <summary>
        /// The reported message.
        /// </summary>
        [JsonProperty("message_id")]
        public ulong MessageID { get; set; }
        [JsonIgnore()]
        public DiscordMessage Message { get; set; }
        
        /// <summary>
        /// The author of the message.
        /// </summary>
        [JsonProperty("author_id")]
        public ulong AuthorID { get; set; }
        [JsonIgnore()]
        private DiscordUser _Author;
        [JsonIgnore()]
        public override DiscordUser Author { get { return _Author; } }

        /// <summary>
        /// The saved message content.
        /// So that if the author ever edit/remove his message to avoid the infraction.
        /// Bots can still have it.
        /// </summary>
        [JsonProperty("content")]
        public string MessageContentSaved { get; set; }

        /// <summary>
        /// Timestamp of the message.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTimeOffset MessageTimestampSaved { get; set; }

        /// <summary>
        /// Timestamp of the message.
        /// </summary>
        [JsonProperty("timestamp_edit")]
        public DateTimeOffset MessageTimestampEditSaved { get; set; }

        /// <summary>
        /// The channel ID the message was posted in.
        /// </summary>
        [JsonProperty("channel_id")]
        public ulong ChannelID { get; private set; }

        /// <summary>
        /// URL link to the message.
        /// </summary>
        [JsonIgnore()]
        public override string ContextLink
        {
            get
            {
                return Message != null && Message.JumpLink != null ? Message.JumpLink.AbsoluteUri : null;
            }
        }

        /// <summary>
        /// Discord channel the message was posted in.
        /// </summary>
        [JsonIgnore()]
        public override DiscordChannel Channel { get { return Message != null ? Message.Channel : null; } }
    }

    class ReportedMessage : IReportedContent
    {
        public ReportedMessage(DiscordMessage messageValue)
        {
            Message = messageValue;
        }

        /// <summary>
        /// The content can be deleted as long as the associated message exists.
        /// </summary>
        public override void TryDelete()
        {
            if (Message != null)
            {
                Message.DeleteAsync();
            }
        }

        /// <summary>
        /// Discord message associated with the reported content.
        /// </summary>
        public DiscordMessage Message { get; }
    }

    partial class ReportTypes
    {
        static public ReportType MessageReportType = new ReportType(typeof(MessageReportInfo), "message", "Message");
    }
}
