namespace Cliptok.Commands.InteractionCommands
{
    public class ClearInteractions : ApplicationCommandModule
    {
        public static Dictionary<ulong, List<DiscordMessage>> MessagesToClear = new();

        [SlashCommand("clear", "Delete many messages from the current channel.", defaultPermission: false)]
        [HomeServer, SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), RequireBotPermissions(Permissions.ManageMessages), SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task ClearSlashCommand(InteractionContext ctx,
            [Option("count", "The number of messages to delete. Required if you don't use the 'upto' argument.")] long count = 0,
            [Option("up_to", "Optionally delete messages up to (not including) this one. Accepts IDs and links.")] string upTo = "",
            [Option("user", "Optionally filter the deletion to a specific user.")] DiscordUser user = default,
            [Option("ignore_mods", "Optionally filter the deletion to only messages sent by users who are not Moderators.")] bool ignoreMods = false,
            [Option("match", "Optionally filter the deletion to only messages containing certain text.")] string match = "",
            [Option("bots_only", "Optionally filter the deletion to only bots.")] bool botsOnly = false,
            [Option("humans_only", "Optionally filter the deletion to only humans.")] bool humansOnly = false,
            [Option("images_only", "Optionally filter the deletion to only messages containing images.")] bool imagesOnly = false,
            [Option("links_only", "Optionally filter the deletion to only messages containing links.")] bool linksOnly = false
        )
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource, new DiscordInteractionResponseBuilder().AsEphemeral(true));

            // If all args are unset
            if (count == 0 && upTo == "" && user == default && ignoreMods == false && match == "" && botsOnly == false && humansOnly == false && imagesOnly == false && linksOnly == false)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} You must provide at least one argument! I need to know which messages to delete.").AsEphemeral(true));
                return;
            }

            // If count is too low or too high, refuse the request

            if (count < 0)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I can't delete a negative number of messages! Try setting `count` to a positive number.").AsEphemeral(true));
            }

            if (count >= 1000)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} Deleting that many messages poses a risk of something disastrous happening, so I'm refusing your request, sorry.").AsEphemeral(true));
                return;
            }

            // Get messages to delete, whether that's messages up to a certain one or the last 'x' number of messages.

            if (upTo != "" && count != 0)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} You can't provide both a count of messages and a message to delete up to! Please only provide one of the two arguments.").AsEphemeral(true));
                return;
            }

            List<DiscordMessage> messagesToClear;
            if (upTo == "")
            {
                var messages = await ctx.Channel.GetMessagesAsync((int)count);
                messagesToClear = messages.ToList();
            }
            else
            {
                DiscordMessage message;
                ulong messageId;
                if (!upTo.Contains("discord.com"))
                {
                    try
                    {
                        messageId = Convert.ToUInt64(upTo);
                    }
                    catch
                    {
                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} That doesn't look like a valid message ID or link! Try again."));
                        return;
                    }
                }
                else
                {
                    // Extract all IDs from URL. This will leave you with something like "guild_id/channel_id/message_id".
                    Regex extractIds = new(@".*.discord.com\/channels\/");
                    Match selectionToRemove = extractIds.Match(upTo);
                    upTo = upTo.Replace(selectionToRemove.ToString(), "");

                    // Parse message ID, set to variable
                    Regex getMessageId = new(@"[a-zA-Z0-9]*\/[a-zA-Z0-9]*\/");
                    Match idsToRemove = getMessageId.Match(upTo);
                    string targetMsgId = upTo.Replace(idsToRemove.ToString(), "");
                    messageId = Convert.ToUInt64(targetMsgId.ToString());
                }

                // This is the message we will delete up to. This message will not be deleted.
                message = await ctx.Channel.GetMessageAsync(messageId);

                // List of messages to delete, up to (not including) the one we just got.
                var messages = await ctx.Channel.GetMessagesAfterAsync(message.Id);
                messagesToClear = messages.ToList();
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
                        // User is not in the server, assume not mod
                        continue;
                    }

                    if (GetPermLevel(member) >= ServerPermLevel.TrialModerator)
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
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} You can't use `botsonly` and `humansonly` together! Pick one or the other please.").AsEphemeral(true));
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

            // Images only
            if (imagesOnly)
            {
                if (linksOnly)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} You can't use `imagesonly` and `linksonly` together! Pick one or the other please.").AsEphemeral(true));
                    return;
                }

                foreach (var message in messagesToClear.ToList())
                {
                    if (message.Attachments.Count == 0)
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

            // All filters checked. 'messages' is now our final list of messages to delete.

            // Warn the mod if we're going to be deleting 50 or more messages.
            if (messagesToClear.Count >= 50)
            {
                DiscordButtonComponent confirmButton = new(ButtonStyle.Danger, "clear-confirm-callback", "Delete Messages");
                DiscordMessage confirmationMessage = await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Muted} You're about to delete {messagesToClear.Count} messages. Are you sure?").AddComponents(confirmButton).AsEphemeral(true));

                MessagesToClear.Add(confirmationMessage.Id, messagesToClear);
            }
            else
            {
                if (messagesToClear.Count >= 1)
                {
                    await ctx.Channel.DeleteMessagesAsync(messagesToClear);

                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Done!").AsEphemeral(true));
                }
                else
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} There were no messages that matched all of the arguments you provided! Nothing to do."));
                }
            }
        }
    }
}
