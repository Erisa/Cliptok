namespace Cliptok.Tasks
{
    internal class PunishmentTasks
    {
        public static async Task<bool> CheckBansAsync()
        {
            DiscordGuild targetGuild = Program.homeGuild;
            Dictionary<string, MemberPunishment> banList = (await Program.redis.HashGetAllAsync("bans")).ToDictionary(
                x => x.Name.ToString(),
                x => JsonConvert.DeserializeObject<MemberPunishment>(x.Value)
            );
            if (banList is null | banList.Keys.Count == 0)
                return false;
            else
            {
                // The success value will be changed later if any of the unmutes are successful.
                bool success = false;
                foreach (KeyValuePair<string, MemberPunishment> entry in banList)
                {
                    MemberPunishment banEntry = entry.Value;
                    if (DateTime.UtcNow > banEntry.ExpireTime)
                    {
                        targetGuild = await Program.discord.GetGuildAsync(banEntry.ServerId);
                        var user = await Program.discord.GetUserAsync(banEntry.MemberId);
                        await BanHelpers.UnbanUserAsync(targetGuild, user, reason: "Ban naturally expired.");
                        success = true;

                    }

                }
                Program.discord.Logger.LogDebug(Program.CliptokEventID, "Checked bans at {time} with result: {result}", DateTime.UtcNow, success);
                return success;
            }
        }
        public static async Task<bool> CheckMutesAsync()
        {
            Dictionary<string, MemberPunishment> muteList = (await Program.redis.HashGetAllAsync("mutes")).ToDictionary(
                x => x.Name.ToString(),
                x => JsonConvert.DeserializeObject<MemberPunishment>(x.Value)
            );
            if (muteList is null | muteList.Keys.Count == 0)
                return false;
            else
            {
                // The success value will be changed later if any of the unmutes are successful.
                bool success = false;
                foreach (KeyValuePair<string, MemberPunishment> entry in muteList)
                {
                    MemberPunishment mute = entry.Value;
                    if (DateTime.UtcNow > mute.ExpireTime)
                    {
                        await MuteHelpers.UnmuteUserAsync(await Program.discord.GetUserAsync(mute.MemberId), "Mute has naturally expired.", false);
                        success = true;
                    }
                }
                Program.discord.Logger.LogDebug(Program.CliptokEventID, "Checked mutes at {time} with result: {result}", DateTime.UtcNow, success);
                return success;
            }
        }

        // Cleans up public messages for automatic warnings & bans for compromised accounts
        public static async Task<bool> CleanUpPunishmentMessagesAsync()
        {
            if (Program.cfgjson.AutoWarnMsgAutoDeleteDays == 0 && Program.cfgjson.CompromisedAccountBanMsgAutoDeleteDays == 0)
                return false;

            // The success value will be changed later if any of the message deletes are successful.
            bool success = false;

            if (Program.cfgjson.AutoWarnMsgAutoDeleteDays > 0)
            {
                Dictionary<string, UserWarning> warnList = (await Program.redis.HashGetAllAsync("automaticWarnings")).ToDictionary(
                    x => x.Name.ToString(),
                    x => JsonConvert.DeserializeObject<UserWarning>(x.Value)
                );

                foreach (KeyValuePair<string, UserWarning> entry in warnList)
                {
                    UserWarning warn = entry.Value;
#if DEBUG
                    if (DateTime.UtcNow > warn.WarnTimestamp.AddSeconds(Program.cfgjson.AutoWarnMsgAutoDeleteDays))
#else
                    if (DateTime.UtcNow > warn.WarnTimestamp.AddDays(Program.cfgjson.AutoWarnMsgAutoDeleteDays))
#endif
                    {
                        try
                        {
                            var contextMessage = await DiscordHelpers.GetMessageFromReferenceAsync(warn.ContextMessageReference);
                            await contextMessage.DeleteAsync();
                            Program.redis.HashDelete("automaticWarnings", warn.WarningId);
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            // If we fail to delete the message, forget about it; this isn't incredibly important & we don't want to keep trying every task run
                            Program.redis.HashDelete("automaticWarnings", warn.WarningId);

                            // Log a warning too
                            Program.discord.Logger.LogWarning(ex, "Failed to clean up automatic warning message: {messageLink}; it will be skipped", warn.ContextLink);
                            continue;
                        }
                    }
                }
            }

            if (Program.cfgjson.CompromisedAccountBanMsgAutoDeleteDays > 0)
            {
                Dictionary<string, MemberPunishment> banList = (await Program.redis.HashGetAllAsync("compromisedAccountBans")).ToDictionary(
                    x => x.Name.ToString(),
                    x => JsonConvert.DeserializeObject<MemberPunishment>(x.Value)
                );

                foreach (KeyValuePair<string, MemberPunishment> entry in banList)
                {
                    MemberPunishment ban = entry.Value;
#if DEBUG
                    if (DateTime.UtcNow > ban.ActionTime.Value.AddSeconds(Program.cfgjson.CompromisedAccountBanMsgAutoDeleteDays))
#else
                    if (DateTime.UtcNow > ban.ActionTime.Value.AddDays(Program.cfgjson.CompromisedAccountBanMsgAutoDeleteDays))
#endif
                    {
                        try
                        {
                            var contextMessage = await DiscordHelpers.GetMessageFromReferenceAsync(ban.ContextMessageReference);
                            if (contextMessage is not null)
                                await contextMessage.DeleteAsync();
                            Program.redis.HashDelete("compromisedAccountBans", ban.MemberId);
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            // If we fail to delete the message, forget about it; this isn't incredibly important & we don't want to keep trying every task run
                            Program.redis.HashDelete("compromisedAccountBans", ban.MemberId);

                            // Log a warning too
                            var messageLink = ban.ContextMessageReference is null
                                ? "[no ContextMessageReference]"
                                : $"https://discord.com/channels/{ban.ServerId}/{ban.ContextMessageReference.ChannelId}/{ban.ContextMessageReference.MessageId}";
                            Program.discord.Logger.LogWarning(ex, "Failed to clean up compromised account ban message: {messageLink}; it will be skipped", messageLink);
                            continue;
                        }
                    }
                }
            }

            Program.discord.Logger.LogDebug(Program.CliptokEventID, "Checked for auto-warn and compromised account ban messages at {time} with result: {result}", DateTime.UtcNow, success);
            return success;
        }
    }

}
