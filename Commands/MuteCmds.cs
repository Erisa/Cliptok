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
                    muteDuration = HumanDateParser.HumanDateParser.Parse(time).ToUniversalTime().Subtract(ctx.Interaction.CreationTimestamp.LocalDateTime);
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
                muteDuration = HumanDateParser.HumanDateParser.Parse(possibleTime).ToUniversalTime().Subtract(ctx.Message.Timestamp.DateTime);
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

                if (reason.ToLower().Contains("modmail") && Program.cfgjson.ModmailUserId != 0)
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