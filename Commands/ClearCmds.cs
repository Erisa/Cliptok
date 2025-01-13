namespace Cliptok.Commands
{
    public class ClearCmds
    {
        public static Dictionary<ulong, List<DiscordMessage>> MessagesToClear = new();

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
            [Parameter("dry_run"), Description("Don't actually delete the messages, just output what would be deleted.")] bool dryRun = false
        )
        {
            await ctx.DeferResponseAsync(ephemeral: !dryRun);

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

            // Get messages to delete, whether that's messages up to a certain one or the last 'x' number of messages.

            if (upTo != "" && count != 0)
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} You can't provide both a count of messages and a message to delete up to! Please only provide one of the two arguments.").AsEphemeral(true));
                return;
            }

            List<DiscordMessage> messagesToClear = new();
            if (upTo == "")
            {
                var messages = await ctx.Channel.GetMessagesAsync((int)count).ToListAsync();
                messagesToClear = messages.ToList();
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
                        Constants.RegexConstants.discord_link_rx.Match(upTo).Groups[2].Value != ctx.Channel.Id.ToString()
                        || !ulong.TryParse(Constants.RegexConstants.discord_link_rx.Match(upTo).Groups[3].Value, out messageId)
                    )
                    {
                        await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} Please provide a valid link to a message in this channel!").AsEphemeral(true));
                        return;
                    }
                }

                // This is the message we will delete up to. This message will not be deleted.
                message = await ctx.Channel.GetMessageAsync(messageId);

                // List of messages to delete, up to (not including) the one we just got.
                var firstMsg = (await ctx.Channel.GetMessagesAfterAsync(message.Id, 1).ToListAsync())[0];
                var firstMsgId = firstMsg.Id;
                messagesToClear.Add(firstMsg);
                while (true)
                {
                    var newMessages = (await ctx.Channel.GetMessagesAfterAsync(firstMsgId, 100).ToListAsync()).ToList();
                    messagesToClear.AddRange(newMessages);
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
                foreach (var message in messagesToClear.ToList())
                {
                    if (message.Author.Id != user.Id)
                    {
                        messagesToClear.Remove(message);
                    }
                }
            }

            // Ignore mods
            if (ignoreMods)
            {
                foreach (var message in messagesToClear.ToList())
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
                        messagesToClear.Remove(message);
                    }
                }
            }

            // Match text
            if (match != "")
            {
                foreach (var message in messagesToClear.ToList())
                {
                    if (!message.Content.ToLower().Contains(match.ToLower()))
                    {
                        messagesToClear.Remove(message);
                    }
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

                foreach (var message in messagesToClear.ToList())
                {
                    if (!message.Author.IsBot)
                    {
                        messagesToClear.Remove(message);
                    }
                }
            }

            // Humans only
            if (humansOnly)
            {
                foreach (var message in messagesToClear.ToList())
                {
                    if (message.Author.IsBot)
                    {
                        messagesToClear.Remove(message);
                    }
                }
            }

            // Attachments only
            if (attachmentsOnly)
            {
                foreach (var message in messagesToClear.ToList())
                {
                    if (message.Attachments.Count == 0)
                    {
                        messagesToClear.Remove(message);
                    }
                }
            }

            // Stickers only
            if (stickersOnly)
            {
                foreach (var message in messagesToClear.ToList())
                {
                    if (message.Stickers.Count == 0)
                    {
                        messagesToClear.Remove(message);
                    }
                }
            }

            // Links only
            if (linksOnly)
            {
                foreach (var message in messagesToClear.ToList())
                {
                    if (!Constants.RegexConstants.url_rx.IsMatch(message.Content.ToLower()))
                    {
                        messagesToClear.Remove(message);
                    }
                }
            }

            // Skip messages older than 2 weeks, since Discord won't let us delete them anyway

            bool skipped = false;
            foreach (var message in messagesToClear.ToList())
            {
                if (message.CreationTimestamp.ToUniversalTime() < DateTime.UtcNow.AddDays(-14))
                {
                    messagesToClear.Remove(message);
                    skipped = true;
                }
            }

            if (messagesToClear.Count == 0 && skipped)
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} All of the messages to delete are older than 2 weeks, so I can't delete them!").AsEphemeral(true));
                return;
            }

            // All filters checked. 'messages' is now our final list of messages to delete.

            if (dryRun)
            {
                var msg = await LogChannelHelper.CreateDumpMessageAsync($"{Program.cfgjson.Emoji.Information} **{messagesToClear.Count}** messages would have been deleted, but are instead logged below.",
                    messagesToClear,
                    ctx.Channel);
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent(msg.Content).AddFiles(msg.Files).AddEmbeds(msg.Embeds).AsEphemeral(false));
                return;
            }

            // Warn the mod if we're going to be deleting 50 or more messages.
            if (messagesToClear.Count >= 50)
            {
                DiscordButtonComponent confirmButton = new(DiscordButtonStyle.Danger, "clear-confirm-callback", "Delete Messages");
                DiscordMessage confirmationMessage = await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Muted} You're about to delete {messagesToClear.Count} messages. Are you sure?").AddComponents(confirmButton).AsEphemeral(true));

                MessagesToClear.Add(confirmationMessage.Id, messagesToClear);
            }
            else
            {
                if (messagesToClear.Count >= 1)
                {
                    await ctx.Channel.DeleteMessagesAsync(messagesToClear, $"[Clear by {DiscordHelpers.UniqueUsername(ctx.User)}]");
                    if (skipped)
                    {
                        await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Deleted} Cleared **{messagesToClear.Count}** messages from {ctx.Channel.Mention}!\nSome messages were not deleted because they are older than 2 weeks.");
                    }
                    else
                    {
                        await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Deleted} Cleared **{messagesToClear.Count}** messages from {ctx.Channel.Mention}!");
                    }
                    await LogChannelHelper.LogMessageAsync("mod",
                        new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Deleted} **{messagesToClear.Count}** messages were cleared in {ctx.Channel.Mention} by {ctx.User.Mention}.")
                            .WithAllowedMentions(Mentions.None)
                    );
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Done!").AsEphemeral(true));
                }
                else
                {
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} There were no messages that matched all of the arguments you provided! Nothing to do."));
                }

                await LogChannelHelper.LogDeletedMessagesAsync(
                    "messages",
                    $"{Program.cfgjson.Emoji.Deleted} **{messagesToClear.Count}** messages were cleared from {ctx.Channel.Mention} by {ctx.User.Mention}.",
                    messagesToClear,
                    ctx.Channel
                );

            }
        }
    }
}
