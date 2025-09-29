namespace Cliptok.Commands
{
    public class LockdownCmds
    {
        public static bool ongoingLockdown = false;

        [Command("lockdown")]
        [Description("Lock the current channel or all channels in the server, preventing new messages. See also: unlock")]
        [TextAlias("lock")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions([DiscordPermission.ManageChannels], [])]
        public class LockdownCmd
        {
            [DefaultGroupCommand]
            [Command("channel")]
            [Description("Lock the current channel. See also: unlock channel")]
            public async Task LockdownChannelCommand(
                CommandContext ctx,
                [Parameter("reason"), Description("The reason for the lockdown.")] string reason = "No reason specified.",
                [Parameter("time"), Description("The length of time to lock the channel for.")] string time = null,
                [Parameter("lockthreads"), Description("Whether to lock this channel's threads. Disables sending messages, but does not archive them.")] bool lockThreads = false)
            {
                if (ctx is SlashCommandContext)
                    await ctx.As<SlashCommandContext>().DeferResponseAsync(ephemeral: true);

                if (ctx.Channel.Type is DiscordChannelType.PublicThread or DiscordChannelType.PrivateThread or DiscordChannelType.NewsThread)
                {
                    if (lockThreads)
                    {
                        if (ctx is SlashCommandContext)
                            await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Denied} You can't lock this channel!\n`/lockdown` with `lockthreads` cannot be used inside of a thread. If you meant to lock {ctx.Channel.Parent.Mention} and all of its threads, use the command there.\n\nIf you meant to only lock this thread, use `!lock` instead, or use `/lockdown` with `lockthreads` set to False.").AsEphemeral(true));
                        else
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Denied} You can't lock this channel!\n`/lockdown` with `lockthreads` cannot be used inside of a thread. If you meant to lock {ctx.Channel.Parent.Mention} and all of its threads, use the command there.\n\nIf you meant to only lock this thread, use `!lock` instead, or use `/lockdown` with `lockthreads` set to False.");
                        return;
                    }

                    var thread = (DiscordThreadChannel)ctx.Channel;

                    await thread.ModifyAsync(a =>
                    {
                        a.IsArchived = true;
                        a.Locked = true;
                    });

                    if (ctx is SlashCommandContext)
                        await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Thread locked successfully!").AsEphemeral(true));
                    return;
                }

                TimeSpan? lockDuration = null;

                if (!string.IsNullOrWhiteSpace(time))
                {
                    if (ctx is SlashCommandContext)
                        lockDuration = HumanDateParser.HumanDateParser.Parse(time).ToUniversalTime().Subtract(ctx.As<SlashCommandContext>().Interaction.CreationTimestamp.DateTime);
                    else
                        lockDuration = HumanDateParser.HumanDateParser.Parse(time).ToUniversalTime().Subtract(ctx.As<TextCommandContext>().Message.Timestamp.DateTime);
                }

                var currentChannel = ctx.Channel;
                if (!Program.cfgjson.LockdownEnabledChannels.Contains(currentChannel.Id))
                {
                    if (ctx is SlashCommandContext)
                        ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Denied} You can't lock or unlock this channel!\nIf this is in error, add its ID (`{currentChannel.Id}`) to the lockdown whitelist."));
                    else
                        ctx.RespondAsync($"{Program.cfgjson.Emoji.Denied} You can't lock or unlock this channel!\nIf this is in error, add its ID (`{currentChannel.Id}`) to the lockdown whitelist.");
                    return;
                }

                if (ongoingLockdown)
                {
                    if (ctx is SlashCommandContext)
                        await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} A mass lockdown or unlock is already ongoing. Refusing your request to avoid conflicts, sorry."));
                    else
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} A mass lockdown or unlock is already ongoing. Refusing your request to avoid conflicts, sorry.");
                    return;
                }

                if (ctx is TextCommandContext)
                    await ctx.As<TextCommandContext>().Message.DeleteAsync();

                try
                {
                    await LockdownHelpers.LockChannelAsync(user: ctx.User, channel: currentChannel, duration: lockDuration, reason: reason, lockThreads: lockThreads);
                    if (ctx is SlashCommandContext)
                        await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Channel locked successfully.").AsEphemeral(true));
                }
                catch (ArgumentException)
                {
                    if (ctx is SlashCommandContext)
                        await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Failed to lock this channel!").AsEphemeral(true));
                }
            }

            [Command("all")]
            [Description("Lock all lockable channels in the server. See also: unlock all")]
            public async Task LockdownAllCommand(
                CommandContext ctx,
                [Parameter("reason"), Description("The reason for the lockdown.")] string reason = "",
                [Parameter("time"), Description("The length of time to lock the channels for.")] string time = null,
                [Parameter("lockthreads"), Description("Whether to lock threads. Disables sending messages, but does not archive them.")] bool lockThreads = false)
            {
                if (ctx is SlashCommandContext)
                    await ctx.DeferResponseAsync();

                ongoingLockdown = true;
                if (ctx is SlashCommandContext)
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Loading} Working on it, please hold..."));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it, please hold...");

                TimeSpan? lockDuration = null;

                if (!string.IsNullOrWhiteSpace(time))
                {
                    if (ctx is SlashCommandContext)
                        lockDuration = HumanDateParser.HumanDateParser.Parse(time).ToUniversalTime().Subtract(ctx.As<SlashCommandContext>().Interaction.CreationTimestamp.DateTime);
                    else
                        lockDuration = HumanDateParser.HumanDateParser.Parse(time).ToUniversalTime().Subtract(ctx.As<TextCommandContext>().Message.Timestamp.DateTime);
                }

                foreach (var chanID in Program.cfgjson.LockdownEnabledChannels)
                {
                    try
                    {
                        var channel = await ctx.Client.GetChannelAsync(chanID);
                        await LockdownHelpers.LockChannelAsync(user: ctx.User, channel: channel, duration: lockDuration, reason: reason, lockThreads: lockThreads);
                    }
                    catch
                    {

                    }

                }
                if (ctx is SlashCommandContext)
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Done!"));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Done!");
                ongoingLockdown = false;
                return;
            }
        }

        [Command("unlock")]
        [TextAlias("unlockdown")]
        [Description("Unlock the current channel or all channels in the server, allowing new messages. See also: lockdown")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions([DiscordPermission.ManageChannels], [])]
        public class UnlockCmds
        {
            [DefaultGroupCommand]
            [Command("channel")]
            [Description("Unlock the current channel. See also: lockdown")]
            public async Task UnlockChannelCommand(CommandContext ctx, [Parameter("reason"), Description("The reason for the unlock.")] string reason = "")
            {
                if (ctx is SlashCommandContext)
                    await ctx.As<SlashCommandContext>().DeferResponseAsync(ephemeral: true);

                var currentChannel = ctx.Channel;
                if (!Program.cfgjson.LockdownEnabledChannels.Contains(currentChannel.Id))
                {
                    if (ctx is SlashCommandContext)
                        await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Denied} You can't lock or unlock this channel!\nIf this is in error, add its ID (`{currentChannel.Id}`) to the lockdown whitelist.").AsEphemeral(true));
                    else
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Denied} You can't lock or unlock this channel!\nIf this is in error, add its ID (`{currentChannel.Id}`) to the lockdown whitelist.");
                    return;
                }

                if (ongoingLockdown)
                {
                    if (ctx is SlashCommandContext)
                        await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} A mass lockdown or unlock is already ongoing. Refusing your request. sorry.").AsEphemeral(true));
                    else
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} A mass lockdown or unlock is already ongoing. Refusing your request. sorry.");
                    return;
                }
                try
                {
                    await LockdownHelpers.UnlockChannel(currentChannel, ctx.Member);
                    if (ctx is SlashCommandContext)
                        await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Channel locked successfully.").AsEphemeral(true));
                }
                catch (ArgumentException)
                {
                    if (ctx is SlashCommandContext)
                        await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Failed to lock this channel!").AsEphemeral(true));
                }
            }

            [Command("all")]
            [Description("Unlock all lockable channels in the server. See also: lockdown all")]
            public async Task UnlockAllCommand(CommandContext ctx, [Parameter("reason"), Description("The reason for the unlock.")] string reason = "")
            {
                if (ctx is SlashCommandContext)
                    ctx.DeferResponseAsync();

                ongoingLockdown = true;
                if (ctx is SlashCommandContext)
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Loading} Working on it, please hold..."));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it, please hold...");
                foreach (var chanID in Program.cfgjson.LockdownEnabledChannels)
                {
                    try
                    {
                        var currentChannel = await ctx.Client.GetChannelAsync(chanID);
                        await LockdownHelpers.UnlockChannel(currentChannel, ctx.Member, reason, true);
                    }
                    catch
                    {

                    }
                }
                if (ctx is SlashCommandContext)
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Done!"));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Done!");
                ongoingLockdown = false;
                return;
            }
        }
    }
}