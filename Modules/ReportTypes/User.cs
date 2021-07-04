using DSharpPlus.Entities;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Cliptok.Modules
{
    /// <summary>
    /// User report type.
    /// Store the author ID.
    /// </summary>
    class UserReportInfo : ReportInfo
    {
        public UserReportInfo()
        {
        }

        public UserReportInfo(DiscordUser other)
        {
            _Author = other;
            AuthorID = _Author.Id;
        }

        [OnSerializing()]
        internal void WriteVolatile(StreamingContext context)
        {
            if (Author != null)
            {
                AuthorID = Author.Id;
            }
        }

        /// <summary>
        /// Can be reported multiple time.
        /// No one knows what the user is doing outside the server.
        /// </summary>
        /// <returns></returns>
        public override bool CanReportAfterReview()
        {
            // users can be reported multiple times
            return true;
        }

        public override async Task ReadInfo(DiscordGuild guild)
        {
            _Author = await Program.discord.GetUserAsync(AuthorID);
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
                   .WithAuthor(Username, null, AvatarUrl);
        }

        /// <summary>
        /// ID of the reported user.
        /// </summary>
        [JsonProperty("author_id")]
        public ulong AuthorID;

        [JsonIgnore()]
        private DiscordUser _Author;

        /// <summary>
        /// The author's discord user.
        /// </summary>
        [JsonIgnore()]
        public override DiscordUser Author { get { return _Author; } }

        /// <summary>
        /// Link to the user ID.
        /// </summary>
        public override string ContextLink { get { return $"<!@{Author.Id}>"; } }

        /// <summary>
        /// Always null.
        /// </summary>
        public override DiscordChannel Channel { get { return null; } }
    }

    partial class ReportTypes
    {
        static public ReportType UserReportType = new ReportType(typeof(UserReportInfo), "user", "User");
    }
}
