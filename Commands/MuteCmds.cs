namespace Cliptok.Commands
{
    public class MuteCmds
    {
        [Command("mute")]
        [Description("Mute a user, temporarily or permanently.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task MuteSlashCommand(
            SlashCommandContext ctx,
            [Parameter("user"), Description("The user you wish to mute.")] DiscordUser targetUser,
            [Parameter("reason"), Description("The reason for the mute.")] string reason,
            [Parameter("time"), Description("The length of time to mute for.")] string time = ""
        )
        {
            // collision detection
            if (MuteHelpers.MostRecentMute is not null && MuteHelpers.MostRecentMute.MemberId == targetUser.Id)
            {
                var timeSinceLastBan = DateTime.UtcNow.Subtract((DateTime)MuteHelpers.MostRecentMute.ActionTime);
                if (timeSinceLastBan <= TimeSpan.FromSeconds(5))
                {
                    var response = new DiscordInteractionResponseBuilder()
        .WithContent($"{Program.cfgjson.Emoji.Error} {targetUser.Mention} was already muted a few seconds ago, refusing yours to prevent collisions. If you meant to mute them again, try again in a few seconds.")
        .AsEphemeral(true);
                    if (!MuteHelpers.MostRecentMute.Stub)
                        response.AddEmbed(await MuteHelpers.MuteStatusEmbed(targetUser, ctx.Guild));

                    await ctx.RespondAsync(response);
                    return;
                }
            }

            MuteHelpers.MostRecentMute = new()
            {
                MemberId = targetUser.Id,
                ActionTime = ctx.Interaction.CreationTimestamp.DateTime,
                ModId = ctx.User.Id,
                ServerId = ctx.Guild.Id,
                Reason = reason,
                Stub = true
            };

            await ctx.DeferResponseAsync(ephemeral: true);
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
                    muteDuration = HumanDateParser.HumanDateParser.Parse(time).Subtract(ctx.Interaction.CreationTimestamp.LocalDateTime);
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

        [Command("unmute")]
        [Description("Unmute a user.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task UnmuteSlashCommand(
            SlashCommandContext ctx,
            [Parameter("user"), Description("The user you wish to mute.")] DiscordUser targetUser,
            [Parameter("reason"), Description("The reason for the unmute.")] string reason = "No reason specified."
            )
        {
            await ctx.DeferResponseAsync(ephemeral: false);

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
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Information} Successfully unmuted **{DiscordHelpers.UniqueUsername(targetUser)}**."));
            }
            else
                try
                {
                    await MuteHelpers.UnmuteUserAsync(targetUser, reason, true, ctx.User);
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Warning} According to Discord that user is not muted, but I tried to unmute them anyway. Hope it works."));
                }
                catch (Exception e)
                {
                    Program.discord.Logger.LogError(e, "An error occurred unmuting {user}", targetUser.Id);
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be muted, *and* an error occurred while attempting to unmute them anyway. Please contact the bot owner, the error has been logged."));
                }
        }

        [Command("tqsmute")]
        [Description("Temporarily mute a user in tech support channels.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TechnicalQueriesSlayer)]
        public async Task TqsMuteSlashCommand(
            CommandContext ctx,
            [Parameter("user"), Description("The user to mute.")] DiscordUser targetUser,
            [Parameter("reason"), Description("The reason for the mute.")] string reason)
        {
            if (ctx is SlashCommandContext)
                await ctx.As<SlashCommandContext>().DeferResponseAsync(ephemeral: true);
            else
                await ctx.As<TextCommandContext>().Message.DeleteAsync();

            // only work if TQS mute role is configured
            if (Program.cfgjson.TqsMutedRole == 0)
            {
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} TQS mutes are not configured, so this command does nothing. Please contact the bot maintainer if this is unexpected."));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} TQS mutes are not configured, so this command does nothing. Please contact the bot maintainer if this is unexpected.");
                return;
            }

            // Only allow usage in #tech-support, #tech-support-forum, and their threads
            if (ctx.Channel.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Id != Program.cfgjson.SupportForumId &&
                ctx.Channel.Parent.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Parent.Id != Program.cfgjson.SupportForumId)
            {
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} This command can only be used in <#{Program.cfgjson.TechSupportChannel}>, <#{Program.cfgjson.SupportForumId}>, and threads in those channels!"));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command can only be used in <#{Program.cfgjson.TechSupportChannel}>, <#{Program.cfgjson.SupportForumId}>, and threads in those channels!");
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
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, that user is already muted."));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, that user is already muted.");
                return;
            }

            // Check if user to be muted is staff or TQS, and disallow if so
            if (targetMember != default && (await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TechnicalQueriesSlayer && ((await GetPermLevelAsync(targetMember)) >= ServerPermLevel.TechnicalQueriesSlayer || targetMember.IsBot))
            {
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, you cannot mute other TQS or staff members."));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, you cannot mute other TQS or staff members.");
                return;
            }

            // mute duration is static for TQS mutes
            TimeSpan muteDuration = TimeSpan.FromHours(Program.cfgjson.TqsMuteDurationHours);

            await MuteHelpers.MuteUserAsync(targetUser, reason, ctx.User.Id, ctx.Guild, ctx.Channel, muteDuration, true, true);
            if (ctx is SlashCommandContext)
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Done. Please open a modmail thread for this user if you haven't already!"));
        }

        [Command("muteinfo")]
        [Description("Show information about the mute for a user.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task MuteInfoSlashCommand(
            SlashCommandContext ctx,
            [Parameter("user"), Description("The user whose mute information to show.")] DiscordUser targetUser,
            [Parameter("public"), Description("Whether to show the output publicly. Default: false")] bool isPublic = false)
        {
            await ctx.RespondAsync(embed: await MuteHelpers.MuteStatusEmbed(targetUser, ctx.Guild), ephemeral: !isPublic);
        }

        [Command("unmutetextcmd")]
        [TextAlias("unmute", "umute")]
        [Description("Unmutes a previously muted user, typically ahead of the standard expiration time. See also: mute")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task UnmuteCmd(TextCommandContext ctx, [Description("The user you're trying to unmute.")] DiscordUser targetUser, string reason = "No reason provided.")
        {
            reason = $"[Manual unmute by {DiscordHelpers.UniqueUsername(ctx.User)}]: {reason}";

            // todo: store per-guild
            DiscordRole mutedRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.MutedRole);
            DiscordRole tqsMutedRole = default;
            if (Program.cfgjson.TqsMutedRole != 0)
                tqsMutedRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.TqsMutedRole);

            DiscordMember member = default;
            try
            {
                member = await ctx.Guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException ex)
            {
                Program.discord.Logger.LogWarning(eventId: Program.CliptokEventID, exception: ex, message: "Failed to unmute {user} in {server} because they weren't in the server.", $"{DiscordHelpers.UniqueUsername(targetUser)}", ctx.Guild.Name);
            }

            if ((await Program.db.HashExistsAsync("mutes", targetUser.Id)) || (member != default && (member.Roles.Contains(mutedRole) || member.Roles.Contains(tqsMutedRole))))
            {
                await MuteHelpers.UnmuteUserAsync(targetUser, reason, true, ctx.User);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Information} Successfully unmuted **{DiscordHelpers.UniqueUsername(targetUser)}**.");
            }
            else
                try
                {
                    await MuteHelpers.UnmuteUserAsync(targetUser, reason, true, ctx.User);
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} According to Discord that user is not muted, but I tried to unmute them anyway. Hope it works.");
                }
                catch (Exception e)
                {
                    Program.discord.Logger.LogError(e, "An error occurred unmuting {user}", targetUser.Id);
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be muted, *and* an error occurred while attempting to unmute them anyway. Please contact the bot owner, the error has been logged.");
                }
        }

        [Command("mutetextcmd")]
        [TextAlias("mute")]
        [Description("Mutes a user, preventing them from sending messages until they're unmuted. See also: unmute")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task MuteCmd(
            TextCommandContext ctx, [Description("The user you're trying to mute")] DiscordUser targetUser,
            [RemainingText, Description("Combined argument for the time and reason for the mute. For example '1h rule 7' or 'rule 10'")] string timeAndReason = "No reason specified."
        )
        {
            // collision detection
            if (MuteHelpers.MostRecentMute is not null && targetUser.Id == MuteHelpers.MostRecentMute.MemberId)
            {
                var timeSinceLastWarning = DateTime.UtcNow.Subtract((DateTime)MuteHelpers.MostRecentMute.ActionTime);
                if (timeSinceLastWarning <= TimeSpan.FromSeconds(5))
                {
                    await ctx.Message.DeleteAsync();
                    var resp = await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.BSOD} I was asked to mute someone twice within a few seconds, but I'm not going to. If I'm wrong, try again in a few seconds.");
                    await Task.Delay(5000);
                    await resp.DeleteAsync();
                    return;
                }
            }

            MuteHelpers.MostRecentMute = new()
            {
                MemberId = targetUser.Id,
                ActionTime = DateTime.UtcNow,
                ModId = ctx.User.Id,
                ServerId = ctx.Guild.Id,
                Reason = timeAndReason,
                Stub = true
            };

            DiscordMember targetMember = default;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // is this worth logging?
            }

            if (targetMember != default && ((await GetPermLevelAsync(ctx.Member))) == ServerPermLevel.TrialModerator && ((await GetPermLevelAsync(targetMember)) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
            {
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
                return;
            }

            await ctx.Message.DeleteAsync();
            bool timeParsed = false;

            TimeSpan muteDuration = default;
            string possibleTime = timeAndReason.Split(' ').First();
            string reason = timeAndReason;

            try
            {
                muteDuration = HumanDateParser.HumanDateParser.Parse(possibleTime).Subtract(ctx.Message.Timestamp.DateTime);
                timeParsed = true;
            }
            catch
            {
                // keep default
            }

            if (timeParsed)
            {
                int i = reason.IndexOf(" ") + 1;
                reason = reason[i..];
            }

            if (timeParsed && possibleTime == reason)
                reason = "No reason specified.";

            _ = MuteHelpers.MuteUserAsync(targetUser, reason, ctx.User.Id, ctx.Guild, ctx.Channel, muteDuration, true);
        }
    }
}