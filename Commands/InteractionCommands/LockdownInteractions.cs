namespace Cliptok.Commands.InteractionCommands
{
    class LockdownInteractions : ApplicationCommandModule
    {
        public static bool ongoingLockdown = false;

        [SlashCommandGroup("lockdown", "Lock the current channel or all channels in the server, preventing new messages. See also: unlock")]
        [HomeServer, SlashRequireHomeserverPerm(ServerPermLevel.Moderator), RequireBotPermissions(DiscordPermissions.ManageChannels)]
        public class LockdownCmds
        {
            [SlashCommand("channel", "Lock the current channel. See also: unlock channel")]
            public async Task LockdownChannelCommand(
                InteractionContext ctx,
                [Option("reason", "The reason for the lockdown.")] string reason = "No reason specified.",
                [Option("time", "The length of time to lock the channel for.")] string time = null,
                [Option("lockthreads", "Whether to lock this channel's threads. Disables sending messages, but does not archive them.")] bool lockThreads = false)
            {
                await ctx.DeferAsync(ephemeral: true);

                if (ctx.Channel.Type is DiscordChannelType.PublicThread or DiscordChannelType.PrivateThread or DiscordChannelType.NewsThread)
                {
                    if (lockThreads)
                    {
                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Denied} You can't lock this channel!\n`/lockdown` with `lockthreads` cannot be used inside of a thread. If you meant to lock {ctx.Channel.Parent.Mention} and all of its threads, use the command there.\n\nIf you meant to only lock this thread, use `!lock` instead, or use `/lockdown` with `lockthreads` set to False.").AsEphemeral(true));
                        return;
                    }

                    var thread = (DiscordThreadChannel)ctx.Channel;

                    await thread.ModifyAsync(a =>
                    {
                        a.IsArchived = true;
                        a.Locked = true;
                    });

                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Thread locked successfully!").AsEphemeral(true));
                    return;
                }

                TimeSpan? lockDuration = null;

                if (!string.IsNullOrWhiteSpace(time))
                {
                    lockDuration = HumanDateParser.HumanDateParser.Parse(time).Subtract(ctx.Interaction.CreationTimestamp.LocalDateTime);
                }

                var currentChannel = ctx.Channel;
                if (!Program.cfgjson.LockdownEnabledChannels.Contains(currentChannel.Id))
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Denied} You can't lock or unlock this channel!\nIf this is in error, add its ID (`{currentChannel.Id}`) to the lockdown whitelist."));
                    return;
                }

                if (ongoingLockdown)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} A mass lockdown or unlock is already ongoing. Refusing your request to avoid conflicts, sorry."));
                    return;
                }

                bool success = await LockdownHelpers.LockChannelAsync(user: ctx.User, channel: currentChannel, duration: lockDuration, reason: reason, lockThreads: lockThreads);
                if (success)
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Channel locked successfully.").AsEphemeral(true));
                else
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Failed to lock this channel!").AsEphemeral(true));
            }

            [SlashCommand("all", "Lock all lockable channels in the server. See also: unlock all")]
            public async Task LockdownAllCommand(
                InteractionContext ctx,
                [Option("reason", "The reason for the lockdown.")] string reason = "",
                [Option("time", "The length of time to lock the channels for.")] string time = null,
                [Option("lockthreads", "Whether to lock threads. Disables sending messages, but does not archive them.")] bool lockThreads = false)
            {
                await ctx.DeferAsync();

                ongoingLockdown = true;
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Loading} Working on it, please hold..."));

                TimeSpan? lockDuration = null;

                if (!string.IsNullOrWhiteSpace(time))
                {
                    lockDuration = HumanDateParser.HumanDateParser.Parse(time).Subtract(ctx.Interaction.CreationTimestamp.LocalDateTime);
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
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Done!"));
                ongoingLockdown = false;
                return;
            }
        }

        [SlashCommandGroup("unlock", "Unlock the current channel or all channels in the server, allowing new messages. See also: lockdown")]
        [HomeServer, SlashRequireHomeserverPerm(ServerPermLevel.Moderator), RequireBotPermissions(DiscordPermissions.ManageChannels)]
        public class UnlockCmds
        {
            [SlashCommand("channel", "Unlock the current channel. See also: lockdown")]
            public async Task UnlockChannelCommand(InteractionContext ctx, [Option("reason", "The reason for the unlock.")] string reason = "")
            {
                await ctx.DeferAsync(ephemeral: true);

                var currentChannel = ctx.Channel;
                if (!Program.cfgjson.LockdownEnabledChannels.Contains(currentChannel.Id))
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Denied} You can't lock or unlock this channel!\nIf this is in error, add its ID (`{currentChannel.Id}`) to the lockdown whitelist.").AsEphemeral(true));
                    return;
                }

                if (ongoingLockdown)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} A mass lockdown or unlock is already ongoing. Refusing your request. sorry.").AsEphemeral(true));
                    return;
                }
                bool success = await LockdownHelpers.UnlockChannel(currentChannel, ctx.Member);
                if (success)
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Channel unlocked successfully.").AsEphemeral(true));
                else
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Failed to unlock this channel!").AsEphemeral(true));
            }

            [SlashCommand("all", "Unlock all lockable channels in the server. See also: lockdown all")]
            public async Task UnlockAllCommand(InteractionContext ctx, [Option("reason", "The reason for the unlock.")] string reason = "")
            {
                await ctx.CreateResponseAsync(DiscordInteractionResponseType.DeferredChannelMessageWithSource);

                ongoingLockdown = true;
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Loading} Working on it, please hold..."));
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
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Done!"));
                ongoingLockdown = false;
                return;
            }
        }
    }
}
