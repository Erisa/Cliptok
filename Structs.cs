namespace Cliptok
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

    public class ConfigJson
    {
        [JsonProperty("core")]
        public CoreConfig Core { get; private set; }

        [JsonProperty("redis")]
        public RedisConfig Redis { get; private set; }

        [JsonProperty("trialModRole")]
        public ulong TrialModRole { get; private set; }

        [JsonProperty("modRole")]
        public ulong ModRole { get; private set; }

        [JsonProperty("adminRole")]
        public ulong AdminRole { get; private set; }

        [JsonProperty("logChannel")]
        public ulong LogChannel { get; private set; } = 0;

        [JsonProperty("userLogChannel")]
        public ulong UserLogChannel { get; private set; } = 0;

        [JsonProperty("serverID")]
        public ulong ServerID { get; private set; }

        [JsonProperty("homeChannel")]
        public ulong HomeChannel { get; private set; } = 0;

        [JsonProperty("emoji")]
        public EmojiJson Emoji { get; private set; }

        [JsonProperty("mutedRole")]
        public ulong MutedRole { get; private set; }

        [JsonProperty("warningDaysThreshold")]
        public int WarningDaysThreshold { get; private set; }

        [JsonProperty("autoMuteThresholds")]
        public Dictionary<string, int> AutoMuteThresholds { get; private set; }

        [JsonProperty("recentWarningsPeriodHours")]
        public int RecentWarningsPeriodHours { get; private set; }

        [JsonProperty("recentWarningsAutoMuteThresholds")]
        public Dictionary<string, int> RecentWarningsAutoMuteThresholds { get; private set; }

        [JsonProperty("userRoles")]
        public UserRoleConfig UserRoles { get; private set; }

        [JsonProperty("restrictedWords")]
        public List<string> RestrictedWords { get; private set; }

        [JsonProperty("massMentionThreshold")]
        public int MassMentionThreshold { get; private set; }

        [JsonProperty("massEmojiThreshold")]
        public int MassEmojiThreshold { get; private set; }

        [JsonProperty("tierRoles")]
        public List<ulong> TierRoles { get; private set; }

        [JsonProperty("inviteExclusion")]
        public List<string> InviteExclusion { get; private set; }

        [JsonProperty("inviteIDExclusion")]
        public List<ulong> InviteIDExclusion { get; private set; }

        [JsonProperty("inviteTierRequirement")]
        public int InviteTierRequirement { get; private set; }

        [JsonProperty("unrestrictedEmojiChannels")]
        public List<ulong> UnrestrictedEmojiChannels { get; private set; }

        [JsonProperty("wordLists")]
        public List<WordListJson> WordListList { get; private set; }

        [JsonProperty("lockdownEnabledChannels")]
        public List<ulong> LockdownEnabledChannels { get; private set; }

        [JsonProperty("heartosoftId")]
        public ulong HeartosoftId { get; private set; }

        [JsonProperty("autoDehoistCharacters")]
        public string AutoDehoistCharacters { get; private set; }

        [JsonProperty("investigationsChannel")]
        public ulong InvestigationsChannelId { get; private set; }

        [JsonProperty("appealLink")]
        public string AppealLink { get; private set; }

        [JsonProperty("communityTechSupportRoleID")]
        public ulong CommunityTechSupportRoleID { get; private set; }

        [JsonProperty("techSupportChannel")]
        public ulong TechSupportChannel { get; private set; } = 0;

        [JsonProperty("supportLogChannel")]
        public ulong SupportLogChannel { get; private set; } = 0;

        [JsonProperty("supportRatelimitMinutes")]
        public int SupportRatelimitMinutes { get; private set; }

        [JsonProperty("massMentionBanThreshold")]
        public int MassMentionBanThreshold { get; private set; }

        [JsonProperty("secondaryAutoDehoistCharacters")]
        public string SecondaryAutoDehoistCharacters { get; private set; }

        [JsonProperty("modmailUserId")]
        public ulong ModmailUserId { get; private set; }

        [JsonProperty("announcementRoles")]
        public Dictionary<string, ulong> AnnouncementRoles { get; private set; }

        [JsonProperty("hastebinEndpoint")]
        public string HastebinEndpoint { get; private set; }

        [JsonProperty("modmailCategory")]
        public ulong ModmailCategory { get; private set; }

        [JsonProperty("lineLimit")]
        public int LineLimit { get; private set; }

        [JsonProperty("increasedLineLimit")]
        public int IncreasedLineLimit { get; private set; }

        [JsonProperty("lineLimitTier")]
        public int LineLimitTier { get; private set; }

        [JsonProperty("lineLimitExcludedChannels")]
        public List<ulong> LineLimitExcludedChannels { get; private set; }

        [JsonProperty("giveawaysChannel")]
        public ulong GiveawaysChannel { get; private set; } = 0;

        [JsonProperty("giveawayBot")]
        public ulong GiveawayBot { get; private set; }

        [JsonProperty("giveawayTriggerMessage")]
        public string GiveawayTriggerMessage { get; private set; }

        [JsonProperty("githubWorkflow")]
        public WorkflowConfig GitHubWorkflow { get; private set; }

        [JsonProperty("githubWorkflowPrivate")]
        public WorkflowConfig GitHubWorkflowPrivate { get; private set; }

        [JsonProperty("everyoneExcludedChannels")]
        public List<ulong> EveryoneExcludedChannels { get; private set; } = new();

        [JsonProperty("gitListDirectory")]
        public string GitListDirectory { get; private set; }

        [JsonProperty("dmLogChannelId")]
        public ulong DmLogChannelId { get; private set; } = 0;

        [JsonProperty("errorLogChannelId")]
        public ulong ErrorLogChannelId { get; private set; } = 0;

        [JsonProperty("mysteryLogChannelId")]
        public ulong MysteryLogChannelId { get; private set; } = 0;

        [JsonProperty("everyoneFilter")]
        public bool EveryoneFilter { get; private set; } = false;

        [JsonProperty("usernameAPILogChannel")]
        public ulong UsernameAPILogChannel { get; private set; } = 0;

        [JsonProperty("logChannels")]
        public Dictionary<string, LogChannelConfig> LogChannels { get; private set; }

        [JsonProperty("botOwners")]
        public List<ulong> BotOwners { get; private set; } = new();

        [JsonProperty("ignoredVoiceChannels")]
        public List<ulong> IgnoredVoiceChannels { get; private set; } = new();

        [JsonProperty("enableTextInVoice")]
        public bool EnableTextInVoice { get; private set; } = false;

        [JsonProperty("tqsRoleId")]
        public ulong TqsRoleId { get; private set; } = 0;

        [JsonProperty("supportForumIntroThreadId")]
        public ulong SupportForumIntroThreadId { get; private set; } = 0;

        [JsonProperty("supportForumId")]
        public ulong SupportForumId { get; private set; } = 0;

        [JsonProperty("everyoneExcludedRoles")]
        public List<ulong> EveryoneExcludedRoles { get; private set; } = new();

        [JsonProperty("feedbackHubForum")]
        public ulong FeedbackHubForum { get; private set; } = 0;

        [JsonProperty("forumIntroPosts")]
        public List<ulong> ForumIntroPosts { get; private set; } = new();

        [JsonProperty("insiderInfoChannel")]
        public ulong InsiderInfoChannel { get; private set; }

        [JsonProperty("insiderAnnouncementChannel")]
        public ulong InsiderAnnouncementChannel { get; private set; } = 0;

        private ulong insidersChannel;
        [JsonProperty("insidersChannel")]
        public ulong InsidersChannel
        {
            get => insidersChannel == 0 ? InsiderCommandLockedToChannel : insidersChannel;
            private set => insidersChannel = value;
        }

        [JsonProperty("insiderCommandLockedToChannel")]
        private ulong InsiderCommandLockedToChannel { get; set; } = 0;

        [JsonProperty("dmAutoresponseTimeLimit")]
        public int DmAutoresponseTimeLimit { get; private set; } = 0;

        [JsonProperty("autoDeleteEmptyThreads")]
        public bool AutoDeleteEmptyThreads { get; private set; } = false;

        [JsonProperty("insiderCanaryThread")]
        public ulong InsiderCanaryThread { get; set; } = 0;

        [JsonProperty("tqsMutedRole")]
        public ulong TqsMutedRole { get; private set; } = 0;

        [JsonProperty("tqsMuteDurationHours")]
        public int TqsMuteDurationHours { get; private set; }

        [JsonProperty("autoWarnMsgAutoDeleteDays")]
        public int AutoWarnMsgAutoDeleteDays { get; private set; }

        [JsonProperty("compromisedAccountBanMsgAutoDeleteDays")]
        public int CompromisedAccountBanMsgAutoDeleteDays { get; private set; }

        [JsonProperty("logLevel")]
        public Level LogLevel { get; private set; } = Level.Information;

        [JsonProperty("lokiURL")]
        public string LokiURL { get; private set; } = null;

        [JsonProperty("lokiServiceName")]
        public string LokiServiceName { get; private set; } = null;

        [JsonProperty("voiceChannelPurge")]
        public bool VoiceChannelPurge { get; private set; } = true;

        [JsonProperty("forumChannelAutoWarnFallbackChannel")]
        public ulong ForumChannelAutoWarnFallbackChannel { get; private set; } = 0;

        [JsonProperty("rulesAllowedPublicChannels")]
        public List<ulong> RulesAllowedPublicChannels { get; private set; } = new();

        [JsonProperty("mentionTrackExcludedChannels")]
        public List<ulong> MentionTrackExcludedChannels { get; private set; } = new();

        [JsonProperty("pingBotOwnersOnBadErrors")]
        public bool PingBotOwnersOnBadErrors { get; private set; } = false;

        [JsonProperty("githubWorkflowSucessString")]
        public string GithubWorkflowSucessString { get; private set; } = "";
        
        [JsonProperty("botCommandsChannel")]
        public ulong BotCommandsChannel { get; private set; }
    }

    public enum Level { Information, Warning, Error, Debug, Verbose }

    public class LogChannelConfig
    {
        [JsonProperty("channelId")]
        public ulong ChannelId { get; private set; } = 0;

        [JsonProperty("webhookUrl")]
        public string WebhookUrl { get; private set; } = "";

        [JsonProperty("webhookEnvVar")]
        public string WebhookEnvVar { get; private set; } = "";
    }

    public class WorkflowConfig
    {
        [JsonProperty("repo")]
        public string Repo { get; private set; }

        [JsonProperty("ref")]
        public string Ref { get; private set; }

        [JsonProperty("workflowId")]
        public string WorkflowId { get; private set; }
    }

    public class WordListJson
    {
        [JsonProperty("name")]
        public string Name { get; private set; }

        [JsonProperty("wholeWord")]
        public bool WholeWord { get; private set; }

        [JsonProperty("url")]
        public bool Url { get; private set; }

        [JsonProperty("reason")]
        public string Reason { get; private set; }

        public string[] Words { get; set; }

        [JsonProperty("excludedChannels")]
        public List<ulong> ExcludedChannels { get; private set; } = new();

        [JsonProperty("passive")]
        public bool Passive { get; private set; } = false;

        [JsonProperty("channelId")]
        public ulong? ChannelId { get; private set; }
    }
    public class EmojiJson
    {
        [JsonProperty("noPermissions")]
        public string NoPermissions { get; set; }

        [JsonProperty("warning")]
        public string Warning { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("deleted")]
        public string Deleted { get; set; }

        [JsonProperty("information")]
        public string Information { get; set; }

        [JsonProperty("muted")]
        public string Muted { get; set; }

        [JsonProperty("denied")]
        public string Denied { get; set; }

        [JsonProperty("banned")]
        public string Banned { get; set; }

        [JsonProperty("unbanned")]
        public string Unbanned { get; set; }

        [JsonProperty("ejected")]
        public string Ejected { get; set; }

        [JsonProperty("loading")]
        public string Loading { get; set; }

        [JsonProperty("success")]
        public string Success { get; set; }

        [JsonProperty("locked")]
        public string Locked { get; set; }

        [JsonProperty("connected")]
        public string Connected { get; set; }

        [JsonProperty("help")]
        public string Help { get; set; }

        [JsonProperty("shieldHelp")]
        public string ShieldHelp { get; set; }

        [JsonProperty("shieldMicrosoft")]
        public string ShieldMicrosoft { get; set; }

        [JsonProperty("unlock")]
        public string Unlock { get; set; }

        [JsonProperty("bsod")]
        public string BSOD { get; set; }

        [JsonProperty("userJoin")]
        public string UserJoin { get; set; }

        [JsonProperty("userLeave")]
        public string UserLeave { get; set; }

        [JsonProperty("userUpdate")]
        public string UserUpdate { get; set; }

        [JsonProperty("messageEdit")]
        public string MessageEdit { get; set; }

        [JsonProperty("clockTime")]
        public string ClockTime { get; set; }

        [JsonProperty("windows11")]
        public string Windows11 { get; set; }

        [JsonProperty("on")]
        public string On { get; set; }

        [JsonProperty("off")]
        public string Off { get; set; }

        [JsonProperty("insider")]
        public string Insider { get; set; }

        [JsonProperty("windows10")]
        public string Windows10 { get; set; }

    }

    public class CoreConfig
    {
        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("prefixes")]
        public List<string> Prefixes { get; private set; }
    }

    public class RedisConfig
    {
        [JsonProperty("host")]
        public string Host { get; private set; }

        [JsonProperty("port")]
        public ulong Port { get; private set; }
    }

    public class UserRoleConfig
    {
        [JsonProperty("insiderCanary")]
        public ulong InsiderCanary { get; private set; }


        [JsonProperty("insiderDev")]
        public ulong InsiderDev { get; private set; }

        [JsonProperty("insiderBeta")]
        public ulong InsiderBeta { get; private set; }

        [JsonProperty("insiderRP")]
        public ulong InsiderRP { get; private set; }

        [JsonProperty("insider10RP")]
        public ulong Insider10RP { get; private set; }

        [JsonProperty("insiderChat")]
        public ulong InsiderChat { get; private set; }

        [JsonProperty("patchTuesday")]
        public ulong PatchTuesday { get; private set; }

        [JsonProperty("giveaways")]
        public ulong Giveaways { get; private set; }
    }

    public class PhishingRequestBody
    {
        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class PhishingResponseBody
    {
        [JsonProperty("match")]
        public bool Match { get; set; }

        [JsonProperty("matches")]
        public List<PhishingMatch> Matches { get; set; }
    }

    public class PhishingMatch
    {
        [JsonProperty("followed")]
        public bool Followed { get; set; }

        [JsonProperty("domain")]
        public string Domain { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("trust_rating")]
        public float TrustRating { get; set; }
    }

    public class UsernameScamApiRequestJson
    {
        [JsonProperty("username")]
        public string Username { get; set; }
    }

    public class UsernameScamApiResponseJson
    {
        [JsonProperty("match")]
        public bool Match { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class ServerApiResponseJson
    {
        [JsonProperty("serverID")]
        public string ServerID { get; set; }

        [JsonProperty("match")]
        public bool Match { get; set; } = true;

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("vanity")]
        public string Vanity { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("invite")]
        public string Invite { get; set; }
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

    public class MockUserOverwrite
    {
        [JsonProperty("type")]
        public int Type { get; } = 1;

        [JsonProperty("allow")]
        public DiscordPermissions Allowed { get; set; }

        [JsonProperty("deny")]
        public DiscordPermissions Denied { get; set; }

        [JsonProperty("id")]
        public ulong Id { get; set; }
    }

}
