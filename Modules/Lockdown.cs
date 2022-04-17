using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Cliptok.Modules
{
    class Lockdown : BaseCommandModule
    {
        public bool ongoingLockdown = false;

        public static async Task LockChannelAsync(DiscordChannel channel, TimeSpan? duration = null, string reason = "")
        {
            if (!Program.cfgjson.LockdownEnabledChannels.Contains(channel.Id))
            {
                return;
            }

            await channel.AddOverwriteAsync(channel.Guild.CurrentMember, Permissions.SendMessages, Permissions.None, "Failsafe 1 for Lockdown");
            await channel.AddOverwriteAsync(channel.Guild.GetRole(Program.cfgjson.ModRole), Permissions.SendMessages, Permissions.None, "Failsafe 2 for Lockdown");
            await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages, "Lockdown command");

            string msg;
            if (reason == "")
                msg = $"{Program.cfgjson.Emoji.Locked} This channel has been locked by a Moderator.";
            else
                msg = $"{Program.cfgjson.Emoji.Locked} This channel has been locked: **{reason}**";

            if (duration != null)
            {
                await Program.db.HashSetAsync("unlocks", channel.Id, ModCmds.ToUnixTimestamp(DateTime.Now + duration));
                msg += $"\nChannel unlocks: <t:{ModCmds.ToUnixTimestamp(DateTime.Now + duration)}:R>";
            }

            await channel.SendMessageAsync(msg);
        }

        [Command("lockdown")]
        [Aliases("lock")]
        [Description("Locks the current channel, preventing any new messages. See also: unlock")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequireBotPermissions(Permissions.ManageChannels)]
        public async Task LockdownCommand(
            CommandContext ctx,
            [RemainingText, Description("The time and reason for the lockdown. For example '3h' or '3h spam'. Default is permanent with no reason.")] string timeAndReason = ""
        )
        {
            bool timeParsed = false;
            TimeSpan? lockDuration = null;
            string reason = "";

            if (timeAndReason != "")
            {
                string possibleTime = timeAndReason.Split(' ').First();
                try
                {
                    lockDuration = HumanDateParser.HumanDateParser.Parse(possibleTime).Subtract(ctx.Message.Timestamp.DateTime);
                    timeParsed = true;
                }
                catch
                {
                    // keep null
                }

                reason = timeAndReason;

                if (timeParsed)
                {
                    int i = reason.IndexOf(" ") + 1;

                    if (i == 0)
                        reason = "";
                    else
                        reason = reason[i..];
                }
            }

            var currentChannel = ctx.Channel;
            if (!Program.cfgjson.LockdownEnabledChannels.Contains(currentChannel.Id))
            {
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} You can't lock or unlock this channel!\nIf this is in error, add its ID (`{currentChannel.Id}`) to the lockdown whitelist.");
                return;
            }

            if (ongoingLockdown)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} A mass lockdown or unlock is already ongoing. Refusing your request to avoid conflicts, sorry.");
                return;
            }

            if (timeAndReason == "all")
            {
                ongoingLockdown = true;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it, please hold...");
                foreach (var chanID in Program.cfgjson.LockdownEnabledChannels)
                {
                    try
                    {
                        var channel = await ctx.Client.GetChannelAsync(chanID);
                        await LockChannelAsync(channel: channel, reason: reason);
                    }
                    catch
                    {

                    }

                }
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Done!");
                ongoingLockdown = false;
                return;

            }

            await ctx.Message.DeleteAsync();

            await LockChannelAsync(channel: currentChannel, duration: lockDuration, reason: reason);
        }

        public static async Task<bool> UnlockChannel(DiscordChannel discordChannel, DiscordMember discordMember)
        {
            bool success = false;
            var permissions = discordChannel.PermissionOverwrites.ToArray();
            foreach (var permission in permissions)
            {
                if (permission.Type == OverwriteType.Role)
                {
                    var role = await permission.GetRoleAsync();
                    if (
                        (role == discordChannel.Guild.EveryoneRole
                        && permission.Denied == Permissions.SendMessages)
                        ||
                        (role == discordChannel.Guild.GetRole(Program.cfgjson.ModRole)
                        && permission.Allowed == Permissions.SendMessages
                        )
                        )
                    {
                        success = true;
                        await permission.DeleteAsync();
                    }
                }
                else
                {
                    var member = await permission.GetMemberAsync();
                    if ((member == discordMember || member == discordChannel.Guild.CurrentMember) && permission.Allowed == Permissions.SendMessages)
                    {
                        success = true;
                        await permission.DeleteAsync();
                    }

                }
            }
            if (success)
            {
                await Program.db.HashDeleteAsync("unlocks", discordChannel.Id);
                await discordChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Unlock} This channel has been unlocked!");
            }
            else
            {
                await discordChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} This channel is not locked, or unlock failed.");
            }
            return success;
        }

        public static async Task<bool> CheckUnlocksAsync()
        {
            var channelUnlocks = await Program.db.HashGetAllAsync("unlocks");
            var success = false;

            foreach (var channelUnlock in channelUnlocks)
            {
                long unixExpiration = (long)channelUnlock.Value;
                long currentUnixTime = ModCmds.ToUnixTimestamp(DateTime.Now);
                if (currentUnixTime >= unixExpiration)
                {
                    var channel = await Program.discord.GetChannelAsync((ulong)channelUnlock.Name);
                    var currentMember = await channel.Guild.GetMemberAsync(Program.discord.CurrentUser.Id);
                    await UnlockChannel(channel, currentMember);
                    success = true;
                }
            }

            return success;
        }

        [Command("unlock")]
        [Description("Unlocks a previously locked channel. See also: lockdown")]
        [Aliases("unlockdown"), HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequireBotPermissions(Permissions.ManageChannels)]
        public async Task UnlockCommand(CommandContext ctx, [RemainingText] string reason = "")
        {
            var currentChannel = ctx.Channel;
            if (!Program.cfgjson.LockdownEnabledChannels.Contains(currentChannel.Id))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Denied} You can't lock or unlock this channel!\nIf this is in error, add its ID (`{currentChannel.Id}`) to the lockdown whitelist.");
                return;
            }

            if (ongoingLockdown)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} A mass lockdown or unlock is already ongoing. Refusing your request. sorry.");
                return;
            }

            if (reason == "all")
            {
                ongoingLockdown = true;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it, please hold...");
                foreach (var chanID in Program.cfgjson.LockdownEnabledChannels)
                {
                    try
                    {
                        currentChannel = await ctx.Client.GetChannelAsync(chanID);
                        await UnlockChannel(currentChannel, ctx.Member);
                    }
                    catch
                    {

                    }
                }
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Done!");
                ongoingLockdown = false;
                return;
            }
            await UnlockChannel(currentChannel, ctx.Member);
        }

    }
}
