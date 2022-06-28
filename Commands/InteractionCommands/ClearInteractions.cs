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

            if (count == 0 && upTo == "")
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I need to know how many messages to delete! Please provide a value for `count` or `up_to`.").AsEphemeral(true));
                return;
            }

            // If count is too low or too high, refuse the request

            if (count < 0)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I can't delete a negative number of messages! Try setting `count` to a positive number.").AsEphemeral(true));
                return;
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
                    if (Constants.RegexConstants.discord_link_rx.Match(upTo).Groups[2].Value == ctx.Channel.Id.ToString())
                    {
                        messageId = Convert.ToUInt64(Constants.RegexConstants.discord_link_rx.Match(upTo).Groups[3].Value);
                    }
                    else
                    {
                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} Please provide a link to a message in this channel!").AsEphemeral(true));
                        return;
                    }
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
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} You can't use `bots_only` and `humans_only` together! Pick one or the other please.").AsEphemeral(true));
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
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} You can't use `images_only` and `links_only` together! Pick one or the other please.").AsEphemeral(true));
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
                    await ctx.Channel.DeleteMessagesAsync(messagesToClear, $"[Clear by {ctx.User.Username}#{ctx.User.Discriminator}]");
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Deleted} Cleared **{messagesToClear.Count}** messages from {ctx.Channel.Mention}!");
                    await LogChannelHelper.LogMessageAsync("mod",
                        new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Deleted} **{messagesToClear.Count}** messages were cleared in {ctx.Channel.Mention} by {ctx.User.Mention}.")
                            .WithAllowedMentions(Mentions.None)
                    );
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
