namespace Cliptok.Commands
{
    public class ClearCmds
    {
        // Outer ulong is confirmation message ID, used to keep track of the entire set of messages between /clear and the confirmation button on that message
        // Inner ulong is channel ID, holding the list of messages to clear just for the respective channel
        // so Dictionary<confirmation message ID, Dictionary<channel ID, List<DiscordMessage>>>
        public static Dictionary<ulong, Dictionary<ulong, List<DiscordMessage>>> MessagesToClear = new();

        [Command("clear")]
        [Description("Delete many messages from the current channel.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ManageMessages, DiscordPermission.ModerateMembers)]
        public async Task ClearSlashCommand(SlashCommandContext ctx,
            [Parameter("count"), Description("The number of messages to consider for deletion. Required if you don't use the 'up_to' argument.")] long count = 0,
            [Parameter("up_to"), Description("Optionally delete messages up to (not including) this one. Accepts IDs and links.")] string upTo = "",
            [Parameter("user"), Description("Optionally filter the deletion to a specific user.")] DiscordUser user = default,
            [Parameter("ignore_mods"), Description("Optionally filter the deletion to only messages sent by users who are not Moderators.")] bool ignoreMods = false,
            [Parameter("match"), Description("Optionally filter the deletion to only messages containing certain text.")] string match = "",
            [Parameter("bots_only"), Description("Optionally filter the deletion to only bots.")] bool botsOnly = false,
            [Parameter("humans_only"), Description("Optionally filter the deletion to only humans.")] bool humansOnly = false,
            [Parameter("attachments_only"), Description("Optionally filter the deletion to only messages with attachments.")] bool attachmentsOnly = false,
            [Parameter("stickers_only"), Description("Optionally filter the deletion to only messages with stickers.")] bool stickersOnly = false,
            [Parameter("links_only"), Description("Optionally filter the deletion to only messages containing links.")] bool linksOnly = false,
            [Parameter("dry_run"), Description("Don't actually delete the messages, just output what would be deleted.")] bool dryRun = false,
            [Parameter("channel"), Description("Choose a specific channel to clear from. Defaults to the current channel.")] DiscordChannel channel = default,
            [Parameter("multi_channel"), Description("Optionally clear across all channels.")] bool multiChannel = false
        )
        {
            await ctx.DeferResponseAsync(ephemeral: !dryRun);

            if (channel is null)
                channel = ctx.Channel;

            // If all args are unset
            if (count == 0 && upTo == "" && user == default && ignoreMods == false && match == "" && botsOnly == false && humansOnly == false && attachmentsOnly == false && stickersOnly == false && linksOnly == false)
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} You must provide at least one argument! I need to know which messages to delete.").AsEphemeral(true));
                return;
            }

            if (count == 0 && upTo == "")
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I need to know how many messages to delete! Please provide a value for `count` or `up_to`.").AsEphemeral(true));
                return;
            }

            // If count is too low or too high, refuse the request

            if (count < 0)
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I can't delete a negative number of messages! Try setting `count` to a positive number.").AsEphemeral(true));
                return;
            }

            if (count >= 1000)
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} Deleting that many messages poses a risk of something disastrous happening, so I'm refusing your request, sorry.").AsEphemeral(true));
                return;
            }

            // If multi-channel, limit msg count per channel to 100 to try to limit API requests...

            if (multiChannel && upTo != "")
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} Sorry, but if you are using `multi_channel`, you can only use `count`, and the count cannot be greater than 100.").AsEphemeral(true));
                return;
            }

            if (multiChannel && count > 100)
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} Sorry, but if you are using `multi_channel`, `count` cannot be greater than 100!").AsEphemeral(true));
                return;
            }

            // Get messages to delete, whether that's messages up to a certain one or the last 'x' number of messages.

            if (upTo != "" && count != 0)
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} You can't provide both a count of messages and a message to delete up to! Please only provide one of the two arguments.").AsEphemeral(true));
                return;
            }

            await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Loading} Looking for messages that match your filters. This may take a while..."));

            Dictionary<ulong, List<DiscordMessage>> messagesToClear = new();
            if (upTo == "")
            {
                List<DiscordChannel> channelsToSearch;
                if (multiChannel)
                {
                    var clearChannelIds = Program.cfgjson.PublicFacingChannels is not null
                        ? Program.cfgjson.PublicFacingChannels
                        : Program.cfgjson.LockdownEnabledChannels;

                    channelsToSearch = ctx.Guild.Channels.Values.Where(x => clearChannelIds.Contains(x.Id)).ToList();
                }
                else
                {
                    channelsToSearch = [channel];
                }

                foreach (var c in channelsToSearch)
                {
                    messagesToClear.Add(c.Id, await c.GetMessagesAsync((int)count).ToListAsync());
                }
            }
            else
            {
                DiscordMessage message;
                ulong messageId;
                if (!upTo.Contains("discord.com"))
                {
                    if (!ulong.TryParse(upTo, out messageId))
                    {
                        await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} That doesn't look like a valid message ID or link! Please try again."));
                        return;
                    }
                }
                else
                {
                    if (
                        Constants.RegexConstants.discord_link_rx.Match(upTo).Groups[2].Value != channel.Id.ToString()
                        || !ulong.TryParse(Constants.RegexConstants.discord_link_rx.Match(upTo).Groups[3].Value, out messageId)
                    )
                    {
                        await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} Please provide a valid link to a message in {channel.Mention}!").AsEphemeral(true));
                        return;
                    }
                }

                // This is the message we will delete up to. This message will not be deleted.
                try
                {
                    message = await channel.GetMessageAsync(messageId, skipCache: true);
                }
                catch
                {
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I couldn't fetch the message you provided for `up_to`! Please provide a valid message link or ID.").AsEphemeral(true));
                    return;
                }

                // List of messages to delete, up to (not including) the one we just got.
                var firstMsg = (await channel.GetMessagesAfterAsync(message.Id, 1).ToListAsync()).FirstOrDefault();
                if (firstMsg is null)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find any messages to clear! Please try again. (Hint: `up_to` is NOT inclusive.)");
                    return;
                }
                var firstMsgId = firstMsg.Id;
                AddToMessageList(messagesToClear, firstMsg);
                while (true)
                {
                    var newMessages = (await channel.GetMessagesAfterAsync(firstMsgId, 100).ToListAsync()).OrderByDescending(x => x.Id).ToList();
                    if (newMessages.Count == 0)
                        break;
                    AddToMessageList(messagesToClear, newMessages);
                    firstMsgId = newMessages.First().Id;
                    if (newMessages.Count() < 100)
                        break;
                }
            }

            // Now we know how many messages we'll be looking through and we won't be refusing the request. Time to check filters.
            // Order of priority here is the order of the arguments for the command.

            // Match user
            if (user != default)
            {
                foreach (var messagesForChannel in messagesToClear)
                {
                    messagesToClear[messagesForChannel.Key].RemoveAll(message => message.Author.Id != user.Id);
                }
            }

            // Ignore mods
            if (ignoreMods)
            {
                foreach (var messagesForChannel in messagesToClear)
                {
                    foreach (var message in messagesForChannel.Value)
                    {
                        DiscordMember member;
                        try
                        {
                            member = await ctx.Guild.GetMemberAsync(message.Author.Id);
                        }
                        catch (DSharpPlus.Exceptions.NotFoundException)
                        {
                            // User is not in the server, so they can't be a current mod
                            continue;
                        }

                        if ((await GetPermLevelAsync(member)) >= ServerPermLevel.TrialModerator)
                        {
                            messagesToClear[messagesForChannel.Key].Remove(message);
                        }
                    }
                }
            }

            // Match text
            if (match != "")
            {
                foreach (var messagesForChannel in messagesToClear)
                {
                    messagesToClear[messagesForChannel.Key].RemoveAll(message => !message.Content.ToLower().Contains(match.ToLower()));
                }
            }

            // Bots only
            if (botsOnly)
            {
                if (humansOnly)
                {
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} You can't use `bots_only` and `humans_only` together! Pick one or the other please.").AsEphemeral(true));
                    return;
                }

                foreach (var messagesForChannel in messagesToClear)
                {
                    messagesToClear[messagesForChannel.Key].RemoveAll(message => !message.Author.IsBot);
                }
            }

            // Humans only
            if (humansOnly)
            {
                foreach (var messagesForChannel in messagesToClear)
                {
                    messagesToClear[messagesForChannel.Key].RemoveAll(message => message.Author.IsBot);
                }
            }

            // Attachments only
            if (attachmentsOnly)
            {
                foreach (var messagesForChannel in messagesToClear)
                {
                    messagesToClear[messagesForChannel.Key].RemoveAll(message => message.Attachments.Count == 0);
                }
            }

            // Stickers only
            if (stickersOnly)
            {
                foreach (var messagesForChannel in messagesToClear)
                {
                    messagesToClear[messagesForChannel.Key].RemoveAll(message => message.Stickers.Count == 0);
                }
            }

            // Links only
            if (linksOnly)
            {
                foreach (var messagesForChannel in messagesToClear)
                {
                    messagesToClear[messagesForChannel.Key].RemoveAll(message => !Constants.RegexConstants.domain_rx.IsMatch(message.Content.ToLower()));
                }
            }

            // Skip messages older than 2 weeks, since Discord won't let us delete them anyway
            foreach (var messagesForChannel in messagesToClear)
            {
                messagesToClear[messagesForChannel.Key].RemoveAll(message => message.CreationTimestamp.ToUniversalTime() < DateTime.UtcNow.AddDays(-14));
            }

            // Clean up a bit
            foreach (var messagesForChannel in messagesToClear)
            {
                if (messagesForChannel.Value.Count == 0)
                    messagesToClear.Remove(messagesForChannel.Key);
            }

            if (messagesToClear.Count == 0)
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} All of the messages to delete are older than 2 weeks, so I can't delete them!").AsEphemeral(true));
                return;
            }

            // All filters checked. 'messagesToClear' is now our final list of messages to delete, by channel

            if (dryRun)
            {
                var msg = (await LogChannelHelper.CreateDumpMessageAsync($"{Program.cfgjson.Emoji.Information} **{messagesToClear.Count}** messages would have been deleted, but are instead logged below.",
                    messagesToClear.Values.SelectMany(x => x).ToList(), // pull all of the messages out of the dict
                    channel)).messageBuilder;
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent(msg.Content).AddFiles(msg.Files).AddEmbeds(msg.Embeds).AsEphemeral(false));
                return;
            }

            // Warn the mod if we're going to be deleting 50 or more messages.
            if (messagesToClear.Values.Sum(x => x.Count) >= 50)
            {
                DiscordButtonComponent confirmButton = new(DiscordButtonStyle.Danger, "clear-confirm-callback", "Delete Messages");
                DiscordMessage confirmationMessage = await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Muted} You're about to delete {messagesToClear.Values.Sum(x => x.Count)} messages. Are you sure?").AddActionRowComponent(confirmButton));

                MessagesToClear.Add(confirmationMessage.Id, messagesToClear);
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Loading} Deleting messages. This may take a while..."));

                if (messagesToClear.Count >= 1)
                {
                    foreach (var messagesForChannel in messagesToClear)
                    {
                        channel = ctx.Guild.Channels[messagesForChannel.Key];
                        await channel.DeleteMessagesAsync(messagesForChannel.Value, $"[Clear by {DiscordHelpers.UniqueUsername(ctx.User)}]");
                        if (messagesToClear.Count == 1)
                            await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Deleted} Cleared **{messagesForChannel.Value.Count}** messages from {channel.Mention}!");
                        await LogChannelHelper.LogMessageAsync("mod",
                            new DiscordMessageBuilder()
                                .WithContent($"{Program.cfgjson.Emoji.Deleted} **{messagesForChannel.Value.Count}** messages were cleared in {channel.Mention} by {ctx.User.Mention}.")
                                .WithAllowedMentions(Mentions.None)
                        );

                        // logging is now handled in the bulk delete event
                        if (!Program.cfgjson.EnablePersistentDb)
                        {
                            await LogChannelHelper.LogDeletedMessagesAsync(
                                "messages",
                                $"{Program.cfgjson.Emoji.Deleted} **{messagesForChannel.Value.Count}** messages were cleared from {channel.Mention} by {ctx.User.Mention}.",
                                messagesForChannel.Value,
                                channel
                            );
                        }
                    }
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Done!"));
                }
                else
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} There were no messages that matched all of the arguments you provided! Nothing to do."));
                }
            }
        }

        private static Dictionary<ulong, List<DiscordMessage>> AddToMessageList(Dictionary<ulong, List<DiscordMessage>> messagesToClear, DiscordMessage messageToAdd)
        {
            return AddToMessageList(messagesToClear, [messageToAdd]);
        }

        private static Dictionary<ulong, List<DiscordMessage>> AddToMessageList(Dictionary<ulong, List<DiscordMessage>> messagesToClear, List<DiscordMessage> messagesToAdd)
        {
            var channelId = messagesToAdd.First().Channel.Id;
            if (messagesToClear.ContainsKey(channelId))
                messagesToClear[channelId].AddRange(messagesToAdd);
            else
                messagesToClear.Add(channelId, messagesToAdd);

            return messagesToClear;
        }
    }
}
