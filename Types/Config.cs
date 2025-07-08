namespace Cliptok.Types
{
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
        public ulong ModmailUserId { get; private set; } = 0;

        [JsonProperty("announcementRoles")]
        public Dictionary<string, ulong> AnnouncementRoles { get; private set; }

        [JsonProperty("announcementRolesFriendlyNames")]
        public Dictionary<string, string> AnnouncementRolesFriendlyNames { get; private set; }

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
        public WorkflowConfig GitHubWorkflow { get; private set; } = default;

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

        [JsonProperty("supportForumId")]
        public ulong SupportForumId { get; private set; } = 0;

        [JsonProperty("everyoneExcludedRoles")]
        public List<ulong> EveryoneExcludedRoles { get; private set; } = new();

        [JsonProperty("feedbackHubForum")]
        public ulong FeedbackHubForum { get; private set; } = 0;

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

        [JsonProperty("insiderThreads")]
        public Dictionary<string, ulong> InsiderThreads { get; set; } = new();

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

        [JsonProperty("duplicateMessageThreshold")]
        public int DuplicateMessageThreshold { get; private set; } = 0;

        [JsonProperty("duplicateMessageSeconds")]
        public int DuplicateMessageSeconds { get; private set; } = 0;

        [JsonProperty("insiderThreadKeepLastPins")]
        public int InsiderThreadKeepLastPins { get; private set; } = 50; // 50 is the pin limit, so it would be silly to allow infinite

        [JsonProperty("warningLogReactionTimeMinutes")]
        public int WarningLogReactionTimeMinutes { get; private set; }

        [JsonProperty("enablePersistentDb")]
        public bool EnablePersistentDb { get; set; } = false;

        [JsonProperty("disableMicrosoftCommands")]
        public bool DisableMicrosoftCommands { get; set; } = false;

        [JsonProperty("noFun")]
        public bool NoFun { get; set; } = false;

        [JsonProperty("hastebinType")]
        public string HastebinType { get; set; } = "haste";

        [JsonProperty("messageCachePruneDays")]
        public int MessageCachePruneDays { get; set; } = 30;

        [JsonProperty("messageLogExcludedChannels")]
        public List<ulong> MessageLogExcludedChannels { get; set; } = new();

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

    public class LogChannelConfig
    {
        [JsonProperty("channelId")]
        public ulong ChannelId { get; private set; } = 0;

        [JsonProperty("webhookUrl")]
        public string WebhookUrl { get; private set; } = "";

        [JsonProperty("webhookEnvVar")]
        public string WebhookEnvVar { get; private set; } = "";
    }

}
