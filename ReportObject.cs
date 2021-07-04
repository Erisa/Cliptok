using DSharpPlus.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Cliptok
{
    abstract class IReportedContent
    {
        /// <summary>
        /// Try to delete whatever the content is.
        /// </summary>
        /// <returns></returns>
        public virtual void TryDelete() { }
    }

    /// <summary>
    /// Abstract class describing the reported content.
    /// </summary>
    abstract class ReportInfo
    {
        public ReportInfo()
        {
            // automatically set the type
            Type = ReportType.GetType(this.GetType());
        }

        /// <summary>
        /// Return whether or not the content can be reported again after review.
        /// </summary>
        /// <returns></returns>
        public virtual bool CanReportAfterReview() { return false; }

        /// <summary>
        /// Return the reported content.
        /// </summary>
        /// <returns></returns>
        public virtual IReportedContent GetReportedContent() { return null; }

        /// <summary>
        /// Generate an embed describing the content.
        /// </summary>
        /// <returns>An embed builder.</returns>
        public abstract DiscordEmbedBuilder GenerateEmbed();

        /// <summary>
        /// Initialize fields, objects, etc, anything from their ID.
        /// </summary>
        /// <param name="guild">Necessary to get channels, users, etc.</param>
        public abstract Task ReadInfo(DiscordGuild guild);


        /// <summary>
        /// The report type for this object
        /// </summary>
        [JsonIgnore()]
        public ReportType Type { get; }

        /// <summary>
        /// Author of the message.
        /// </summary>
        public abstract DiscordUser Author { get; }

        /// <summary>
        /// Link to the reported thing.
        /// </summary>
        public abstract string ContextLink { get; }

        /// <summary>
        /// The channel of the reported message.
        /// </summary>
        public abstract DiscordChannel Channel { get; }
    }

    /// <summary>
    /// Used to convert between any *ReportInfo* class and JSON, with the *type* field.
    /// </summary>
    public class ReportInfoConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            ReportInfo info = (ReportInfo)value;

            JObject jObject = JObject.FromObject(value);
            jObject["type"] = info.Type.Name;
            serializer.Serialize(writer, jObject);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // load json data
            JObject jObject = JObject.Load(reader);
            // get report type from object
            ReportType reportType = ReportType.GetType(jObject.GetValue("type").ToString());

            // create the specific report
            ReportInfo newReportInfo = (ReportInfo)Activator.CreateInstance(reportType.ClassType);

            // serialize the specific report
            serializer.Populate(jObject.CreateReader(), newReportInfo);

            return newReportInfo;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType.IsSubclassOf(typeof(ReportInfo));
        }
    }

    /// <summary>
    /// Self-explanatory, contains info about the class to use for the report.
    /// </summary>
    class ReportType
    {
        /// <summary>
        /// Initialize a new report type.
        /// </summary>
        /// <param name="typeValue">Class to use for the report type, must inherit from *ReportInfo*</param>
        /// <param name="nameValue">Name of the type (code name).</param>
        /// <param name="descriptionValue">Short human-readable text describing the type.</param>
        public ReportType(Type typeValue, string nameValue, string descriptionValue)
        {
            Name = nameValue;
            Description = descriptionValue;
            ClassType = typeValue;
            Types.Add(this);
        }

        ~ReportType()
        {
            Types.Remove(this);
        }

        /// <summary>
        /// Return the report type by name.
        /// </summary>
        /// <param name="name">Name to get the type from.</param>
        /// <returns></returns>
        static public ReportType GetType(string name)
        {
            return Types.Find(t => t.Name == name);
        }

        /// <summary>
        /// Return the report type by class type.
        /// </summary>
        /// <param name="reportType">Class type to get report from.</param>
        /// <returns></returns>
        static public ReportType GetType(Type reportType)
        {
            return Types.Find(t => t.ClassType == reportType);
        }

        /// <summary>
        /// The class type to use for this report type.
        /// </summary>
        [JsonIgnore()]
        public Type ClassType { get; }

        /// <summary>
        /// Name of the report type.
        /// </summary>
        [JsonProperty()]
        public string Name { get; }

        /// <summary>
        /// The human readable description.
        /// </summary>
        [JsonIgnore()]
        public string Description { get; }

        /// <summary>
        /// List of all report types.
        /// </summary>
        static public List<ReportType> Types { get; } = new List<ReportType>();
    }

    /// <summary>
    /// The status of a report.
    /// </summary>
    enum ReportStatus
    {
        /// <summary>
        /// The report is currently pending.
        /// </summary>
        Pending,
        /// <summary>
        /// The report was reviewed and validated by a moderator.
        /// </summary>
        Validated,
        /// <summary>
        /// The report was reviewed and rejected by a moderator.
        /// </summary>
        Rejected
    };

    /// <summary>
    /// Stores various information about the reported message.
    /// </summary>
    class ReportObject
    {
        public ReportObject()
        {
            Signalers = new List<DiscordMember>();
        }

        /// <summary>
        /// Initialize the report object.
        /// </summary>
        /// <param name="idValue">The report ID.</param>
        /// <param name="guild">Discord guild that this report is on.</param>
        /// <param name="reportInfo">Abstract report information.</param>
        /// <param name="reasonValue">The reason given for the report.</param>
        public ReportObject(ulong idValue, DiscordGuild guild, ReportInfo reportInfo, string reasonValue)
        {
            Id = idValue;
            MessageData = reportInfo;
            GuildID = guild.Id;
            Signalers = new List<DiscordMember>();
            Reason = reasonValue;
            Status = ReportStatus.Pending;
        }

        /// <summary>
        /// Sets IDs before serializing.
        /// </summary>
        [OnSerializing()]
        internal void WriteVolatile(StreamingContext context)
        {
            if (ReportHandle != null)
            {
                ReportHandleID = ReportHandle.Id;
                ReportChannelID = ReportHandle.Channel.Id;
            }

            if (HandledBy != null)
            {
                HandledByID = HandledBy.Id;
            }

            // gather members ID
            SignalersID = new ulong[Signalers.Count];
            ulong index = 0;
            foreach (DiscordMember signaler in Signalers)
            {
                SignalersID[index++] = signaler.Id;
            }
        }

        /// <summary>
        /// Read and get objects from their ID.
        /// </summary>
        public async Task ReadVolatile()
        {
            // get channel and message
            try
            {
                DiscordGuild guild = await Program.discord.GetGuildAsync(GuildID);
                if (guild != null)
                {
                    await MessageData.ReadInfo(guild);
                }

                // gather users who reported
                Signalers.Clear();
                foreach (ulong signalerID in SignalersID)
                {
                    try
                    {
                        DiscordMember member = await guild.GetMemberAsync(signalerID);
                        Signalers.Add(member);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }

            try
            {
                // get the generated report message
                DiscordChannel handleChannel = await Program.discord.GetChannelAsync(ReportChannelID);
                if (handleChannel != null)
                {
                    ReportHandle = await handleChannel.GetMessageAsync(ReportHandleID);
                }
            }
            catch (Exception)
            {
            }

            try
            {
                HandledBy = await Program.discord.GetUserAsync(HandledByID);
            }
            catch (Exception)
            {
            }
        }

        public delegate void ReportNotifyDelegate(ReportObject report, DiscordMember member);
        /// <summary>
        /// Callback for all users who reported.
        /// </summary>
        /// <param name="callback">Callback</param>
        public void NotifyUserReport(ReportNotifyDelegate callback)
        {
            // notify all users about the report
            foreach (DiscordMember member in Signalers)
            {
                callback(this, member);
            }
        }

        /// <summary>
        /// The report ID.
        /// </summary>
        [JsonProperty("id")]
        public ulong Id { get; private set; }

        /// <summary>
        /// The main guild of the report.
        /// </summary>
        [JsonProperty("guild_id")]
        public ulong GuildID { get; private set; }

        /// <summary>
        /// The message information.
        /// </summary>
        [JsonProperty(), JsonConverter(typeof(ReportInfoConverter))]
        public ReportInfo MessageData { get; private set; }

        /// <summary>
        /// List of users who reported the message.
        /// </summary>
        [JsonProperty("signalers_id")]
        public ulong[] SignalersID { get; private set; }
        [JsonIgnore()]
        public List<DiscordMember> Signalers { get; }
        [JsonIgnore()]
        public DiscordMember ReportOwner
        {
            get
            {
                return Signalers.Count > 0 ? Signalers[0] : null;
            }
        }

        /// <summary>
        /// Reason given to the message.
        /// </summary>
        [JsonProperty("reason")]
        public string Reason { get; set; }

        /// <summary>
        /// Handle to the message the bot created for the report.
        /// </summary>
        [JsonProperty("report_handle")]
        public ulong ReportHandleID { get; set; }
        [JsonIgnore()]
        public DiscordMessage ReportHandle;

        /// <summary>
        /// The channel the report handle was created in.
        /// </summary>
        [JsonProperty("report_channel_id")]
        public ulong ReportChannelID { get; set; }

        /// <summary>
        /// The current report status.
        /// </summary>
        [JsonProperty("status")]
        public ReportStatus Status { get; set; }

        /// <summary>
        /// The current report status.
        /// </summary>
        [JsonProperty("handled_by_id")]
        public ulong HandledByID { get; private set; }
        [JsonIgnore()]
        public DiscordUser HandledBy;

        /// <summary>
        /// The action that was taken after validation.
        /// </summary>
        [JsonIgnore()]
        public string ActionTaken;
    }
}
