using static Cliptok.Helpers.WarningHelpers;

namespace Cliptok.Commands
{
    internal class WarningCmds
    {
        [Command("Show Warnings")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task ContextWarnings(UserCommandContext ctx, DiscordUser targetUser)
        {
            await ctx.RespondAsync(embed: await WarningHelpers.GenerateWarningsEmbedAsync(targetUser), ephemeral: true);
        }

        [Command("warn")]
        [Description("Formally warn a user, usually for breaking the server rules.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task WarnSlashCommand(SlashCommandContext ctx,
         [Parameter("user"), Description("The user to warn.")] DiscordUser user,
         [Parameter("reason"), Description("The reason they're being warned.")] string reason,
         [Parameter("reply_msg_id"), Description("The ID of a message to reply to, must be in the same channel.")] string replyMsgId = "0",
         [Parameter("channel"), Description("The channel to warn the user in, implied if not supplied.")] DiscordChannel channel = null
        )
        {
            // collision detection
            if (mostRecentWarning is not null && user.Id == mostRecentWarning.TargetUserId)
            {
                var timeSinceLastWarning = DateTime.UtcNow.Subtract(mostRecentWarning.WarnTimestamp);
                if (timeSinceLastWarning <= TimeSpan.FromSeconds(5))
                {
                    var response = new DiscordInteractionResponseBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Error} {user.Mention} was already warned a few seconds ago, refusing yours to prevent collisions. If your warning is unrelated, try again in a few seconds.")
                            .AsEphemeral(true);
                    if (!mostRecentWarning.Stub)
                        response.AddEmbed(await FancyWarnEmbedAsync(mostRecentWarning, detailed: true));

                    await ctx.RespondAsync(response);
                    return;
                }
            }

            // this gets updated with a full warning object later, shove a stub in for now
            mostRecentWarning = new()
            {
                TargetUserId = user.Id,
                ModUserId = ctx.User.Id,
                WarnTimestamp = DateTime.UtcNow,
                Stub = true // make it clear this isn't a real warning
            };

            // Initial response to avoid the 3 second timeout, will edit later.
            await ctx.DeferResponseAsync(true);

            // Edits need a webhook rather than interaction..?
            DiscordWebhookBuilder webhookOut;

            DiscordMember targetMember;

            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(user.Id);
                if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && ((await GetPermLevelAsync(targetMember)) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
                {
                    webhookOut = new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} As a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
                    await ctx.EditResponseAsync(webhookOut);
                    return;
                }
            }
            catch
            {
                // do nothing :/
            }

            if (channel is null)
                channel = ctx.Channel;

            var messageBuild = new DiscordMessageBuilder()
                .WithContent($"{Program.cfgjson.Emoji.Warning} {user.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");

            if (replyMsgId != "0")
            {
                if (!ulong.TryParse(replyMsgId, out var msgId))
                {
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} Invalid reply message ID! Please try again.").AsEphemeral(true));
                    return;
                }
                messageBuild.WithReply(msgId, true, false);
            }

            var msg = await channel.SendMessageAsync(messageBuild);

            _ = await WarningHelpers.GiveWarningAsync(user, ctx.User, reason, msg, channel);
            webhookOut = new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Success} User was warned successfully: {DiscordHelpers.MessageLink(msg)}");
            await ctx.EditResponseAsync(webhookOut);
        }

        [Command("warnings")]
        [Description("Fetch the warnings for a user.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        public async Task WarningsSlashCommand(SlashCommandContext ctx,
            [Parameter("user"), Description("The user to find the warnings for.")] DiscordUser user,
            [Parameter("public"), Description("Whether to show the warnings in public chat. Do not disrupt chat with this.")] bool publicWarnings = false
        )
        {
            var eout = new DiscordInteractionResponseBuilder().AddEmbed(await WarningHelpers.GenerateWarningsEmbedAsync(user));
            if (!publicWarnings)
                eout.AsEphemeral(true);

            await ctx.RespondAsync(eout);
        }

        internal partial class WarningsAutocompleteProvider : IAutoCompleteProvider
        {
            public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext ctx)
            {
                return await GetWarningsForAutocompleteAsync(ctx);
            }
        }
        
        internal partial class PardonedWarningsAutocompleteProvider : IAutoCompleteProvider
        {
            public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext ctx)
            {
                return await GetWarningsForAutocompleteAsync(ctx, pardonedOnly: true);
            }
        }
        
        internal partial class UnpardonedWarningsAutocompleteProvider : IAutoCompleteProvider
        {
            public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext ctx)
            {
                return await GetWarningsForAutocompleteAsync(ctx, excludePardoned: true);
            }
        }
        
        private static async Task<List<DiscordAutoCompleteChoice>> GetWarningsForAutocompleteAsync(AutoCompleteContext ctx, bool excludePardoned = false, bool pardonedOnly = false)
        {
            if (excludePardoned && pardonedOnly)
                throw new ArgumentException("Cannot simultaneously exclude pardoned warnings from autocomplete suggestions and only show pardoned warnings.");
            
            var list = new List<DiscordAutoCompleteChoice>();

            var useroption = ctx.Options.FirstOrDefault(x => x.Name == "user");
            if (useroption == default)
            {
                return list;
            }

            var user = await ctx.Client.GetUserAsync((ulong)useroption.Value);

            var warnings = (await Program.redis.HashGetAllAsync(user.Id.ToString()))
                .Where(x => JsonConvert.DeserializeObject<UserWarning>(x.Value).Type == WarningType.Warning).ToDictionary(
                    x => x.Name.ToString(),
                    x => JsonConvert.DeserializeObject<UserWarning>(x.Value)
                ).OrderByDescending(x => x.Value.WarningId);

            foreach (var warning in warnings)
            {
                if (list.Count >= 25)
                    break;

                string warningString = $"{StringHelpers.Pad(warning.Value.WarningId)} - {StringHelpers.Truncate(warning.Value.WarnReason, 29, true)} - {TimeHelpers.TimeToPrettyFormat(DateTime.UtcNow - warning.Value.WarnTimestamp, true)}";
                if (warning.Value.IsPardoned)
                {
                    if (excludePardoned)
                        continue;
                    
                    warningString += " (pardoned)";
                }
                else if (pardonedOnly)
                    continue;

                var focusedOption = ctx.Options.FirstOrDefault(option => option.Focused);
                if (focusedOption is not null)
                    if (warning.Value.WarnReason.Contains((string)focusedOption.Value) || warningString.ToLower().Contains(focusedOption.Value.ToString().ToLower()))
                        list.Add(new DiscordAutoCompleteChoice(warningString, StringHelpers.Pad(warning.Value.WarningId)));
            }

            return list;
        }

        [Command("warndetails")]
        [Description("Search for a warning and return its details.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task WarndetailsSlashCommand(SlashCommandContext ctx,
            [Parameter("user"), Description("The user to fetch a warning for.")] DiscordUser user,
            [SlashAutoCompleteProvider(typeof(WarningsAutocompleteProvider)), Parameter("warning"), Description("Type to search! Find the warning you want to fetch.")] string warning,
            [Parameter("public"), Description("Whether to show the output publicly.")] bool publicWarnings = false
        )
        {
            if (warning.Contains(' '))
            {
                warning = warning.Split(' ')[0];
            }

            long warnId;
            try
            {
                warnId = Convert.ToInt64(warning);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Looks like your warning option was invalid! Give it another go?", ephemeral: true);
                return;
            }

            UserWarning warningObject = GetWarning(user.Id, warnId);

            if (warningObject is null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.", ephemeral: true);
            else if (warningObject.Type == WarningType.Note)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That's a note, not a warning! Try using `/note details` instead, or make sure you've got the right warning ID.", ephemeral: true);
            else
            {
                await ctx.DeferResponseAsync(ephemeral: !publicWarnings);
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().AddEmbed(await FancyWarnEmbedAsync(warningObject, true, userID: user.Id)));
            }
        }

        [Command("delwarn")]
        [Description("Search for a warning and delete it!")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task DelwarnSlashCommand(SlashCommandContext ctx,
            [Parameter("user"), Description("The user to delete a warning for.")] DiscordUser targetUser,
            [SlashAutoCompleteProvider(typeof(WarningsAutocompleteProvider))][Parameter("warning"), Description("Type to search! Find the warning you want to delete.")] string warningId,
            [Parameter("public"), Description("Whether to show the output publicly. Default: false")] bool showPublic = false
        )
        {
            if (warningId.Contains(' '))
            {
                warningId = warningId.Split(' ')[0];
            }

            long warnId;
            try
            {
                warnId = Convert.ToInt64(warningId);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Looks like your warning option was invalid! Give it another go?", ephemeral: true);
                return;
            }

            UserWarning warning = GetWarning(targetUser.Id, warnId);

            if (warning is null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.", ephemeral: true);
            else if (warning.Type == WarningType.Note)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That's a note, not a warning! Try using `/note delete` instead, or make sure you've got the right warning ID.", ephemeral: true);
            }
            else if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && warning.ModUserId != ctx.User.Id && warning.ModUserId != ctx.Client.CurrentUser.Id)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot edit or delete warnings that aren't issued by you or the bot!", ephemeral: true);
            }
            else
            {
                await ctx.DeferResponseAsync(ephemeral: !showPublic);

                bool success = await DelWarningAsync(warning, targetUser.Id);
                if (success)
                {
                    await LogChannelHelper.LogMessageAsync("mod",
                        new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Deleted} Warning deleted:" +
                            $"`{StringHelpers.Pad(warnId)}` (belonging to {targetUser.Mention}, deleted by {ctx.Member.Mention})")
                            .AddEmbed(await FancyWarnEmbedAsync(warning, true, 0xf03916, true, targetUser.Id))
                            .WithAllowedMentions(Mentions.None)
                    );

                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Deleted} Successfully deleted warning `{StringHelpers.Pad(warnId)}` (belonging to {targetUser.Mention})"));


                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to delete warning `{StringHelpers.Pad(warnId)}` from {targetUser.Mention}!\nPlease contact the bot author.", ephemeral: true);
                }
            }
        }

        [Command("editwarn")]
        [Description("Search for a warning and edit it!")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task EditWarnSlashCommand(SlashCommandContext ctx,
            [Parameter("user"), Description("The user to fetch a warning for.")] DiscordUser user,
            [SlashAutoCompleteProvider(typeof(WarningsAutocompleteProvider))][Parameter("warning"), Description("Type to search! Find the warning you want to edit.")] string warning,
            [Parameter("new_reason"), Description("The new reason for the warning")] string reason,
            [Parameter("public"), Description("Whether to show the output publicly. Default: false")] bool showPublic = false)
        {
            if (warning.Contains(' '))
            {
                warning = warning.Split(' ')[0];
            }

            long warnId;
            try
            {
                warnId = Convert.ToInt64(warning);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Looks like your warning option was invalid! Give it another go?", ephemeral: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You haven't given a new reason to set for the warning!", ephemeral: true);
                return;
            }

            var warningObject = GetWarning(user.Id, warnId);

            if (warningObject is null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.", ephemeral: true);
            else if (warningObject.Type == WarningType.Note)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That's a note, not a warning! Try using `/note edit` instead, or make sure you've got the right warning ID.", ephemeral: true);
            }
            else if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && warningObject.ModUserId != ctx.User.Id && warningObject.ModUserId != ctx.Client.CurrentUser.Id)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot edit or delete warnings that aren't issued by you or the bot!", ephemeral: true);
            }
            else
            {
                await ctx.DeferResponseAsync(ephemeral: !showPublic);

                await EditWarning(user, warnId, ctx.User, reason);

                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Information} Warning edited:" +
                        $"`{StringHelpers.Pad(warnId)}` (belonging to {user.Mention})")
                        .AddEmbed(await FancyWarnEmbedAsync(GetWarning(user.Id, warnId), true, userID: user.Id))
                );

                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Information} Successfully edited warning `{StringHelpers.Pad(warnId)}` (belonging to {user.Mention})")
                    .AddEmbed(await FancyWarnEmbedAsync(GetWarning(user.Id, warnId), userID: user.Id)));
            }
        }
        
        [Command("pardon")]
        [Description("Pardon a warning.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task PardonSlashCommand(SlashCommandContext ctx,
            [Parameter("user"), Description("The user to pardon a warning for.")] DiscordUser user,
            [SlashAutoCompleteProvider(typeof(UnpardonedWarningsAutocompleteProvider))][Parameter("warning"), Description("Type to search! Find the warning you want to pardon.")] string warning,
            [Parameter("public"), Description("Whether to show the output publicly. Default: false")] bool showPublic = false)
        {
            if (warning.Contains(' '))
            {
                warning = warning.Split(' ')[0];
            }

            long warnId;
            try
            {
                warnId = Convert.ToInt64(warning);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Looks like your warning option was invalid! Give it another go?", ephemeral: true);
                return;
            }

            var warningObject = GetWarning(user.Id, warnId);

            if (warningObject is null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.", ephemeral: true);
            else if (warningObject.Type == WarningType.Note)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That's a note, not a warning! Make sure you've got the right warning ID.", ephemeral: true);
            }
            else if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && warningObject.ModUserId != ctx.User.Id && warningObject.ModUserId != ctx.Client.CurrentUser.Id)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot edit or delete warnings that aren't issued by you or the bot!", ephemeral: true);
            }
            else
            {
                await ctx.DeferResponseAsync(ephemeral: !showPublic);
                
                if (warningObject.IsPardoned)
                {
                    await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} That warning has already been pardoned!");
                    return;
                }

                warningObject.IsPardoned = true;
                await Program.redis.HashSetAsync(warningObject.TargetUserId.ToString(), warningObject.WarningId.ToString(), JsonConvert.SerializeObject(warningObject));

                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Information} Warning pardoned:" +
                        $"`{StringHelpers.Pad(warnId)}` (belonging to {user.Mention})")
                        .AddEmbed(await FancyWarnEmbedAsync(GetWarning(user.Id, warnId), true, userID: user.Id))
                );

                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Information} Successfully pardoned warning `{StringHelpers.Pad(warnId)}` (belonging to {user.Mention})")
                    .AddEmbed(await FancyWarnEmbedAsync(GetWarning(user.Id, warnId), userID: user.Id, showPardonedInline: true)));
            }
        }
        
        [Command("unpardon")]
        [Description("Unpardon a warning.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task UnpardonSlashCommand(SlashCommandContext ctx,
            [Parameter("user"), Description("The user to unpardon a warning for.")] DiscordUser user,
            [SlashAutoCompleteProvider(typeof(PardonedWarningsAutocompleteProvider))][Parameter("warning"), Description("Type to search! Find the warning you want to unpardon.")] string warning,
            [Parameter("public"), Description("Whether to show the output publicly. Default: false")] bool showPublic = false)
        {
            if (warning.Contains(' '))
            {
                warning = warning.Split(' ')[0];
            }

            long warnId;
            try
            {
                warnId = Convert.ToInt64(warning);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Looks like your warning option was invalid! Give it another go?", ephemeral: true);
                return;
            }

            var warningObject = GetWarning(user.Id, warnId);

            if (warningObject is null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.", ephemeral: true);
            else if (warningObject.Type == WarningType.Note)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That's a note, not a warning! Make sure you've got the right warning ID.", ephemeral: true);
            }
            else if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && warningObject.ModUserId != ctx.User.Id && warningObject.ModUserId != ctx.Client.CurrentUser.Id)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot edit or delete warnings that aren't issued by you or the bot!", ephemeral: true);
            }
            else
            {
                await ctx.DeferResponseAsync(ephemeral: !showPublic);
                
                if (!warningObject.IsPardoned)
                {
                    await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} That warning isn't pardoned!");
                    return;
                }

                warningObject.IsPardoned = false;
                await Program.redis.HashSetAsync(warningObject.TargetUserId.ToString(), warningObject.WarningId.ToString(), JsonConvert.SerializeObject(warningObject));

                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Information} Warning unpardoned:" +
                        $"`{StringHelpers.Pad(warnId)}` (belonging to {user.Mention})")
                        .AddEmbed(await FancyWarnEmbedAsync(GetWarning(user.Id, warnId), true, userID: user.Id))
                );

                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Information} Successfully unpardoned warning `{StringHelpers.Pad(warnId)}` (belonging to {user.Mention})")
                    .AddEmbed(await FancyWarnEmbedAsync(GetWarning(user.Id, warnId), userID: user.Id, showPardonedInline: true)));
            }
        }

        [
            Command("warntextcmd"),
            Description("Issues a formal warning to a user."),
            TextAlias("warn", "wam", "warm"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task WarnCmd(
            TextCommandContext ctx,
            [Description("The user you are warning. Should be a mention or ID..")] DiscordUser targetUser,
            [RemainingText, Description("The reason for giving this warning.")] string reason = null
        )
        {
            // collision detection
            if (mostRecentWarning is not null && targetUser.Id == mostRecentWarning.TargetUserId)
            {
                var timeSinceLastWarning = DateTime.UtcNow.Subtract(mostRecentWarning.WarnTimestamp);
                if (timeSinceLastWarning <= TimeSpan.FromSeconds(5))
                {
                    await ctx.Message.DeleteAsync();
                    var resp = await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.BSOD} I was asked to warn someone twice within a few seconds, but I'm not going to. If I'm wrong, try again in a few seconds.");
                    await Task.Delay(5000);
                    await resp.DeleteAsync();
                    return;
                }
            }

            // this gets updated with a full warning object later, shove a stub in for now
            mostRecentWarning = new()
            {
                TargetUserId = targetUser.Id,
                ModUserId = ctx.User.Id,
                WarnTimestamp = DateTime.UtcNow,
                Stub = true // make it clear this isn't a real warning
            };

            DiscordMember targetMember;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
                if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && ((await GetPermLevelAsync(targetMember)) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
                {
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
                    return;
                }
            }
            catch
            {
                // do nothing :/
            }

            var reply = ctx.Message.ReferencedMessage;

            await ctx.Message.DeleteAsync();
            if (reason is null)
            {
                await ctx.Member.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} Reason must be included for the warning command to work.");
                return;
            }

            var messageBuild = new DiscordMessageBuilder()
                .WithContent($"{Program.cfgjson.Emoji.Warning} <@{targetUser.Id}> was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");

            if (reply is not null)
                messageBuild.WithReply(reply.Id, true, false);

            var msg = await ctx.Channel.SendMessageAsync(messageBuild);
            _ = await GiveWarningAsync(targetUser, ctx.User, reason, msg, ctx.Channel);
        }

        [
            Command("anonwarntextcmd"),
            TextAlias("anonwarn", "anonwam", "anonwarm"),
            Description("Issues a formal warning to a user from a private channel."),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task AnonWarnCmd(
            TextCommandContext ctx,
            [Description("The channel you wish for the warning message to appear in.")] DiscordChannel targetChannel,
            [Description("The user you are warning. Should be a mention or ID..")] DiscordUser targetUser,
            [RemainingText, Description("The reason for giving this warning.")] string reason = null
        )
        {
            // collision detection
            if (mostRecentWarning is not null && targetUser.Id == mostRecentWarning.TargetUserId)
            {
                var timeSinceLastWarning = DateTime.UtcNow.Subtract(mostRecentWarning.WarnTimestamp);
                if (timeSinceLastWarning <= TimeSpan.FromSeconds(5))
                {
                    var response = new DiscordInteractionResponseBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Error} {targetUser.Mention} was already warned a few seconds ago, refusing yours to prevent collisions. If your warning is unrelated, try again in a few seconds.")
                            .AsEphemeral(true);
                    if (!mostRecentWarning.Stub)
                        response.AddEmbed(await FancyWarnEmbedAsync(mostRecentWarning, detailed: true));

                    await ctx.RespondAsync(response);
                    return;
                }
            }

            // this gets updated with a full warning object later, shove a stub in for now
            mostRecentWarning = new()
            {
                TargetUserId = targetUser.Id,
                ModUserId = ctx.User.Id,
                WarnTimestamp = DateTime.UtcNow,
                Stub = true // make it clear this isn't a real warning
            };

            DiscordMember targetMember;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
                if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && ((await GetPermLevelAsync(targetMember)) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
                {
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
                    return;
                }
            }
            catch
            {
                // do nothing :/
            }

            await ctx.Message.DeleteAsync();
            if (reason is null)
            {
                await ctx.Member.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} Reason must be included for the warning command to work.");
                return;
            }
            DiscordMessage msg = await targetChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} {targetUser.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
            await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} {targetUser.Mention} was warned in {targetChannel.Mention}: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
            _ = await GiveWarningAsync(targetUser, ctx.User, reason, msg, ctx.Channel);
        }

        [
            Command("warningstextcmd"),
            TextAlias("warnings", "infractions", "warnfractions", "wammings", "wamfractions"),
            Description("Shows a list of warnings that a user has been given. For more in-depth information, use the 'warnlookup' command."),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task WarningCmd(
            TextCommandContext ctx,
            [Description("The user you want to look up warnings for. Should be a mention or ID..")] DiscordUser targetUser = null
        )
        {
            if (targetUser is null)
                targetUser = ctx.User;

            await ctx.RespondAsync(null, await GenerateWarningsEmbedAsync(targetUser));
        }

        [
            Command("delwarntextcmd"),
            TextAlias("delwarn", "delwarm", "delwam", "deletewarn", "delwarning", "deletewarning"),
            Description("Delete a warning that was issued by mistake or later became invalid."),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task DelwarnCmd(
            TextCommandContext ctx,
            [Description("The user you're removing a warning from. Should be a mention or ID..")] DiscordUser targetUser,
            [Description("The ID of the warning you want to delete.")] long warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);
            if (warning is null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else if (warning.Type == WarningType.Note)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That's a note, not a warning! Try using `/note delete` instead, or make sure you've got the right warning ID.");
            }
            else if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && warning.ModUserId != ctx.User.Id && warning.ModUserId != ctx.Client.CurrentUser.Id)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot edit or delete warnings that aren't issued by you or the bot!");
            }
            else
            {
                bool success = await DelWarningAsync(warning, targetUser.Id);
                if (success)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Deleted} Successfully deleted warning `{StringHelpers.Pad(warnId)}` (belonging to {targetUser.Mention})");

                    await LogChannelHelper.LogMessageAsync("mod",
                        new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Deleted} Warning deleted:" +
                            $"`{StringHelpers.Pad(warnId)}` (belonging to {targetUser.Mention}, deleted by {ctx.Member.Mention})")
                            .AddEmbed(await FancyWarnEmbedAsync(warning, true, 0xf03916, true, targetUser.Id))
                            .WithAllowedMentions(Mentions.None)
                        );
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to delete warning `{StringHelpers.Pad(warnId)}` from {targetUser.Mention}!\nPlease contact the bot author.");
                }
            }
        }

        [
            Command("warnlookuptextcmd"),
            Description("Looks up information about a warning. Shows only publicly available information."),
            TextAlias("warnlookup", "warning", "warming", "waming", "wamming", "lookup", "lookylooky", "peek", "investigate", "what-did-i-do-wrong-there", "incident"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task WarnlookupCmd(
            TextCommandContext ctx,
            [Description("The user you're looking at a warning for. Should be a mention or ID..")] DiscordUser targetUser,
            [Description("The ID of the warning you want to see")] long warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);
            if (warning is null || warning.Type == WarningType.Note)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else
                await ctx.RespondAsync(null, await FancyWarnEmbedAsync(warning, userID: targetUser.Id));
        }

        [
            Command("warndetailstextcmd"),
            TextAlias("warndetails", "warninfo", "waminfo", "wamdetails", "warndetail", "wamdetail"),
            Description("Check the details of a warning in depth. Shows extra information (Such as responsible Mod) that may not be wanted to be public."),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task WarnDetailsCmd(
            TextCommandContext ctx,
            [Description("The user you're looking up detailed warn information for. Should be a mention or ID..")] DiscordUser targetUser,
            [Description("The ID of the warning you're looking at in detail.")] long warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);

            if (warning is null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else if (warning.Type == WarningType.Note)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That's a note, not a warning! Try using `/note details` instead, or make sure you've got the right warning ID.");
            }
            else
                await ctx.RespondAsync(null, await FancyWarnEmbedAsync(warning, true, userID: targetUser.Id));

        }

        [
            Command("editwarntextcmd"),
            TextAlias("editwarn", "warnedit", "editwarning"),
            Description("Edit the reason of an existing warning.\n" +
                "The Moderator who is editing the reason will become responsible for the case."),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task EditwarnCmd(
            TextCommandContext ctx,
            [Description("The user you're editing a warning for. Should be a mention or ID..")] DiscordUser targetUser,
            [Description("The ID of the warning you want to edit.")] long warnId,
            [RemainingText, Description("The new reason for the warning.")] string newReason)
        {
            if (string.IsNullOrWhiteSpace(newReason))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You haven't given a new reason to set for the warning!");
                return;
            }

            await ctx.RespondAsync("Processing your request...");
            var msg = await ctx.GetResponseAsync();
            var warning = GetWarning(targetUser.Id, warnId);
            if (warning is null)
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else if (warning.Type == WarningType.Note)
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} That's a note, not a warning! Try using `/note edit` instead, or make sure you've got the right warning ID.");
            }
            else if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && warning.ModUserId != ctx.User.Id && warning.ModUserId != ctx.Client.CurrentUser.Id)
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot edit or delete warnings that aren't issued by you or the bot!");
            }
            else
            {
                await EditWarning(targetUser, warnId, ctx.User, newReason);
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Information} Successfully edited warning `{StringHelpers.Pad(warnId)}` (belonging to {targetUser.Mention})",
                    await FancyWarnEmbedAsync(GetWarning(targetUser.Id, warnId), userID: targetUser.Id));

                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Information} Warning edited:" +
                        $"`{StringHelpers.Pad(warnId)}` (belonging to {targetUser.Mention})")
                        .AddEmbed(await FancyWarnEmbedAsync(GetWarning(targetUser.Id, warnId), true, userID: targetUser.Id))
                    );
            }
        }

        [Command("mostwarningstextcmd")]
        [TextAlias("mostwarnings")]
        [Description("Who has the most warnings???")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task MostWarningsCmd(TextCommandContext ctx)
        {
            await DiscordHelpers.SafeTyping(ctx.Channel);

            var server = Program.redisConnection.GetServer(Program.redisConnection.GetEndPoints()[0]);
            var keys = server.Keys();

            Dictionary<string, int> counts = new();
            foreach (var key in keys)
            {
                if (ulong.TryParse(key.ToString(), out ulong number))
                {
                    counts[key.ToString()] = (await Program.redis.HashGetAllAsync(key)).Count(x => JsonConvert.DeserializeObject<UserWarning>(x.Value.ToString()).Type == WarningType.Warning);
                }
            }

            List<KeyValuePair<string, int>> myList = counts.ToList();
            myList.Sort(
                delegate (KeyValuePair<string, int> pair1,
                KeyValuePair<string, int> pair2)
                {
                    return pair1.Value.CompareTo(pair2.Value);
                }
            );

            var user = await ctx.Client.GetUserAsync(Convert.ToUInt64(myList.Last().Key));
            await ctx.RespondAsync($":thinking: The user with the most warnings is **{DiscordHelpers.UniqueUsername(user)}** with a total of **{myList.Last().Value} warnings!**\nThis includes users who have left or been banned.");
        }

        [Command("mostwarningsdaytextcmd")]
        [TextAlias("mostwarningsday")]
        [Description("Which day has the most warnings???")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task MostWarningsDayCmd(TextCommandContext ctx)
        {
            await DiscordHelpers.SafeTyping(ctx.Channel);

            var server = Program.redisConnection.GetServer(Program.redisConnection.GetEndPoints()[0]);
            var keys = server.Keys();

            Dictionary<string, int> counts = new();
            Dictionary<string, int> noAutoCounts = new();

            foreach (var key in keys)
            {
                if (ulong.TryParse(key.ToString(), out ulong number))
                {
                    var warningsOutput = (await Program.redis.HashGetAllAsync(key.ToString())).ToDictionary(
                        x => x.Name.ToString(),
                        x => JsonConvert.DeserializeObject<UserWarning>(x.Value)
                    );

                    foreach (var warning in warningsOutput)
                    {
                        if (warning.Value.Type != WarningType.Warning) continue;

                        var day = warning.Value.WarnTimestamp.ToString("yyyy-MM-dd");
                        if (!counts.ContainsKey(day))
                        {
                            counts[day] = 1;
                        }
                        else
                        {
                            counts[day] += 1;
                        }
                        if (warning.Value.ModUserId != 159985870458322944 && warning.Value.ModUserId != Program.discord.CurrentUser.Id)
                        {
                            if (!noAutoCounts.ContainsKey(day))
                            {
                                noAutoCounts[day] = 1;
                            }
                            else
                            {
                                noAutoCounts[day] += 1;
                            }
                        }
                    }
                }
            }

            List<KeyValuePair<string, int>> countList = counts.ToList();
            countList.Sort(
                delegate (KeyValuePair<string, int> pair1,
                KeyValuePair<string, int> pair2)
                {
                    return pair1.Value.CompareTo(pair2.Value);
                }
            );

            List<KeyValuePair<string, int>> noAutoCountList = noAutoCounts.ToList();
            noAutoCountList.Sort(
                delegate (KeyValuePair<string, int> pair1,
                KeyValuePair<string, int> pair2)
                {
                    return pair1.Value.CompareTo(pair2.Value);
                }
            );

            await ctx.RespondAsync($":thinking: As far as I can tell, the day with the most warnings issued was **{countList.Last().Key}** with a total of **{countList.Last().Value} warnings!**" +
                $"\nExcluding automatic warnings, the most was on **{noAutoCountList.Last().Key}** with a total of **{noAutoCountList.Last().Value}** warnings!");
        }

        [Command("revoke"), Description("Revoke a warning. Reply to the chat message for the warning to revoke when using this!")]
        [TextAlias("undo")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task RevokeWarningCommand(TextCommandContext ctx)
        {
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it...");
            var msg = await ctx.GetResponseAsync();

            var reply = ctx.Message.ReferencedMessage;

            if (reply is null)
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} Please reply to the warning message to delete!");
                return;
            }

            if (reply.Author.Id != Program.discord.CurrentUser.Id || (!Constants.RegexConstants.warn_msg_rx.IsMatch(reply.Content) && (!Constants.RegexConstants.auto_warn_msg_rx.IsMatch(reply.Content))))
            {
                // this isnt a warning message
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} That reply doesn't look like a warning message! Please reply to the warning message to delete.");
                return;
            }

            // Collect data from message
            var userId = Constants.RegexConstants.user_rx.Match(reply.Content).Groups[1].ToString();
            var reason = Constants.RegexConstants.warn_msg_rx.Match(reply.Content).Groups[1].Value;
            if (string.IsNullOrEmpty(reason))
                reason = Constants.RegexConstants.auto_warn_msg_rx.Match(reply.Content).Groups[1].Value;
            var userWarnings = (await Program.redis.HashGetAllAsync(userId));

            // Try to match against user warnings;
            // match warnings that have a reason that exactly matches the reason in the msg being replied to,
            // and that are explicitly warnings (WarningType.Warning), not notes

            UserWarning warning = null;

            var matchingWarnings = userWarnings.Where(x =>
            {
                var warn = JsonConvert.DeserializeObject<UserWarning>(x.Value);
                return warn.WarnReason == reason && warn.Type == WarningType.Warning;
            }).Select(x => JsonConvert.DeserializeObject<UserWarning>(x.Value)).ToList();

            if (matchingWarnings.Count > 1)
            {
                bool foundMatch = false;
                foreach (var match in matchingWarnings)
                {
                    // timestamps of warning msg & warning are within a minute, this is most likely the correct warning
                    if (reply.Timestamp.ToUniversalTime() - match.WarnTimestamp.ToUniversalTime() < TimeSpan.FromMinutes(1))
                    {
                        warning = match;
                        foundMatch = true;
                        break;
                    }
                }
                if (!foundMatch)
                {
                    await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} I couldn't identify that warning! Please report this to bot maintainers.");
                    return;
                }
            }
            else if (matchingWarnings.Count < 1)
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} I couldn't identify that warning! Please report this to bot maintainers.");
                return;
            }
            else
            {
                warning = matchingWarnings.First();
            }

            if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && warning.ModUserId != ctx.User.Id && warning.ModUserId != ctx.Client.CurrentUser.Id)
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot edit or delete warnings that aren't issued by you or the bot!");
            }
            else
            {
                bool success = await DelWarningAsync(warning, warning.TargetUserId);
                if (success)
                {
                    await msg.ModifyAsync($"{Program.cfgjson.Emoji.Deleted} Successfully deleted warning `{StringHelpers.Pad(warning.WarningId)}` (belonging to <@{warning.TargetUserId}>)");

                    await LogChannelHelper.LogMessageAsync("mod",
                        new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Deleted} Warning deleted:" +
                                         $"`{StringHelpers.Pad(warning.WarningId)}` (belonging to <@{warning.TargetUserId}>, deleted by {ctx.Member.Mention})")
                            .AddEmbed(await FancyWarnEmbedAsync(warning, true, 0xf03916, true, warning.TargetUserId))
                            .WithAllowedMentions(Mentions.None)
                    );
                }
                else
                {
                    await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} Failed to delete warning `{StringHelpers.Pad(warning.WarningId)}` from <@{warning.TargetUserId}>!\nPlease report this to bot maintainers.");
                }
            }
        }
    }
}