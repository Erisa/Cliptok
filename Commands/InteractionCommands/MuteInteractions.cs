namespace Cliptok.Commands.InteractionCommands
{
    internal class MuteInteractions : ApplicationCommandModule
    {
        [SlashCommand("mute", "Mute a user, temporarily or permanently.")]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [SlashCommandPermissions(DiscordPermissions.ModerateMembers)]
        public async Task MuteSlashCommand(
            InteractionContext ctx,
            [Option("user", "The user you wish to mute.")] DiscordUser targetUser,
            [Option("reason", "The reason for the mute.")] string reason,
            [Option("time", "The length of time to mute for.")] string time = ""
        )
        {
            await ctx.DeferAsync(ephemeral: true);
            DiscordMember targetMember = default;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // is this worth logging?
            }

            if (targetMember != default && (await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && ((await GetPermLevelAsync(targetMember)) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
            {
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
                return;
            }

            TimeSpan muteDuration = default;

            if (time != "")
            {
                try
                {
                    muteDuration = HumanDateParser.HumanDateParser.Parse(time).Subtract(ctx.Interaction.CreationTimestamp.DateTime);
                }
                catch
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} Failed to parse time argument."));
                    throw;
                }
            }

            await MuteHelpers.MuteUserAsync(targetUser, reason, ctx.User.Id, ctx.Guild, ctx.Channel, muteDuration, true);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Command completed successfully."));
        }

        [SlashCommand("unmute", "Unmute a user.")]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [SlashCommandPermissions(DiscordPermissions.ModerateMembers)]
        public async Task UnmuteSlashCommand(
            InteractionContext ctx,
            [Option("user", "The user you wish to mute.")] DiscordUser targetUser,
            [Option("reason", "The reason for the unmute.")] string reason = "No reason specified."
            )
        {
            await ctx.DeferAsync(ephemeral: false);

            reason = $"[Manual unmute by {DiscordHelpers.UniqueUsername(ctx.User)}]: {reason}";

            // todo: store per-guild
            DiscordRole mutedRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.MutedRole);

            DiscordMember member = default;
            try
            {
                member = await ctx.Guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException ex)
            {
                Program.discord.Logger.LogWarning(eventId: Program.CliptokEventID, exception: ex, message: "Failed to unmute {user} in {server} because they weren't in the server.", $"{DiscordHelpers.UniqueUsername(targetUser)}", ctx.Guild.Name);
            }

            if ((await Program.db.HashExistsAsync("mutes", targetUser.Id)) || (member != default && member.Roles.Contains(mutedRole)))
            {
                await MuteHelpers.UnmuteUserAsync(targetUser, reason, true, ctx.User);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Information} Successfully unmuted **{DiscordHelpers.UniqueUsername(targetUser)}**."));
            }
            else
                try
                {
                    await MuteHelpers.UnmuteUserAsync(targetUser, reason, true, ctx.User);
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Warning} According to Discord that user is not muted, but I tried to unmute them anyway. Hope it works."));
                }
                catch (Exception e)
                {
                    Program.discord.Logger.LogError(e, "An error occurred unmuting {user}", targetUser.Id);
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be muted, *and* an error occurred while attempting to unmute them anyway. Please contact the bot owner, the error has been logged."));
                }
        }

        [SlashCommand("tqsmute", "Temporarily mute a user in tech support channels.")]
        [SlashRequireHomeserverPerm(ServerPermLevel.TechnicalQueriesSlayer)]
        public async Task TqsMuteSlashCommand(
            InteractionContext ctx,
            [Option("user", "The user to mute.")] DiscordUser targetUser,
            [Option("reason", "The reason for the mute.")] string reason)
        {
            await ctx.DeferAsync(ephemeral: true);

            // only work if TQS mute role is configured
            if (Program.cfgjson.TqsMutedRole == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} TQS mutes are not configured, so this command does nothing. Please contact the bot maintainer if this is unexpected."));
                return;
            }

            // Only allow usage in #tech-support, #tech-support-forum, and their threads
            if (ctx.Channel.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Id != Program.cfgjson.SupportForumId &&
                ctx.Channel.Parent.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Parent.Id != Program.cfgjson.SupportForumId)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} This command can only be used in <#{Program.cfgjson.TechSupportChannel}>, <#{Program.cfgjson.SupportForumId}>, and threads in those channels!"));
                return;
            }

            // Check if the user is already muted; disallow TQS-mute if so

            DiscordRole mutedRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.MutedRole);
            DiscordRole tqsMutedRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.TqsMutedRole);

            // Get member
            DiscordMember targetMember = default;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // blah
            }

            if (await Program.db.HashExistsAsync("mutes", targetUser.Id) || (targetMember is not null && (targetMember.Roles.Contains(mutedRole) || targetMember.Roles.Contains(tqsMutedRole))))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, that user is already muted."));
                return;
            }

            // Check if user to be muted is staff or TQS, and disallow if so
            if (targetMember != default && (await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TechnicalQueriesSlayer && ((await GetPermLevelAsync(targetMember)) >= ServerPermLevel.TechnicalQueriesSlayer || targetMember.IsBot))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, you cannot mute other TQS or staff members."));
                return;
            }

            // mute duration is static for TQS mutes
            TimeSpan muteDuration = TimeSpan.FromHours(Program.cfgjson.TqsMuteDurationHours);

            await MuteHelpers.MuteUserAsync(targetUser, reason, ctx.User.Id, ctx.Guild, ctx.Channel, muteDuration, true, true);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Done. Please open a modmail thread for this user if you haven't already!"));
        }
    }
}
