using static Cliptok.Helpers.WarningHelpers;

namespace Cliptok.Commands.InteractionCommands
{
    internal class WarningInteractions
    {
        [Command("warn")]
        [Description("Formally warn a user, usually for breaking the server rules.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermissions.ModerateMembers)]
        public async Task WarnSlashCommand(SlashCommandContext ctx,
         [Parameter("user"), Description("The user to warn.")] DiscordUser user,
         [Parameter("reason"), Description("The reason they're being warned.")] string reason,
         [Parameter("reply_msg_id"), Description("The ID of a message to reply to, must be in the same channel.")] string replyMsgId = "0",
         [Parameter("channel"), Description("The channel to warn the user in, implied if not supplied.")] DiscordChannel channel = null
        )
        {
            // Initial response to avoid the 3 second timeout, will edit later.
            var eout = new DiscordInteractionResponseBuilder().AsEphemeral(true);
            await ctx.RespondAsync(eout);

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

        [Command("transfer_warnings")]
        [Description("Transfer warnings from one user to another.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [SlashRequireHomeserverPerm(ServerPermLevel.Moderator)]
        [RequirePermissions(DiscordPermissions.ModerateMembers)]
        public async Task TransferWarningsSlashCommand(SlashCommandContext ctx,
            [Parameter("source_user"), Description("The user currently holding the warnings.")] DiscordUser sourceUser,
            [Parameter("target_user"), Description("The user receiving the warnings.")] DiscordUser targetUser,
            [Parameter("merge"), Description("Whether to merge the source user's warnings and the target user's warnings.")] bool merge = false,
            [Parameter("force_override"), Description("DESTRUCTIVE OPERATION: Whether to OVERRIDE and DELETE the target users existing warnings.")] bool forceOverride = false
        )
        {
            await ctx.DeferResponseAsync(); // TODO(#202): how do you make this ephemeral?

            if (sourceUser == targetUser)
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} The source and target users cannot be the same!"));
                return;
            }

            var sourceWarnings = await Program.db.HashGetAllAsync(sourceUser.Id.ToString());
            var targetWarnings = await Program.db.HashGetAllAsync(targetUser.Id.ToString());

            if (sourceWarnings.Length == 0)
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} The source user has no warnings to transfer.").AddEmbed(await GenerateWarningsEmbedAsync(sourceUser)));
                return;
            }
            else if (merge)
            {
                foreach (var warning in sourceWarnings)
                {
                    await Program.db.HashSetAsync(targetUser.Id.ToString(), warning.Name, warning.Value);
                }
                await Program.db.KeyDeleteAsync(sourceUser.Id.ToString());
            }
            else if (targetWarnings.Length > 0 && !forceOverride)
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Warning} **CAUTION**: The target user has warnings.\n\n" +
                    $"If you are sure you want to **OVERRIDE** and **DELETE** these warnings, please consider the consequences before adding `force_override: True` to the command.\nIf you wish to **NOT** override the target's warnings, please use `merge: True` instead.")
                    .AddEmbed(await GenerateWarningsEmbedAsync(targetUser)));
                return;
            }
            else if (targetWarnings.Length > 0 && forceOverride)
            {
                await Program.db.KeyDeleteAsync(targetUser.Id.ToString());
                await Program.db.KeyRenameAsync(sourceUser.Id.ToString(), targetUser.Id.ToString());
            }
            else
            {
                await Program.db.KeyRenameAsync(sourceUser.Id.ToString(), targetUser.Id.ToString());
            }

            string operationText = "";
            if (merge)
                operationText = "merge ";
            else if (forceOverride)
                operationText = "force ";
            await LogChannelHelper.LogMessageAsync("mod",
                new DiscordMessageBuilder()
                    .WithContent($"{Program.cfgjson.Emoji.Information} Warnings from {sourceUser.Mention} were {operationText}transferred to {targetUser.Mention} by `{DiscordHelpers.UniqueUsername(ctx.User)}`")
                    .AddEmbed(await GenerateWarningsEmbedAsync(targetUser))
            );
            await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Successfully {operationText}transferred warnings from {sourceUser.Mention} to {targetUser.Mention}!"));
        }

        internal partial class WarningsAutocompleteProvider : IAutoCompleteProvider
        {
            public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext ctx)
            {
                var list = new List<DiscordAutoCompleteChoice>();

                var useroption = ctx.Options.FirstOrDefault(x => x.Name == "user");
                if (useroption == default)
                {
                    return list;
                }

                var user = await ctx.Client.GetUserAsync((ulong)useroption.Value);

                var warnings = Program.db.HashGetAll(user.Id.ToString())
                    .Where(x => JsonConvert.DeserializeObject<UserWarning>(x.Value).Type == WarningType.Warning).ToDictionary(
                   x => x.Name.ToString(),
                  x => JsonConvert.DeserializeObject<UserWarning>(x.Value)
                 ).OrderByDescending(x => x.Value.WarningId);

                foreach (var warning in warnings)
                {
                    if (list.Count >= 25)
                        break;

                    string warningString = $"{StringHelpers.Pad(warning.Value.WarningId)} - {StringHelpers.Truncate(warning.Value.WarnReason, 29, true)} - {TimeHelpers.TimeToPrettyFormat(DateTime.Now - warning.Value.WarnTimestamp, true)}";

                    var focusedOption = ctx.Options.FirstOrDefault(option => option.Focused);
                    if (focusedOption is not null) // TODO(#202): is this right?
                        if (warning.Value.WarnReason.Contains((string)focusedOption.Value) || warningString.ToLower().Contains(focusedOption.Value.ToString().ToLower()))
                            list.Add(new DiscordAutoCompleteChoice(warningString, StringHelpers.Pad(warning.Value.WarningId)));
                }

                return list;
                //return Task.FromResult((IEnumerable<DiscordAutoCompleteChoice>)list);
            }
        }

        [Command("warndetails")]
        [Description("Search for a warning and return its details.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermissions.ModerateMembers)]
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
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermissions.ModerateMembers)]
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
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermissions.ModerateMembers)]
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
    }
}