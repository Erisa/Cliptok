namespace Cliptok.Tasks
{
    internal class PunishmentTasks
    {
        public static async Task<bool> CheckBansAsync()
        {
            DiscordGuild targetGuild = Program.homeGuild;
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);
            Dictionary<string, MemberPunishment> banList = Program.db.HashGetAll("bans").ToDictionary(
                x => x.Name.ToString(),
                x => JsonConvert.DeserializeObject<MemberPunishment>(x.Value)
            );
            if (banList == null | banList.Keys.Count == 0)
                return false;
            else
            {
                // The success value will be changed later if any of the unmutes are successful.
                bool success = false;
                foreach (KeyValuePair<string, MemberPunishment> entry in banList)
                {
                    MemberPunishment banEntry = entry.Value;
                    if (DateTime.Now > banEntry.ExpireTime)
                    {
                        targetGuild = await Program.discord.GetGuildAsync(banEntry.ServerId);
                        var user = await Program.discord.GetUserAsync(banEntry.MemberId);
                        await Bans.UnbanUserAsync(targetGuild, user, reason: "Ban naturally expired.");
                        success = true;

                    }

                }
#if DEBUG
                Program.discord.Logger.LogDebug(Program.CliptokEventID, "Checked bans at {time} with result: {result}", DateTime.Now, success);
#endif
                return success;
            }
        }
        public static async Task<bool> CheckMutesAsync()
        {
            Dictionary<string, MemberPunishment> muteList = Program.db.HashGetAll("mutes").ToDictionary(
                x => x.Name.ToString(),
                x => JsonConvert.DeserializeObject<MemberPunishment>(x.Value)
            );
            if (muteList == null | muteList.Keys.Count == 0)
                return false;
            else
            {
                // The success value will be changed later if any of the unmutes are successful.
                bool success = false;
                foreach (KeyValuePair<string, MemberPunishment> entry in muteList)
                {
                    MemberPunishment mute = entry.Value;
                    if (DateTime.Now > mute.ExpireTime)
                    {
                        await Helpers.MuteHelpers.UnmuteUserAsync(await Program.discord.GetUserAsync(mute.MemberId), "Mute has naturally expired.");
                        success = true;
                    }
                }
#if DEBUG
                Program.discord.Logger.LogDebug(Program.CliptokEventID, "Checked mutes at {time} with result: {result}", DateTime.Now, success);
#endif
                return success;
            }
        }
    }

}
