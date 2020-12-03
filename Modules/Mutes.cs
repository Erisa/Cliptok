using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MicrosoftBot.Modules
{

    public static class Mutes 
    {

        public static TimeSpan RoundToNearest(this TimeSpan a, TimeSpan roundTo)
        {
            long ticks = (long)(Math.Round(a.Ticks / (double)roundTo.Ticks) * roundTo.Ticks);
            return new TimeSpan(ticks);
        }

        // Only to be used on naughty users.
        public static async System.Threading.Tasks.Task<bool> MuteUserAsync(DiscordMember naughtyMember, string reason, ulong moderatorId, DiscordGuild guild, DiscordChannel channel = null, TimeSpan muteDuration = default)
        {
            bool permaMute = false;
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);
            DiscordRole mutedRole = guild.GetRole(Program.cfgjson.MutedRole);
            DateTime? expireTime = DateTime.Now + muteDuration;
            DiscordMember moderator = await guild.GetMemberAsync(moderatorId);

            if (muteDuration == default)
            {
                permaMute = true;
                expireTime = null;
            }

            MemberPunishment newMute = new MemberPunishment()
            {
                MemberId = naughtyMember.Id,
                ModId = moderatorId,
                ServerId = guild.Id,
                ExpireTime = expireTime
            };

            await Program.db.HashSetAsync("mutes", naughtyMember.Id, JsonConvert.SerializeObject(newMute));

            try
            {
                await naughtyMember.GrantRoleAsync(mutedRole);
            }
            catch
            {
                return false;
            }
            
            try
            {
                if (permaMute)
                {
                    await logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyMember.Mention} was successfully muted by `{moderator.Username}#{moderator.Discriminator}` (`{moderatorId}`).\nReason: **{reason}**");
                    await naughtyMember.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} You have been muted in **{guild.Name}**!\nReason: **{reason}**");
                }
                    
                else
                {
                    await logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyMember.Mention} was successfully muted for {Warnings.TimeToPrettyFormat(muteDuration, false)} by `{moderator.Username}#{moderator.Discriminator}` (`{moderatorId}`).\nReason: **{reason}**");
                    await naughtyMember.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} You have been muted in **{guild.Name}** for {Warnings.TimeToPrettyFormat(muteDuration, false)}!\nReason: **{reason}**");

                }
            }
            catch
            {
                // A DM failing to send isn't important, but let's put it in chat just so it's somewhere.
                if (!(channel is null))
                    await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyMember.Mention} was muted for **{Warnings.TimeToPrettyFormat(muteDuration, false)}**!");

            }
            return true;
        }

        public static async Task<bool> UnmuteUserAsync(DiscordUser targetUser)
        {
            DiscordGuild guild = await Program.discord.GetGuildAsync(Program.cfgjson.ServerID);
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);

            // todo: store per-guild
            DiscordRole mutedRole = guild.GetRole(Program.cfgjson.MutedRole);
            DiscordMember member = null;
            try
            {
                member = await guild.GetMemberAsync(targetUser.Id);
            }
            catch
            {
                // they probably left :(
            }

            if (member == null)
            {
                await logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} Attempt to unmute <@{targetUser.Id}> failed!\n" +
                    $"Is the user in the server?");
            }
            else
            {
                // Perhaps we could be catching something specific, but this should do for now.
                try
                {
                    await member.RevokeRoleAsync(mutedRole);
                    await logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Information} Successfully unmuted <@{targetUser.Id}>!");
                }
                catch
                {
                    await logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} Attempt to removed Muted role from <@{targetUser.Id}> failed!" +
                    $"\nIf the role was removed manually, this error can be disregarded safely.");
                }
            }
            // Even if the bot failed to remove the role, it reported that failure to a log channel and thus the mute
            //  can be safely removed internally.
            await Program.db.HashDeleteAsync("mutes", targetUser.Id);

            return true;
        }

        public static async System.Threading.Tasks.Task<bool> CheckMutesAsync()
        {
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);
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
                        await UnmuteUserAsync(await Program.discord.GetUserAsync(mute.MemberId));
                }
#if DEBUG
                Console.WriteLine($"Checked mutes at {DateTime.Now} with result: {success}");
#endif
                return success;
            }
        }
    }

    public class MuteCmds : BaseCommandModule
    {
        [Command("unmute")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialMod)]
        public async Task UnmuteCmd(CommandContext ctx, DiscordUser targetUser)
        {
            DiscordGuild guild = ctx.Guild;
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);

            // todo: store per-guild
            DiscordRole mutedRole = guild.GetRole(Program.cfgjson.MutedRole);
            DiscordMember member = await guild.GetMemberAsync(targetUser.Id);

            if ((await Program.db.HashExistsAsync("mutes", targetUser.Id)) || member.Roles.Contains(mutedRole))
            {
                await Mutes.UnmuteUserAsync(targetUser);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Information} Successfully unmuted **{targetUser.Username}#{targetUser.Discriminator}**.");
            }
            else
                try
                {
                    await Mutes.UnmuteUserAsync(targetUser);
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} According to Discord that user is not muted, but I tried to unnmute them anyway. Hope it works.");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be muted, *and* an error ocurred while attempting to unmute them anyway. Please contact the bot owner, the error has been logged.");
                }
        }

        [Command("mute")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialMod)]
        public async Task MuteCmd(CommandContext ctx, DiscordMember targetMember, [RemainingText] string timeAndReason = "No reason specificed.")
        {
            if (targetMember.IsBot ||( Warnings.GetPermLevel(ctx.Member) == ServerPermLevel.TrialMod && Warnings.GetPermLevel(targetMember) >= ServerPermLevel.TrialMod))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
                return;
            }

            await ctx.Message.DeleteAsync();

            TimeSpan muteDuration = default;
            string possibleTime = timeAndReason.Split(' ').First();
            if (possibleTime.Length != 1)
            {
                string reason = timeAndReason;
                // Everything BUT the last character should be a number.
                string possibleNum = possibleTime.Remove(possibleTime.Length - 1);
                if (int.TryParse(possibleNum, out int timeLength))
                {
                    char possibleTimePeriod = possibleTime.Last();
                    muteDuration = ModCmds.ParseTime(possibleTimePeriod, timeLength);
                }
                else
                {
                    muteDuration = default;
                }

                if (muteDuration != default || possibleNum == "0")
                {
                    if (!timeAndReason.Contains(" "))
                        reason = "No reason specified.";
                    else
                    {
                        reason = timeAndReason.Substring(timeAndReason.IndexOf(' ') + 1, timeAndReason.Length - (timeAndReason.IndexOf(' ') + 1));
                    }
                }

                // await ctx.RespondAsync($"debug: {possibleNum}, {possibleTime}, {muteDuration.ToString()}, {reason}");
                Mutes.MuteUserAsync(targetMember, reason, ctx.User.Id, ctx.Guild, null, muteDuration);
                reason = reason.Replace("`", "\\`").Replace("*", "\\*");
                if (muteDuration == default)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Muted} {targetMember.Mention} has been muted: **{reason}**");
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Muted} {targetMember.Mention} has been muted for **{Warnings.TimeToPrettyFormat(muteDuration, false)}**: **{reason}**");
            }
        }
    }
}
