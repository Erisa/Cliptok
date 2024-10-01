﻿using System.Runtime.CompilerServices;

namespace Cliptok.Commands.InteractionCommands
{
    internal class TrackingInteractions
    {
        [Command("tracking")]
        [Description("Commands to manage message tracking of users")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermissions.ModerateMembers)]
        public class PermadehoistSlashCommands
        {
            [Command("add")]
			[Description("Track a users messages.")]
            public async Task TrackingAddSlashCmd(SlashCommandContext ctx, [Parameter("member"), Description("The member to track.")] DiscordUser discordUser)
            {
                await ctx.DeferResponseAsync(ephemeral: false);

                if (Program.db.SetContains("trackedUsers", discordUser.Id))
                {
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} This user is already tracked!"));
                    return;
                }

                await Program.db.SetAddAsync("trackedUsers", discordUser.Id);

                if (Program.db.HashExists("trackingThreads", discordUser.Id))
                {
                    var channelId = Program.db.HashGet("trackingThreads", discordUser.Id);
                    DiscordThreadChannel thread = (DiscordThreadChannel)await ctx.Client.GetChannelAsync((ulong)channelId);

                    await thread.SendMessageAsync($"{Program.cfgjson.Emoji.On} Now tracking {discordUser.Mention} in this thread! :eyes:");
                    thread.AddThreadMemberAsync(ctx.Member);
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.On} Now tracking {discordUser.Mention} in {thread.Mention}!"));

                }
                else
                {
                    var thread = await LogChannelHelper.ChannelCache["investigations"].CreateThreadAsync(DiscordHelpers.UniqueUsername(discordUser), DiscordAutoArchiveDuration.Week, DiscordChannelType.PublicThread);
                    await Program.db.HashSetAsync("trackingThreads", discordUser.Id, thread.Id);
                    await thread.SendMessageAsync($"{Program.cfgjson.Emoji.On} Now tracking {discordUser.Mention} in this thread! :eyes:");
                    await thread.AddThreadMemberAsync(ctx.Member);
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.On} Now tracking {discordUser.Mention} in {thread.Mention}!"));
                }
            }

            [Command("remove")]
			[Description("Stop tracking a users messages.")]
            public async Task TrackingRemoveSlashCmd(SlashCommandContext ctx, [Parameter("member"), Description("The member to track.")] DiscordUser discordUser)
            {
                await ctx.DeferResponseAsync(ephemeral: false);

                if (!Program.db.SetContains("trackedUsers", discordUser.Id))
                {
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} This user is not being tracked."));
                    return;
                }

                await Program.db.SetRemoveAsync("trackedUsers", discordUser.Id);

                var channelId = Program.db.HashGet("trackingThreads", discordUser.Id);
                DiscordThreadChannel thread = (DiscordThreadChannel)await ctx.Client.GetChannelAsync((ulong)channelId);

                await thread.SendMessageAsync($"{Program.cfgjson.Emoji.Off} {discordUser.Mention} is no longer being tracked! Archiving for now.");
                await thread.ModifyAsync(thread =>
                {
                    thread.IsArchived = true;
                });

                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Off} No longer tracking {discordUser.Mention}! Thread has been archived for now."));
            }
        }

    }
}
