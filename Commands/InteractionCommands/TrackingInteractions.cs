namespace Cliptok.Commands.InteractionCommands
{
    internal class TrackingInteractions : ApplicationCommandModule
    {
        [SlashCommandGroup("tracking", "Commands to manage message tracking of users", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), SlashCommandPermissions(permissions: DiscordPermission.ModerateMembers)]
        public class TrackingSlashCommands
        {
            [SlashCommand("add", "Track a users messages.")]
            public async Task TrackingAddSlashCmd(InteractionContext ctx, [Option("member", "The member to track.")] DiscordUser discordUser, [Option("channels", "Optional channels to filter to. Use IDs or mentions, and separate with commas or spaces.")] string channels = "")
            {
                await ctx.DeferAsync(ephemeral: false);
                
                var channelsUpdated = false;
                
                // Resolve list of filter channels
                List<ulong> filterChannels = new();
                if (!string.IsNullOrEmpty(channels))
                {
                    channels = Regex.Replace(channels, ", +", ",").Trim(); // "#general-chat, #lounge" ~> "#general-chat,#lounge" & trim
                    var channelIds = channels.Split(' ', ',');
                    foreach (var channel in channelIds)
                    {
                        // skip some common obviously-invalid entries
                        if (channel == "" || channel == " ")
                            continue;
   
                        // If this is a channel mention, get the ID first
                        var channelId = channel.Replace("<#", "").Replace(">", "");
                        
                        if (ulong.TryParse(channelId, out var id))
                        {
                            if (!filterChannels.Contains(id))
                                filterChannels.Add(id);
                        }
                        else
                        {
                            // Invalid ID; couldn't parse as ulong
                            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I couldn't parse \"{channel}\" as a channel ID or mention! Please double-check it and try again."));
                            return;
                        }
                    }
                }
                
                // If we were passed nothing, filterChannels remains an empty List. Otherwise, it is populated with the parsed channel IDs
                
                // Compare to db; if there is a mismatch, replace whatever is already in the db with what was passed to this command
                if (Program.db.HashExists("trackingChannels", discordUser.Id))
                {
                    var dbChannels = Program.db.HashGet("trackingChannels", discordUser.Id).ToString();
                    string cmdChannels;
                    if (filterChannels.Count < 1)
                    {
                        // No channels were passed. If there are any in the db, remove them
                        if (await Program.db.HashExistsAsync("trackingChannels", discordUser.Id))
                        {
                            await Program.db.HashDeleteAsync("trackingChannels", discordUser.Id);
                            channelsUpdated = true;
                        }
                    }
                    else
                    {
                        cmdChannels = JsonConvert.SerializeObject(filterChannels);
                        if (dbChannels != cmdChannels)
                        {
                            // Passed channels do not match db channels, update db
                            var newChannels = JsonConvert.SerializeObject(filterChannels);
                            await Program.db.HashSetAsync("trackingChannels", discordUser.Id, newChannels);
                            channelsUpdated = true;
                        }
                    }
                }
                else
                {
                    // No channels in db; just add whatever was passed
                    // If nothing was passed, don't add anything
                    if (filterChannels.Count > 0)
                    {
                        var newChannels = JsonConvert.SerializeObject(filterChannels); 
                        await Program.db.HashSetAsync("trackingChannels", discordUser.Id, newChannels);
                        channelsUpdated = true;
                    }
                }

                if (Program.db.SetContains("trackedUsers", discordUser.Id))
                {
                    if (channelsUpdated)
                    {
                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Successfully updated tracking for {discordUser.Mention}!"));
                        return;
                    }
                    
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} This user is already tracked!"));
                    return;
                }

                await Program.db.SetAddAsync("trackedUsers", discordUser.Id);

                if (Program.db.HashExists("trackingThreads", discordUser.Id))
                {
                    var channelId = Program.db.HashGet("trackingThreads", discordUser.Id);
                    DiscordThreadChannel thread = (DiscordThreadChannel)await ctx.Client.GetChannelAsync((ulong)channelId);

                    await thread.SendMessageAsync($"{Program.cfgjson.Emoji.On} Now tracking {discordUser.Mention} in this thread! :eyes:");
                    thread.AddThreadMemberAsync(ctx.Member);
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.On} Now tracking {discordUser.Mention} in {thread.Mention}!"));

                }
                else
                {
                    var thread = await LogChannelHelper.ChannelCache["investigations"].CreateThreadAsync(DiscordHelpers.UniqueUsername(discordUser), DiscordAutoArchiveDuration.Week, DiscordChannelType.PublicThread);
                    await Program.db.HashSetAsync("trackingThreads", discordUser.Id, thread.Id);
                    await thread.SendMessageAsync($"{Program.cfgjson.Emoji.On} Now tracking {discordUser.Mention} in this thread! :eyes:");
                    await thread.AddThreadMemberAsync(ctx.Member);
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.On} Now tracking {discordUser.Mention} in {thread.Mention}!"));
                }
            }

            [SlashCommand("remove", "Stop tracking a users messages.")]
            public async Task TrackingRemoveSlashCmd(InteractionContext ctx, [Option("member", "The member to track.")] DiscordUser discordUser)
            {
                await ctx.DeferAsync(ephemeral: false);

                if (!Program.db.SetContains("trackedUsers", discordUser.Id))
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} This user is not being tracked."));
                    return;
                }

                await Program.db.SetRemoveAsync("trackedUsers", discordUser.Id);
                await Program.db.HashDeleteAsync("trackingChannels", discordUser.Id);

                var channelId = Program.db.HashGet("trackingThreads", discordUser.Id);
                DiscordThreadChannel thread = (DiscordThreadChannel)await ctx.Client.GetChannelAsync((ulong)channelId);

                await thread.SendMessageAsync($"{Program.cfgjson.Emoji.Off} {discordUser.Mention} is no longer being tracked! Archiving for now.");
                await thread.ModifyAsync(thread =>
                {
                    thread.IsArchived = true;
                });

                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Off} No longer tracking {discordUser.Mention}! Thread has been archived for now."));
            }
        }

    }
}
