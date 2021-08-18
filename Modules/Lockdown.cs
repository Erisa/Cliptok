using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using System.Linq;
using System.Threading.Tasks;


namespace Cliptok.Modules
{
    class Lockdown : BaseCommandModule
    {
        public bool ongoingLockdown = false;

        [Command("lockdown")]
        [Aliases("lock")]
        [Description("Locks the current channel, preventing any new messages. See also: unlock")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod), RequireBotPermissions(Permissions.ManageChannels)]
        public async Task LockdownCommand(CommandContext ctx, [RemainingText] string reason = "")
        {
            var currentChannel = ctx.Channel;
            if (!Program.cfgjson.LockdownEnabledChannels.Contains(currentChannel.Id))
            {
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} You can't lock or unlock this channel!\nIf this is in error, add its ID (`{currentChannel.Id}`) to the lockdown whitelist.");
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
                        var channel = await ctx.Client.GetChannelAsync(chanID);
                        await channel.AddOverwriteAsync(ctx.Guild.CurrentMember, Permissions.SendMessages, Permissions.None, "Failsafe 1 for Lockdown: ");
                        await channel.AddOverwriteAsync(ctx.Guild.GetRole(Program.cfgjson.ModRole), Permissions.SendMessages, Permissions.None, "Failsafe 2 for Lockdown");
                        await channel.AddOverwriteAsync(ctx.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages, "Lockdown command");
                        await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Locked} This channel has been locked by a Moderator.");
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

            await currentChannel.AddOverwriteAsync(ctx.Guild.CurrentMember, Permissions.SendMessages, Permissions.None, "Failsafe 1 for Lockdown");
            await currentChannel.AddOverwriteAsync(ctx.Guild.GetRole(Program.cfgjson.ModRole), Permissions.SendMessages, Permissions.None, "Failsafe 2 for Lockdown");
            await currentChannel.AddOverwriteAsync(ctx.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages, "Lockdown command");

            if (reason == "")
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Locked} This channel has been locked by a Moderator.");
            else
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Locked} This channel has been locked: **{reason}**");
            ;
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
                await discordChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Unlock} This channel has been unlocked!");
            }
            else
            {
                await discordChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} This channel is not locked.");
            }
            return success;
        }

        [Command("unlock")]
        [Description("Unlocks a previously locked channel. See also: lockdown")]
        [Aliases("unlockdown"), HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod), RequireBotPermissions(Permissions.ManageChannels)]
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
