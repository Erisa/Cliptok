using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cliptok.Modules
{

    public static class Mutes
    {

        public static TimeSpan RoundToNearest(this TimeSpan a, TimeSpan roundTo)
        {
            long ticks = (long)(Math.Round(a.Ticks / (double)roundTo.Ticks) * roundTo.Ticks);
            return new TimeSpan(ticks);
        }

        // Only to be used on naughty users.
        public static async Task<bool> MuteUserAsync(DiscordUser naughtyUser, string reason, ulong moderatorId, DiscordGuild guild, DiscordChannel channel = null, TimeSpan muteDuration = default, bool alwaysRespond = false)
        {
            bool permaMute = false;
            DateTime? actionTime = DateTime.Now;
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);
            DiscordRole mutedRole = guild.GetRole(Program.cfgjson.MutedRole);
            DateTime? expireTime = actionTime + muteDuration;
            DiscordMember moderator = await guild.GetMemberAsync(moderatorId);

            DiscordMember naughtyMember = default;
            try
            {
                naughtyMember = await guild.GetMemberAsync(naughtyUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // nothing
            }

            if (muteDuration == default)
            {
                permaMute = true;
                expireTime = null;
            }

            MemberPunishment newMute = new()
            {
                MemberId = naughtyUser.Id,
                ModId = moderatorId,
                ServerId = guild.Id,
                ExpireTime = expireTime,
                ActionTime = actionTime,
                Reason = reason
            };

            await Program.db.HashSetAsync("mutes", naughtyUser.Id, JsonConvert.SerializeObject(newMute));

            if (naughtyMember != default)
            {
                try
                {
                    string fullReason = $"[Mute by {moderator.Username}#{moderator.Discriminator}]: {reason}";
                    await naughtyMember.GrantRoleAsync(mutedRole, fullReason);
                    try
                    {
                        try
                        {
                            await naughtyMember.TimeoutAsync(expireTime + TimeSpan.FromSeconds(10), fullReason);
                        }
                        catch (Exception e)
                        {
                            Program.discord.Logger.LogError(e, "Failed to issue timeout");
                        }

                        // Remove the member from any Voice Channel they're currently in.
                        await naughtyMember.ModifyAsync(x => x.VoiceChannel = null);
                    }
                    catch (DSharpPlus.Exceptions.UnauthorizedException)
                    {
                        // do literally nothing. who cares?
                    }

                }
                catch
                {
                    return false;
                }
            }

            try
            {
                if (permaMute)
                {
                    await logChannel.SendMessageAsync(new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} was successfully muted by {moderator.Mention} (`{moderatorId}`).\nReason: **{reason}**")
                        .WithAllowedMentions(Mentions.None)
                    );
                    if (naughtyMember != default)
                        await naughtyMember.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} You have been muted in **{guild.Name}**!\nReason: **{reason}**");
                }

                else
                {
                    await logChannel.SendMessageAsync(new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} was successfully muted for **{Warnings.TimeToPrettyFormat(muteDuration, false)}** by {moderator.Mention} (`{moderatorId}`)." +
                            $"\nReason: **{reason}**" +
                            $"\nMute expires: <t:{ModCmds.ToUnixTimestamp(expireTime)}:R>")
                        .WithAllowedMentions(Mentions.None)
                    );

                    if (naughtyMember != default)
                        await naughtyMember.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} You have been muted in **{guild.Name}** for **{Warnings.TimeToPrettyFormat(muteDuration, false)}**!" +
                            $"\nReason: **{reason}**" +
                            $"\nMute expires: <t:{ModCmds.ToUnixTimestamp(expireTime)}:R>");
                }
            }
            catch
            {
                // A DM failing to send isn't important, but let's put it in chat just so it's somewhere.
                if (channel is not null)
                {
                    if (muteDuration == default)
                        await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been muted: **{reason}**");
                    else
                        await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been muted for **{Warnings.TimeToPrettyFormat(muteDuration, false)}**: **{reason}**");
                    return true;
                }
            }

            if (channel is not null && alwaysRespond)
            {
                reason = reason.Replace("`", "\\`").Replace("*", "\\*");
                if (muteDuration == default)
                    await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been muted: **{reason}**");
                else
                    await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been muted for **{Warnings.TimeToPrettyFormat(muteDuration, false)}**: **{reason}**");
            }
            return true;
        }

        public static async Task<bool> UnmuteUserAsync(DiscordUser targetUser, string reason = "")
        {
            bool success = false;
            DiscordGuild guild = await Program.discord.GetGuildAsync(Program.cfgjson.ServerID);
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);

            // todo: store per-guild
            DiscordRole mutedRole = guild.GetRole(Program.cfgjson.MutedRole);
            DiscordMember member = default;
            try
            {
                member = await guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException ex)
            {
                Program.discord.Logger.LogWarning(eventId: Program.CliptokEventID, exception: ex, message: "Failed to unmute {0} in {1} because they weren't in the server.", $"{targetUser.Username}#{targetUser.Discriminator}", guild.Name);
            }

            if (member == default)
            {
                await logChannel.SendMessageAsync(
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Information} Attempt to remove Muted role from {targetUser.Mention} failed because the user could not be found.\nThis is expected if the user was banned or left.")
                        .WithAllowedMentions(Mentions.None)
                    );
            }
            else
            {
                // Perhaps we could be catching something specific, but this should do for now.
                try
                {
                    await member.RevokeRoleAsync(role: mutedRole, reason);
                    foreach (var role in member.Roles)
                    {
                        if (role.Name == "Muted" && role.Id != Program.cfgjson.MutedRole)
                        {
                            try
                            {
                                await member.RevokeRoleAsync(role: role, reason: reason);
                            }
                            catch
                            {
                                // ignore, continue to next role
                            }
                        }
                    }
                    success = true;
                }
                catch
                {
                    await logChannel.SendMessageAsync(
                        new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Error} Attempt to removed Muted role from {targetUser.Mention} failed because of a Discord API error!" +
                                $"\nIf the role was removed manually, this error can be disregarded safely.")
                            .WithAllowedMentions(Mentions.None)
                        );
                }
                try
                {
                    await member.TimeoutAsync(until: null, reason: reason);
                }
                catch (Exception ex)
                {
                    Program.discord.Logger.LogError(message: "Error ocurred trying to remove Timeout from {0}", args: member.Id, exception: ex, eventId: Program.CliptokEventID);
                }

                if (success)
                    await logChannel.SendMessageAsync(new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Information} Successfully unmuted {targetUser.Mention}!").WithAllowedMentions(Mentions.None));

            }
            // Even if the bot failed to remove the role, it reported that failure to a log channel and thus the mute
            //  can be safely removed internally.
            await Program.db.HashDeleteAsync("mutes", targetUser.Id);

            return true;
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
                        await UnmuteUserAsync(await Program.discord.GetUserAsync(mute.MemberId), "Mute has naturally expired.");
                        success = true;
                    }
                }
#if DEBUG
                Program.discord.Logger.LogDebug(Program.CliptokEventID, $"Checked mutes at {DateTime.Now} with result: {success}");
#endif
                return success;
            }
        }
    }

    public class MuteCmds : BaseCommandModule
    {
        [Command("unmute")]
        [Aliases("umute")]
        [Description("Unmutes a previously muted user, typically ahead of the standard expiration time. See also: mute")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task UnmuteCmd(CommandContext ctx, [Description("The user you're trying to unmute.")] DiscordUser targetUser, string reason = "No reason provided.")
        {
            reason = $"[Manual unmute by {ctx.User.Username}#{ctx.User.Discriminator}]: {reason}";

            // todo: store per-guild
            DiscordRole mutedRole = ctx.Guild.GetRole(Program.cfgjson.MutedRole);

            DiscordMember member = default;
            try
            {
                member = await ctx.Guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException ex)
            {
                Program.discord.Logger.LogWarning(eventId: Program.CliptokEventID, exception: ex, message: "Failed to unmute {0} in {1} because they weren't in the server.", $"{targetUser.Username}#{targetUser.Discriminator}", ctx.Guild.Name);
            }

            if ((await Program.db.HashExistsAsync("mutes", targetUser.Id)) || (member != default && member.Roles.Contains(mutedRole)))
            {
                await Mutes.UnmuteUserAsync(targetUser, reason);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Information} Successfully unmuted **{targetUser.Username}#{targetUser.Discriminator}**.");
            }
            else
                try
                {
                    await Mutes.UnmuteUserAsync(targetUser, reason);
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} According to Discord that user is not muted, but I tried to unmute them anyway. Hope it works.");
                }
                catch (Exception e)
                {
                    Program.discord.Logger.LogError(e, $"An error ocurred unmuting {targetUser.Id}");
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be muted, *and* an error ocurred while attempting to unmute them anyway. Please contact the bot owner, the error has been logged.");
                }
        }

        [Command("mute")]
        [Description("Mutes a user, preventing them from sending messages until they're unmuted. See also: unmute")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task MuteCmd(
            CommandContext ctx, [Description("The user you're trying to mute")] DiscordUser targetUser,
            [RemainingText, Description("Combined argument for the time and reason for the mute. For example '1h rule 7' or 'rule 10'")] string timeAndReason = "No reason specified."
        )
        {
            DiscordMember targetMember = default;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // is this worth logging?
            }

            if (targetMember != default && Warnings.GetPermLevel(ctx.Member) == ServerPermLevel.TrialModerator && (Warnings.GetPermLevel(targetMember) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
            {
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
                return;
            }

            await ctx.Message.DeleteAsync();
            bool timeParsed = false;

            TimeSpan muteDuration = default;
            string possibleTime = timeAndReason.Split(' ').First();
            string reason = timeAndReason;

            try
            {
                muteDuration = HumanDateParser.HumanDateParser.Parse(possibleTime).Subtract(ctx.Message.Timestamp.DateTime);
                timeParsed = true;
            }
            catch
            {
                // keep default
            }

            if (timeParsed)
            {
                int i = reason.IndexOf(" ") + 1;
                reason = reason[i..];
            }

            if (timeParsed && possibleTime == reason)
                reason = "No reason specified.";

            _ = Mutes.MuteUserAsync(targetUser, reason, ctx.User.Id, ctx.Guild, ctx.Channel, muteDuration, true);
        }
    }
}
