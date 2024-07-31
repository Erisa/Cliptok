using static Cliptok.Program;

namespace Cliptok.Events
{
    public class InteractionEvents
    {
        public static async Task ComponentInteractionCreateEvent(DiscordClient _, ComponentInteractionCreatedEventArgs e)
        {
            // Edits need a webhook rather than interaction..?
            DiscordWebhookBuilder webhookOut;

            if (e.Id == "line-limit-deleted-message-callback")
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredChannelMessageWithSource, new DiscordInteractionResponseBuilder().AsEphemeral(true));

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

                if (!messagesToClear.ContainsKey(e.Message.Id))
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent($"{cfgjson.Emoji.Error} These messages have already been deleted!").AsEphemeral(true));
                    return;
                }

                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredChannelMessageWithSource, new DiscordInteractionResponseBuilder().AsEphemeral(true));

                List<DiscordMessage> messages = messagesToClear.GetValueOrDefault(e.Message.Id);

                await e.Channel.DeleteMessagesAsync(messages, $"[Clear by {DiscordHelpers.UniqueUsername(e.User)}]");

                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{cfgjson.Emoji.Deleted} **{messages.Count}** messages were cleared in {e.Channel.Mention} by {e.User.Mention}.")
                        .WithAllowedMentions(Mentions.None)
                );

                await LogChannelHelper.LogDeletedMessagesAsync(
                    "messages",
                    $"{cfgjson.Emoji.Deleted} **{messages.Count}** messages were cleared from {e.Channel.Mention} by {e.User.Mention}.",
                    messages,
                    e.Channel
                );

                messagesToClear.Remove(e.Message.Id);

                await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Deleted} Cleared **{messages.Count}** messages from {e.Channel.Mention}!");

                await e.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().WithContent($"{cfgjson.Emoji.Success} Done!").AsEphemeral(true));
            }
            else if (e.Id == "debug-overrides-add-confirm-callback")
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);
                
                var overridesPendingAddition = Commands.Debug.OverridesPendingAddition;
                if (!overridesPendingAddition.ContainsKey(e.Message.Id))
                {
                    await e.Channel.SendMessageAsync(new DiscordMessageBuilder().WithContent($"{cfgjson.Emoji.Error} {e.User.Mention}, this action has already been completed!").WithReply(e.Message.Id));
                    
                    // Remove buttons from original message so this doesn't happen again
                    var originalMsgWithoutButtons = new DiscordMessageBuilder(e.Message);
                    originalMsgWithoutButtons.ClearComponents();
                    await e.Message.ModifyAsync(originalMsgWithoutButtons);
                    
                    return;
                }
                
                // Get override data
                var pendingOverride = overridesPendingAddition.GetValueOrDefault(e.Message.Id);
                var mockOverwrite = pendingOverride.Overwrite;
                var channelId = pendingOverride.ChannelId;
                
                // This is really cursed, but it effectively converts our mock DiscordOverwrite into an actual one so that it can be added to the current list of overwrites.
                // Since the mock overwrite serializes into the same format as a DiscordOverwrite, we can serialize it and then deserialize it back to DiscordOverwrite to convert it.
                var newOverwrite = JsonConvert.DeserializeObject<DiscordOverwrite>(JsonConvert.SerializeObject(mockOverwrite));

                // Get current overrides for user in db
                var userOverwrites = await db.HashGetAsync("overrides", mockOverwrite.Id);
                if (userOverwrites.IsNullOrEmpty)
                {
                    // No overwrites for this user yet, create a list and add to it
                    
                    var overwrites = new Dictionary<string, DiscordOverwrite> { { channelId.ToString(), newOverwrite } };
                    await db.HashSetAsync("overrides", mockOverwrite.Id, JsonConvert.SerializeObject(overwrites));
                }
                else
                {
                    // Overwrites for user exist, add to them
                    
                    var overwrites = JsonConvert.DeserializeObject<Dictionary<string, DiscordOverwrite>>(userOverwrites);
                    if (overwrites.ContainsKey(channelId.ToString()))
                    {
                        // Require extra confirmation for merging permissions!
                        var mergeConfirmResponse = new DiscordMessageBuilder()
                            .WithContent($"{cfgjson.Emoji.Warning} **Caution:** This user already has an override for <#{channelId}>! Do you want to merge the permissions? Here are their **current** permissions:\n**Allowed:** {overwrites[channelId.ToString()].Allowed}\n**Denied:** {overwrites[channelId.ToString()].Denied}")
                            .AddComponents(new DiscordButtonComponent(DiscordButtonStyle.Danger, "debug-overrides-add-merge-confirm-callback", "Merge"), new DiscordButtonComponent(DiscordButtonStyle.Primary, "debug-overrides-add-cancel-callback", "Cancel"));
                        
                        await e.Message.ModifyAsync(mergeConfirmResponse);
                        return;
                    }
                    else
                    {
                        overwrites.Add(channelId.ToString(), newOverwrite);   
                    }
                    // Update db
                    await db.HashSetAsync("overrides", mockOverwrite.Id, JsonConvert.SerializeObject(overwrites));
                }
                
                // Remove from db so the override is not added again
                overridesPendingAddition.Remove(e.Message.Id);

                // Respond
                await e.Message.ModifyAsync(new DiscordMessageBuilder().WithContent($"{cfgjson.Emoji.Success} Successfully added the following override for <@{newOverwrite.Id}> to <#{pendingOverride.ChannelId}>!\n**Allowed:** {newOverwrite.Allowed}\n**Denied:** {newOverwrite.Denied}"));
            }
            else if (e.Id == "debug-overrides-add-cancel-callback")
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);
                
                var overridesPendingAddition = Commands.Debug.OverridesPendingAddition;
                if (!overridesPendingAddition.ContainsKey(e.Message.Id))
                {
                    await e.Channel.SendMessageAsync(new DiscordMessageBuilder().WithContent($"{cfgjson.Emoji.Error} {e.User.Mention}, this action has already been completed!").WithReply(e.Message.Id));
                    
                    // Remove buttons from original message so this doesn't happen again
                    var originalMsgWithoutButtons = new DiscordMessageBuilder(e.Message);
                    originalMsgWithoutButtons.ClearComponents();
                    await e.Message.ModifyAsync(originalMsgWithoutButtons);
                    
                    return;
                }
                
                await e.Message.ModifyAsync(new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} Cancelled! Nothing was changed."));
                overridesPendingAddition.Remove(e.Message.Id);
            }
            else if (e.Id == "debug-overrides-add-merge-confirm-callback")
            {
                // User already has an overwrite for the requested channel!
                // Merge the permissions of the current & new overrides.
                
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);
                
                var overridesPendingAddition = Commands.Debug.OverridesPendingAddition;
                if (!overridesPendingAddition.ContainsKey(e.Message.Id))
                {
                    await e.Channel.SendMessageAsync(new DiscordMessageBuilder().WithContent($"{cfgjson.Emoji.Error} {e.User.Mention}, this action has already been completed!").WithReply(e.Message.Id));
                    
                    // Remove buttons from original message so this doesn't happen again
                    var originalMsgWithoutButtons = new DiscordMessageBuilder(e.Message);
                    originalMsgWithoutButtons.ClearComponents();
                    await e.Message.ModifyAsync(originalMsgWithoutButtons);
                    
                    return;
                }

                // Get new override data
                var pendingOverride = overridesPendingAddition.GetValueOrDefault(e.Message.Id);
                var mockOverwrite = pendingOverride.Overwrite;
                var channelId = pendingOverride.ChannelId;
                var newOverwrite = JsonConvert.DeserializeObject<DiscordOverwrite>(JsonConvert.SerializeObject(mockOverwrite));
                
                // Existing override data
                var userOverwrites = await db.HashGetAsync("overrides", mockOverwrite.Id);
                var overwrites = JsonConvert.DeserializeObject<Dictionary<string, DiscordOverwrite>>(userOverwrites);
                        
                // Merge permissions
                var existingOverwrite = overwrites[channelId.ToString()];
                var newMockOverwrite = new MockUserOverwrite
                {
                    Id = mockOverwrite.Id,
                    Allowed = newOverwrite.Allowed | existingOverwrite.Allowed,
                    Denied = newOverwrite.Denied | existingOverwrite.Denied
                };
                
                // Cursed conversion again
                newOverwrite = JsonConvert.DeserializeObject<DiscordOverwrite>(JsonConvert.SerializeObject(newMockOverwrite));
                
                overwrites[channelId.ToString()] = newOverwrite;
                
                // Update db
                await db.HashSetAsync("overrides", mockOverwrite.Id, JsonConvert.SerializeObject(overwrites));
                
                // Respond
                await e.Message.ModifyAsync(new DiscordMessageBuilder().WithContent($"{cfgjson.Emoji.Success} Override successfully added. <@{newOverwrite.Id}> already had an override in <#{pendingOverride.ChannelId}>, so here are their new permissions:\n**Allowed:** {newOverwrite.Allowed}\n**Denied:** {newOverwrite.Denied}"));
            }
            else
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Unknown interaction. I don't know what you are asking me for.").AsEphemeral(true));
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
                            DiscordInteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent(
                                $"{cfgjson.Emoji.NoPermissions} Invalid permission level to use command **{e.Context.CommandName}**!\n" +
                                $"Required: `{att.TargetLvl}`\n" +
                                $"You have: `{levelText}`")
                                .AsEphemeral(true)
                            );
                    }
            }
            e.Context.Client.Logger.LogError(CliptokEventID, e.Exception, "Error during invocation of interaction command {command} by {user}", e.Context.CommandName, $"{DiscordHelpers.UniqueUsername(e.Context.User)}");
        }

        public static async Task ContextCommandErrorEvent(SlashCommandsExtension _, DSharpPlus.SlashCommands.EventArgs.ContextMenuErrorEventArgs e)
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
                            DiscordInteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent(
                                $"{cfgjson.Emoji.NoPermissions} Invalid permission level to use command **{e.Context.CommandName}**!\n" +
                                $"Required: `{att.TargetLvl}`\n" +
                                $"You have: `{levelText}`")
                                .AsEphemeral(true)
                            );
                    }
            }
            e.Context.Client.Logger.LogError(CliptokEventID, e.Exception, "Error during invocation of context command {command} by {user}", e.Context.CommandName, $"{DiscordHelpers.UniqueUsername(e.Context.User)}");
        }

    }
}
