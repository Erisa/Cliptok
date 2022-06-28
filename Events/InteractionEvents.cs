using static Cliptok.Program;

namespace Cliptok.Events
{
    public class InteractionEvents
    {
        public static async Task ComponentInteractionCreateEvent(DiscordClient _, ComponentInteractionCreateEventArgs e)
        {
            // Edits need a webhook rather than interaction..?
            DiscordWebhookBuilder webhookOut;

            if (e.Id == "line-limit-deleted-message-callback")
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource, new DiscordInteractionResponseBuilder().AsEphemeral(true));

                var text = await db.HashGetAsync("deletedMessageReferences", e.Message.Id);
                if (text.IsNullOrEmpty)
                {
                    discord.Logger.LogError("Failed to find deleted message content for {id}", e.Message.Id);
                    webhookOut = new DiscordWebhookBuilder().WithContent("I couldn't find any content for that message! This is most likely a bug, please notify my developer!");
                    await e.Interaction.EditOriginalResponseAsync(webhookOut);
                }
                else
                {
                    DiscordEmbedBuilder embed = new();
                    embed.Description = text;
                    embed.Title = "Deleted message content";
                    webhookOut = new DiscordWebhookBuilder().AddEmbed(embed);
                    await e.Interaction.EditOriginalResponseAsync(webhookOut);
                }

            }
            else if (e.Id == "clear-confirm-callback")
            {
                Dictionary<ulong, List<DiscordMessage>> messagesToClear = Commands.InteractionCommands.ClearInteractions.MessagesToClear;
                
                List<DiscordMessage> messages = messagesToClear.GetValueOrDefault(e.Message.Id);

                await e.Channel.DeleteMessagesAsync(messages, $"[Clear by {e.User.Username}#{e.User.Discriminator}]");

                DiscordButtonComponent disabledButton = new(ButtonStyle.Danger, "clear-confirm-callback", "Delete Messages", true);
                await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Deleted} Cleared **{messagesToClear.Count}** messages from {e.Channel.Mention}!");
                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{cfgjson.Emoji.Deleted} **{messagesToClear.Count}** messages were cleared in {e.Channel.Mention} by {e.User.Mention}.")
                        .WithAllowedMentions(Mentions.None)
                );
                e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().WithContent($"{cfgjson.Emoji.Success} Done!").AddComponents(disabledButton).AsEphemeral(true));
            }
            else
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Unknown interaction. I don't know what you are asking me for.").AsEphemeral(true));
            }

        }

        public static async Task SlashCommandErrorEvent(SlashCommandsExtension _, DSharpPlus.SlashCommands.EventArgs.SlashCommandErrorEventArgs e)
        {
            if (e.Exception is SlashExecutionChecksFailedException slex)
            {
                foreach (var check in slex.FailedChecks)
                    if (check is SlashRequireHomeserverPermAttribute att && e.Context.CommandName != "edit")
                    {
                        var level = GetPermLevel(e.Context.Member);
                        var levelText = level.ToString();
                        if (level == ServerPermLevel.Nothing && rand.Next(1, 100) == 69)
                            levelText = $"naught but a thing, my dear human. Congratulations, you win {rand.Next(1, 10)} bonus points.";

                        await e.Context.CreateResponseAsync(
                            InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent(
                                $"{cfgjson.Emoji.NoPermissions} Invalid permission level to use command **{e.Context.CommandName}**!\n" +
                                $"Required: `{att.TargetLvl}`\n" +
                                $"You have: `{levelText}`")
                                .AsEphemeral(true)
                            );
                    }
            }
            e.Context.Client.Logger.LogError(CliptokEventID, e.Exception, "Error during invocation of interaction command {command} by {user}", e.Context.CommandName, $"{e.Context.User.Username}#{e.Context.User.Discriminator}");
        }

    }
}
