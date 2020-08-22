using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public static async System.Threading.Tasks.Task<bool> MuteUserAsync(DiscordMember naughtyMember, TimeSpan muteDuration, string reason, ulong moderatorId, DiscordGuild guild, DiscordChannel channel = null)
        {
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);
            DiscordRole mutedRole = guild.GetRole(Program.cfgjson.MutedRole);
            DateTime expireTime = DateTime.Now + muteDuration;
            MemberMute newMute = new MemberMute()
            {
                MemberId = naughtyMember.Id,
                ExpireTime = expireTime,
                ModId = moderatorId,
                ServerId = guild.Id
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

            await logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} Successfully muted {naughtyMember.Mention} until `{expireTime}` (In roughly {muteDuration.TotalHours} hours)");
            try
            {
                await naughtyMember.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} You have been muted in **{guild.Name}** for {Warnings.TimeToPrettyFormat(muteDuration)}!");
            }
            catch
            {
                // A DM failing to send isn't important, but let's put it in chat just so it's somewhere.
                if (!(channel is null))
                    await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyMember.Mention} was muted for **{Warnings.TimeToPrettyFormat(muteDuration)}**!");

            }
            return true;
        }

        public static async Task<bool> UnmuteUserAsync(DiscordUser targetUser)
        {
            DiscordGuild guild = await Program.discord.GetGuildAsync(Program.cfgjson.ServerID);
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);

            // todo: store per-guild
            DiscordRole mutedRole = guild.GetRole(Program.cfgjson.MutedRole);
            DiscordMember member = await guild.GetMemberAsync(targetUser.Id);
            if (member == null)
            {
                await logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} Attempt to unmute <@{targetUser.Id}> failed!" +
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
            Dictionary<string, MemberMute> muteList = Program.db.HashGetAll("mutes").ToDictionary(
                x => x.Name.ToString(),
                x => JsonConvert.DeserializeObject<MemberMute>(x.Value)
            );
            if (muteList == null | muteList.Keys.Count == 0)
                return false;
            else
            {
                // The success value will be changed later if any of the unmutes are successful.
                bool success = false;
                foreach (KeyValuePair<string, MemberMute> entry in muteList)
                {
                    MemberMute mute = entry.Value;
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
            DiscordGuild guild = await Program.discord.GetGuildAsync(ctx.Guild.Id);
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);

            // todo: store per-guild
            DiscordRole mutedRole = guild.GetRole(Program.cfgjson.MutedRole);
            DiscordMember member = await guild.GetMemberAsync(targetUser.Id);

            if ((await Program.db.HashExistsAsync("mutes", targetUser.Id)) || member.Roles.Contains(mutedRole))
            {
                await Mutes.UnmuteUserAsync(targetUser);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Information} Successfully unmuted **{targetUser.Username}#{targetUser.Discriminator}**");
            }
            else
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user isn't muted!");
        }
    }
}
