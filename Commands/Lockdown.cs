namespace Cliptok.Commands
{
    class Lockdown : BaseCommandModule
    {
        public bool ongoingLockdown = false;

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
                        await LockdownHelpers.LockChannelAsync(channel: channel, reason: reason);
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

            await LockdownHelpers.LockChannelAsync(channel: currentChannel, duration: lockDuration, reason: reason);
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
                        await LockdownHelpers.UnlockChannel(currentChannel, ctx.Member);
                    }
                    catch
                    {

                    }
                }
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Done!");
                ongoingLockdown = false;
                return;
            }
            await LockdownHelpers.UnlockChannel(currentChannel, ctx.Member);
        }

    }
}
