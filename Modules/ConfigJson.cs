﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Cliptok
{
    public class UserWarning
    {
        [JsonProperty("targetUserId")]
        public ulong TargetUserId { get; set; }

        [JsonProperty("modUserId")]
        public ulong ModUserId { get; set; }

        [JsonProperty("warningId")]
        public ulong WarningId { get; set; }

        [JsonProperty("warnReason")]
        public string WarnReason { get; set; }

        [JsonProperty("warnTimestamp")]
        public DateTime WarnTimestamp { get; set; }

        [JsonProperty("contextLink")]
        public string ContextLink { get; set; }

    }

    public class MemberPunishment
    {
        [JsonProperty("memberId")]
        public ulong MemberId { get; set; }

        [JsonProperty("expireTime")]
        public DateTime? ExpireTime { get; set; }

        [JsonProperty("modId")]
        public ulong ModId { get; set; }

        [JsonProperty("serverId")]
        public ulong ServerId { get; set; }
    }

    public struct ConfigJson
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
        public ulong LogChannel { get; set; }

        [JsonProperty("serverID")]
        public ulong ServerID { get; set; }

        [JsonProperty("homeChannel")]
        public ulong HomeChannel { get; set; }

        [JsonProperty("emoji")]
        public EmojiJson Emoji { get; set; }

        [JsonProperty("mutedRole")]
        public ulong MutedRole { get; set; }

        [JsonProperty("warningDaysThreshold")]
        public int WarningDaysThreshold { get; set; }

        [JsonProperty("autoMuteThresholds")]
        public Dictionary<string, int> AutoMuteThresholds { get; set; }

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

        [JsonProperty("inviteTierRequirement")]
        public int InviteTierRequirement { get; private set; }

        [JsonProperty("unrestrictedEmojiChannels")]
        public List<ulong> UnrestrictedEmojiChannels { get; private set; }

        [JsonProperty("wordLists")]
        public Dictionary<string, WordListJson> WordListList { get; private set; }

        [JsonProperty("lockdownEnabledChannels")]
        public List<ulong> LockdownEnabledChannels { get; private set; }

        [JsonProperty("heartosoftId")]
        public ulong HeartosoftId { get; private set; }

        [JsonProperty("noHeartosoftId")]
        public ulong NoHeartosoftId { get; private set; }

        [JsonProperty("restrictedHeartosoftPhrases")]
        public List<string> RestrictedHeartosoftPhrases { get; private set; }

        [JsonProperty("autoDehoistCharacters")]
        public List<char> AutoDehoistCharacters { get; private set; }

        [JsonProperty("investigationsChannel")]
        public ulong InvestigationsChannelId { get; private set; }

    }

    public class WordListJson
    {
        [JsonProperty("wholeWord")]
        public bool WholeWord { get; private set; }

        [JsonProperty("reason")]
        public string Reason { get; private set; }

        public string[] Words { get; set; }
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
        [JsonProperty("insiderDev")]
        public ulong InsiderDev { get; private set; }

        [JsonProperty("insiderBeta")]
        public ulong InsiderBeta { get; private set; }

        [JsonProperty("insiderRP")]
        public ulong InsiderRP { get; private set; }

        [JsonProperty("patchTuesday")]
        public ulong PatchTuesday { get; private set; }
    }

}
