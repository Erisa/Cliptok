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

            // Only allow usage in #tech-support, #tech-support-forum, and their threads + #bot-commands
            if (ctx.Channel.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Id != Program.cfgjson.SupportForumId &&
                ctx.Channel.Parent.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Parent.Id != Program.cfgjson.SupportForumId &&
                ctx.Channel.Id != Program.cfgjson.BotCommandsChannel)
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

            if (await Program.redis.HashExistsAsync("mutes", targetUser.Id) || (targetMember is not null && (targetMember.Roles.Contains(mutedRole) || targetMember.Roles.Contains(tqsMutedRole))))
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
        
        [Command("tqsunmute")]
        [TextAlias("tqs-unmute", "untqsmute")]
        [Description("Removes a TQS Mute from a previously TQS-muted user. See also: tqsmute")]
        [AllowedProcessors(typeof(TextCommandProcessor), typeof(SlashCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TechnicalQueriesSlayer)]
        public async Task TqsUnmuteCmd(CommandContext ctx, [Parameter("user"), Description("The user you're trying to unmute.")] DiscordUser targetUser, [Description("The reason for the unmute.")] string reason)
        {
            if (ctx is SlashCommandContext)
                await ctx.As<SlashCommandContext>().DeferResponseAsync();
            
            // only work if TQS mute role is configured
            if (Program.cfgjson.TqsMutedRole == 0)
            {
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} TQS mutes are not configured, so this command does nothing. Please contact the bot maintainer if this is unexpected."));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} TQS mutes are not configured, so this command does nothing. Please contact the bot maintainer if this is unexpected.");
                return;
            }
            
            // Only allow usage in #tech-support, #tech-support-forum, and their threads + #bot-commands
            if (ctx.Channel.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Id != Program.cfgjson.SupportForumId &&
                ctx.Channel.Parent.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Parent.Id != Program.cfgjson.SupportForumId &&
                ctx.Channel.Id != Program.cfgjson.BotCommandsChannel)
            {
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} This command can only be used in <#{Program.cfgjson.TechSupportChannel}>, <#{Program.cfgjson.SupportForumId}>, and threads in those channels!"));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command can only be used in <#{Program.cfgjson.TechSupportChannel}>, <#{Program.cfgjson.SupportForumId}>, their threads, and <#{Program.cfgjson.BotCommandsChannel}>!");
                return;
            }
            
            // Get muted roles
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
                // couldn't fetch member, fail
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be in the server!");
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be in the server!");
                return;
            }

            if (await Program.redis.HashExistsAsync("mutes", targetUser.Id) && targetMember is not null && targetMember.Roles.Contains(tqsMutedRole))
            {
                // If the member has a regular mute, leave the TQS mute alone (it's only a role now & it has no effect if they also have Muted); it will be removed when they are unmuted
                if (targetMember.Roles.Contains(mutedRole))
                {
                    if (ctx is SlashCommandContext)
                        await ctx.EditResponseAsync($"{Program.cfgjson.Emoji.Error} {targetUser.Mention} has been muted by a Moderator! Their TQS Mute will be removed when the Moderator-issued mute expires.");
                    else
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {targetUser.Mention} has been muted by a Moderator! Their TQS Mute will be removed when the Moderator-issued mute expires.");
                    return;
                }
                
                // user is TQS-muted; unmute
                await MuteHelpers.UnmuteUserAsync(targetUser, reason, true, ctx.User, true);
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync($"{Program.cfgjson.Emoji.Success} Successfully unmuted {targetUser.Mention}!");
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully unmuted {targetUser.Mention}!");
            }
            else
            {
                // member is not TQS-muted, fail
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be TQS-muted!");
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be TQS-muted!");
            }
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

        [Command("unmute")]
        [TextAlias("umute")]
        [Description("Unmutes a previously muted user, typically ahead of the standard expiration time. See also: mute")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task UnmuteCmd(CommandContext ctx, [Parameter("user"), Description("The user you're trying to unmute.")] DiscordUser targetUser, [Parameter("reason"), Description("The reason for the unmute."), RemainingText] string reason = "No reason specified.")
        {
            if (ctx is SlashCommandContext)
                await ctx.DeferResponseAsync();
            
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

            if ((await Program.redis.HashExistsAsync("mutes", targetUser.Id)) || (member != default && (member.Roles.Contains(mutedRole) || member.Roles.Contains(tqsMutedRole))))
            {
                await MuteHelpers.UnmuteUserAsync(targetUser, reason, true, ctx.User);
                var unmuteMsg = $"{Program.cfgjson.Emoji.Information} Successfully unmuted **{DiscordHelpers.UniqueUsername(targetUser)}**";
                
                if (reason != "No reason specified.")
                    unmuteMsg += $": **{reason}**";

                await ctx.RespondAsync(unmuteMsg);
            }
            else
                try
                {
                    await MuteHelpers.UnmuteUserAsync(targetUser, reason, true, ctx.User);
                    var errorMsg = $"{Program.cfgjson.Emoji.Warning} According to Discord that user is not muted, but I tried to unmute them anyway. Hope it works.";

                    if (reason != "No reason specified.")
                        errorMsg += $"\nReason: **{reason}**";

                    await ctx.RespondAsync(errorMsg);
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
        
        [Command("editmutetextcmd")]
        [TextAlias("editmute")]
        [Description("Edit the details of a mute. Updates the DM to the user, among other things.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        public async Task EditMuteCmd(TextCommandContext ctx,
            [Description("The user you wish to edit the mute of. Accepts many formats")] DiscordUser targetUser,
            [RemainingText, Description("Combined argument for the time and reason for the mute. For example '1h rule 7' or 'rule 10'")] string timeAndReason = "No reason specified."
        )
        {
            if (!await Program.redis.HashExistsAsync("mutes", targetUser.Id))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} There's no record of a mute for that user! Please make sure they're muted or you got the right user.");
                return;
            }
            
            (TimeSpan muteDuration, string reason, bool _) = PunishmentHelpers.UnpackTimeAndReason(timeAndReason, ctx.Message.Timestamp.DateTime);

            var mute = JsonConvert.DeserializeObject<MemberPunishment>(await Program.redis.HashGetAsync("mutes", targetUser.Id));
            
            mute.ModId = ctx.User.Id;
            if (muteDuration == default)
                mute.ExpireTime = null;
            else
                mute.ExpireTime = mute.ActionTime + muteDuration;
            mute.Reason = reason;
            
            var guild = await Program.discord.GetGuildAsync(mute.ServerId);
            
            var contextMessage = await DiscordHelpers.GetMessageFromReferenceAsync(mute.ContextMessageReference);
            var dmMessage = await DiscordHelpers.GetMessageFromReferenceAsync(mute.DmMessageReference);
            
            reason = reason.Replace("`", "\\`").Replace("*", "\\*");
            
            if (contextMessage is not null)
            {
                if (muteDuration == default)
                    await contextMessage.ModifyAsync($"{Program.cfgjson.Emoji.Muted} {targetUser.Mention} has been muted: **{reason}**");
                else
                {
                    await contextMessage.ModifyAsync($"{Program.cfgjson.Emoji.Muted} {targetUser.Mention} has been muted for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}**: **{reason}**");
                }
            }
            
            if (dmMessage is not null)
            {
                string dmContent = "";

                if (mute.ExpireTime == null)
                {
                    dmContent = $"{Program.cfgjson.Emoji.Muted} You have been muted in **{guild.Name}**!\nReason: **{reason}**";
                }
                else
                {
                    dmContent = $"{Program.cfgjson.Emoji.Muted} You have been muted in **{guild.Name}** for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}**!" +
                                $"\nReason: **{reason}**" +
                                $"\nMute expires: <t:{TimeHelpers.ToUnixTimestamp(mute.ExpireTime)}:R>";
                }

                if (reason.ToLower().Contains("modmail"))
                {
                    dmContent += $"\n{Program.cfgjson.Emoji.Information} When contacting <@{Program.cfgjson.ModmailUserId}>, make sure to **enable DMs** from the server to allow your message to go through.";
                }
                await dmMessage.ModifyAsync(dmContent);
            }
            
            try
            {
                var targetMember = await guild.GetMemberAsync(targetUser.Id);
                await targetMember.TimeoutAsync(mute.ExpireTime + TimeSpan.FromSeconds(10), mute.Reason);
            }
            catch (Exception e)
            {
                Program.discord.Logger.LogError(e, "Failed to issue timeout to {user}", targetUser.Id);
            }
            
            await Program.redis.HashSetAsync("mutes", targetUser.Id.ToString(), JsonConvert.SerializeObject(mute));
            
            // Construct log message
            string logOut = $"{Program.cfgjson.Emoji.MessageEdit} The mute for {targetUser.Mention} was edited by {ctx.User.Mention}!\nReason: **{reason}**";
            
            if (mute.ExpireTime == null)
            {
                logOut += "\nMute expires: **Never**";
            }
            else
            {
                logOut += $"\nMute expires: <t:{TimeHelpers.ToUnixTimestamp(mute.ExpireTime)}:R>";
            }
            
            // Log to mod log
            await LogChannelHelper.LogMessageAsync("mod", logOut);
            
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully edited the mute for {targetUser.Mention}!");
        }
    }
}