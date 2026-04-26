namespace Cliptok.Types
{
    public class UserWarning
    {
        [JsonProperty("targetUserId")]
        public ulong TargetUserId { get; set; }

        [JsonProperty("modUserId")]
        public ulong ModUserId { get; set; }

        [JsonProperty("warningId")]
        public long WarningId { get; set; }

        [JsonProperty("warnReason")]
        public string WarnReason { get; set; }

        [JsonProperty("warnTimestamp")]
        public DateTime WarnTimestamp { get; set; }

        [JsonProperty("contextLink")]
        public string ContextLink { get; set; }

        [JsonProperty("contextMessageReference")]
        public MessageReference ContextMessageReference { get; set; } = default;

        [JsonProperty("dmMessageReference")]
        public MessageReference DmMessageReference { get; set; } = default;

        [JsonProperty("type")]
        public WarningType Type { get; set; }

        [JsonProperty("stub")]
        public bool Stub { get; set; } = false;
        
        [JsonProperty("isPardoned")]
        public bool IsPardoned { get; set; } = false;
    }

    public class MessageReference
    {
        [JsonProperty("messageId")]
        public ulong MessageId { get; set; } = 0;

        [JsonProperty("channelId")]
        public ulong ChannelId { get; set; } = 0;
    }

    public class MemberPunishment
    {
        [JsonProperty("memberId")]
        public ulong MemberId { get; set; }

        [JsonProperty("actionTime")]
        public DateTime? ActionTime { get; set; }

        [JsonProperty("expireTime")]
        public DateTime? ExpireTime { get; set; }

        [JsonProperty("modId")]
        public ulong ModId { get; set; }

        [JsonProperty("serverId")]
        public ulong ServerId { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("contextMessageReference")]
        public MessageReference ContextMessageReference { get; set; }

        [JsonProperty("dmMessageReference")]
        public MessageReference DmMessageReference { get; set; }

        [JsonProperty("stub")]
        public bool Stub { get; set; } = false;
    }

    public class UserNote
    {
        [JsonProperty("targetUserId")]
        public ulong TargetUserId { get; set; }

        [JsonProperty("modUserId")]
        public ulong ModUserId { get; set; }

        [JsonProperty("noteText")]
        public string NoteText { get; set; }

        [JsonProperty("showOnModmail")]
        public bool ShowOnModmail { get; set; }

        [JsonProperty("showOnWarn")]
        public bool ShowOnWarn { get; set; }

        [JsonProperty("showAllMods")]
        public bool ShowAllMods { get; set; }

        [JsonProperty("showOnce")]
        public bool ShowOnce { get; set; }

        [JsonProperty("showOnJoinAndLeave")]
        public bool ShowOnJoinAndLeave { get; set; }

        [JsonProperty("noteId")]
        public long NoteId { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("type")]
        public WarningType Type { get; set; }
    }

    public class PendingUserOverride
    {
        [JsonProperty("channelId")]
        public ulong ChannelId { get; set; }

        [JsonProperty("overwrite")]
        public MockUserOverwrite Overwrite { get; set; }
    }

}
