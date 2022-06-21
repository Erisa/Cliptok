using static Cliptok.Program;

namespace Cliptok.Events
{
    public class InteractionEvents
    {
        public static async Task ComponentInteractionCreateEvent(DiscordClient _, ComponentInteractionCreateEventArgs e)
        {
            // Initial response to avoid the 3 second timeout, will edit later.
            var eout = new DiscordInteractionResponseBuilder().AsEphemeral(true);
            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource, eout);

            // Edits need a webhook rather than interaction..?
            DiscordWebhookBuilder webhookOut;

            if (e.Id == "line-limit-deleted-message-callback")
            {
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
            else
            {
                await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("Unknown interaction. I don't know what you are asking me for."));
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
        }


    }
}
