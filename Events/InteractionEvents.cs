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
                Dictionary<ulong, List<DiscordMessage>> messagesToClear = Commands.ClearCmds.MessagesToClear;

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

                var overridesPendingAddition = Commands.DebugCmds.OverridesPendingAddition;
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
                        var currentAllowedPerms = (overwrites[channelId.ToString()].Allowed).ToString("name");
                        if (string.IsNullOrWhiteSpace(currentAllowedPerms))
                            currentAllowedPerms = "None";

                        var currentDeniedPerms = (overwrites[channelId.ToString()].Denied).ToString("name");
                        if (string.IsNullOrWhiteSpace(currentDeniedPerms))
                            currentDeniedPerms = "None";

                        var mergeConfirmResponse = new DiscordMessageBuilder()
                            .WithContent($"{cfgjson.Emoji.Warning} **Caution:** This user already has an override for <#{channelId}>! Do you want to merge the permissions? Here are their **current** permissions:\n**Allowed:** {currentAllowedPerms}\n**Denied:** {currentDeniedPerms}")
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
                var allowedPermsStr = newOverwrite.Allowed.ToString("name");
                if (string.IsNullOrWhiteSpace(allowedPermsStr))
                    allowedPermsStr = "None";

                var deniedPermsStr = newOverwrite.Denied.ToString("name");
                if (string.IsNullOrWhiteSpace(deniedPermsStr))
                    deniedPermsStr = "None";

                await e.Message.ModifyAsync(new DiscordMessageBuilder().WithContent($"{cfgjson.Emoji.Success} Successfully added the following override for <@{newOverwrite.Id}> to <#{pendingOverride.ChannelId}>!\n**Allowed:** {allowedPermsStr}\n**Denied:** {deniedPermsStr}"));
            }
            else if (e.Id == "debug-overrides-add-cancel-callback")
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);

                var overridesPendingAddition = Commands.DebugCmds.OverridesPendingAddition;
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

                var overridesPendingAddition = Commands.DebugCmds.OverridesPendingAddition;
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
                    Allowed = newOverwrite.Allowed.Add(existingOverwrite.Allowed.EnumeratePermissions().ToArray()),
                    Denied = newOverwrite.Denied.Add(existingOverwrite.Denied.EnumeratePermissions().ToArray())
                };

                // Cursed conversion again
                newOverwrite = JsonConvert.DeserializeObject<DiscordOverwrite>(JsonConvert.SerializeObject(newMockOverwrite));

                overwrites[channelId.ToString()] = newOverwrite;

                // Update db
                await db.HashSetAsync("overrides", mockOverwrite.Id, JsonConvert.SerializeObject(overwrites));

                // Respond
                var allowedPermsStr = newOverwrite.Allowed.ToString("name");
                if (string.IsNullOrWhiteSpace(allowedPermsStr))
                    allowedPermsStr = "None";

                var deniedPermsStr = newOverwrite.Denied.ToString("name");
                if (string.IsNullOrWhiteSpace(deniedPermsStr))
                    deniedPermsStr = "None";

                await e.Message.ModifyAsync(new DiscordMessageBuilder().WithContent($"{cfgjson.Emoji.Success} Override successfully added. <@{newOverwrite.Id}> already had an override in <#{pendingOverride.ChannelId}>, so here are their new permissions:\n**Allowed:** {allowedPermsStr}\n**Denied:** {deniedPermsStr}"));
            }
            else if (e.Id == "insiders-info-roles-menu-callback")
            {
                // Shows a menu in #insider-info that allows a user to toggle their Insider roles

                // Defer interaction
                await e.Interaction.DeferAsync(ephemeral: true);

                // Fetch member
                var member = await e.Guild.GetMemberAsync(e.User.Id);

                // Fetch Insider roles to check whether member already has them
                var insiderCanaryRole = await e.Guild.GetRoleAsync(cfgjson.UserRoles.InsiderCanary);
                var insiderDevRole = await e.Guild.GetRoleAsync(cfgjson.UserRoles.InsiderDev);
                var insiderBetaRole = await e.Guild.GetRoleAsync(cfgjson.UserRoles.InsiderBeta);
                var insiderRPRole = await e.Guild.GetRoleAsync(cfgjson.UserRoles.InsiderRP);
                var insider10RPRole = await e.Guild.GetRoleAsync(cfgjson.UserRoles.Insider10RP);
                var patchTuesdayRole = await e.Guild.GetRoleAsync(cfgjson.UserRoles.PatchTuesday);

                // Show menu with current Insider roles, apply new roles based on user selection
                var menu = new DiscordSelectComponent("insiders-info-roles-menu-response-callback", "Choose your Insider roles",
                    new List<DiscordSelectComponentOption>()
                    {
                        new("Windows 11 Canary channel", "insiders-info-w11-canary", isDefault: member.Roles.Contains(insiderCanaryRole)),
                        new("Windows 11 Dev channel", "insiders-info-w11-dev", isDefault: member.Roles.Contains(insiderDevRole)),
                        new("Windows 11 Beta channel", "insiders-info-w11-beta", isDefault: member.Roles.Contains(insiderBetaRole)),
                        new("Windows 11 Release Preview channel", "insiders-info-w11-rp", isDefault: member.Roles.Contains(insiderRPRole)),
                        new("Windows 10 Release Preview channel", "insiders-info-w10-rp", isDefault: member.Roles.Contains(insider10RPRole)),
                        new("Patch Tuesday", "insiders-info-pt", isDefault: member.Roles.Contains(patchTuesdayRole)),
                    }, minOptions: 0, maxOptions: 6);

                var builder = new DiscordFollowupMessageBuilder()
                    .WithContent($"{cfgjson.Emoji.Insider} Use the menu below to toggle your Insider roles!")
                    .AddComponents(menu)
                    .AsEphemeral(true);

                await e.Interaction.CreateFollowupMessageAsync(builder);
            }
            else if (e.Id == "insiders-info-roles-menu-response-callback")
            {
                // User has selected new Insider roles w/ menu above
                // Compare selection against current roles; add or remove roles as necessary to match selection

                // Defer
                await e.Interaction.DeferAsync(ephemeral: true);

                // Get member
                var member = await e.Guild.GetMemberAsync(e.User.Id);

                // Map role select options to role IDs
                var insiderRoles = new Dictionary<string, ulong>
                {
                    { "insiders-info-w11-canary", cfgjson.UserRoles.InsiderCanary },
                    { "insiders-info-w11-dev", cfgjson.UserRoles.InsiderDev },
                    { "insiders-info-w11-beta", cfgjson.UserRoles.InsiderBeta },
                    { "insiders-info-w11-rp", cfgjson.UserRoles.InsiderRP },
                    { "insiders-info-w10-rp", cfgjson.UserRoles.Insider10RP },
                    { "insiders-info-pt", cfgjson.UserRoles.PatchTuesday }
                };

                // Get a list of the member's current roles that we can add to or remove from
                // Then we can apply this in a single request with member.ModifyAsync to avoid making repeated member update requests
                List<DiscordRole> memberRoles = member.Roles.ToList();

                var selection = e.Values.Select(x => insiderRoles[x]).ToList();

                foreach (var roleId in insiderRoles.Values)
                {
                    var role = await e.Guild.GetRoleAsync(roleId);

                    if (selection.Contains(roleId))
                    {
                        // Member should have the role
                        if (!memberRoles.Contains(role))
                            memberRoles.Add(role);
                    }
                    else
                    {
                        // Member should not have the role
                        if (memberRoles.Contains(role))
                            memberRoles.Remove(role);
                    }
                }

                // Apply roles
                await member.ModifyAsync(x => x.Roles = memberRoles);

                await e.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().WithContent($"{cfgjson.Emoji.Success} Your Insider roles have been updated!").AsEphemeral(true));
            }
            else if (e.Id == "insiders-info-chat-btn-callback")
            {
                // Button in #insiders-info that checks whether user has 'insiderChat' role and asks them to confirm granting/revoking it

                // Defer
                await e.Interaction.DeferAsync(ephemeral: true);

                // Get member
                var member = await e.Guild.GetMemberAsync(e.User.Id);

                // Get insider chat role
                var insiderChatRole = await e.Guild.GetRoleAsync(cfgjson.UserRoles.InsiderChat);

                // Check whether member already has any insider roles
                var insiderRoles = new List<ulong>()
                {
                    cfgjson.UserRoles.InsiderCanary,
                    cfgjson.UserRoles.InsiderDev,
                    cfgjson.UserRoles.InsiderBeta,
                    cfgjson.UserRoles.InsiderRP,
                    cfgjson.UserRoles.Insider10RP,
                    cfgjson.UserRoles.PatchTuesday
                };
                if (member.Roles.Any(x => insiderRoles.Contains(x.Id)))
                {
                    // Member already has an insider role, thus already has access to #insiders
                    // No need for the chat role too

                    string insidersMention;
                    if (cfgjson.InsidersChannel == 0)
                        insidersMention = "#insiders";
                    else
                        insidersMention = $"<#{cfgjson.InsidersChannel}>";

                    await e.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder()
                        .WithContent($"You already have Insider roles, so you already have access to chat in {insidersMention}!")
                        .AsEphemeral(true));

                    return;
                }

                if (member.Roles.Contains(insiderChatRole))
                {
                    // Member already has the role
                    // Ask them if they'd like to remove it
                    var confirmResponse = new DiscordFollowupMessageBuilder()
                        .WithContent($"{cfgjson.Emoji.Warning} You already have the {insiderChatRole.Mention} role! Would you like to remove it?")
                        .AddComponents(new DiscordButtonComponent(DiscordButtonStyle.Danger, "insiders-info-chat-btn-remove-confirm-callback", "Remove"));

                    await e.Interaction.CreateFollowupMessageAsync(confirmResponse);
                }
                else
                {
                    // Member does not have the role; show a confirmation message with a button that will give it to them
                    await e.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder()
                        .WithContent($"{cfgjson.Emoji.Warning} Please note that <#{cfgjson.InsidersChannel}> is **not for tech support**! If you need tech support, please ask in the appropriate channels instead. Press the button to acknowledge this and get the {insiderChatRole.Mention} role.")
                        .AddComponents(new DiscordButtonComponent(DiscordButtonStyle.Secondary, "insiders-info-chat-btn-confirm-callback", "I understand")));
                }
            }
            else if (e.Id == "insiders-info-chat-btn-confirm-callback")
            {
                // Confirmation for granting insiderChat role, see above

                // Defer
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);

                // Give member insider chat role
                var member = await e.Guild.GetMemberAsync(e.User.Id);
                var insiderChatRole = await e.Guild.GetRoleAsync(cfgjson.UserRoles.InsiderChat);
                await member.GrantRoleAsync(insiderChatRole);

                // Respond
                await e.Interaction.EditFollowupMessageAsync(e.Message.Id, new DiscordWebhookBuilder().WithContent($"{cfgjson.Emoji.Success} You have been given the {insiderChatRole.Mention} role!"));
            }
            else if (e.Id == "insiders-info-chat-btn-remove-confirm-callback")
            {
                // Confirmation for revoking insiderChat role, see above

                // Defer
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);

                // Get member
                var member = await e.Guild.GetMemberAsync(e.User.Id);

                var insiderChatRole = await e.Guild.GetRoleAsync(cfgjson.UserRoles.InsiderChat);
                await member.RevokeRoleAsync(insiderChatRole);

                await e.Interaction.EditFollowupMessageAsync(e.Message.Id, new DiscordWebhookBuilder().WithContent($"{cfgjson.Emoji.Success} You have been removed from the {insiderChatRole.Mention} role!"));
            }
            else
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Unknown interaction. I don't know what you are asking me for.").AsEphemeral(true));
            }

        }

        public static async Task SlashCommandErrored(CommandErroredEventArgs e)
        {
            if (e.Exception is ChecksFailedException slex)
            {
                foreach (var check in slex.Errors)
                    if (check.ContextCheckAttribute is RequireHomeserverPermAttribute att && e.Context.Command.Name != "edit")
                    {
                        var level = (await GetPermLevelAsync(e.Context.Member));
                        var levelText = level.ToString();
                        if (level == ServerPermLevel.Nothing && rand.Next(1, 100) == 69)
                            levelText = $"naught but a thing, my dear human. Congratulations, you win {rand.Next(1, 10)} bonus points.";

                        await e.Context.RespondAsync(new DiscordInteractionResponseBuilder().WithContent(
                                $"{cfgjson.Emoji.NoPermissions} Invalid permission level to use command **{e.Context.Command.Name}**!\n" +
                                $"Required: `{att.TargetLvl}`\n" +
                                $"You have: `{levelText}`")
                                .AsEphemeral(true)
                            );
                    }
            }
            e.Context.Client.Logger.LogError(CliptokEventID, e.Exception, "Error during invocation of interaction command {command} by {user}", e.Context.Command.Name, $"{DiscordHelpers.UniqueUsername(e.Context.User)}");
        }

    }
}
