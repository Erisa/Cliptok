namespace Cliptok.Commands
{
    internal class Mutes : BaseCommandModule
    {
        [Command("unmute")]
        [Aliases("umute")]
        [Description("Unmutes a previously muted user, typically ahead of the standard expiration time. See also: mute")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task UnmuteCmd(CommandContext ctx, [Description("The user you're trying to unmute.")] DiscordUser targetUser, string reason = "No reason provided.")
        {
            reason = $"[Manual unmute by {DiscordHelpers.UniqueUsername(ctx.User)}]: {reason}";

            // todo: store per-guild
            DiscordRole mutedRole = ctx.Guild.GetRole(Program.cfgjson.MutedRole);
            DiscordRole tqsMutedRole = ctx.Guild.GetRole(Program.cfgjson.TqsMutedRole);

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

        [Command("mute")]
        [Description("Mutes a user, preventing them from sending messages until they're unmuted. See also: unmute")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task MuteCmd(
            CommandContext ctx, [Description("The user you're trying to mute")] DiscordUser targetUser,
            [RemainingText, Description("Combined argument for the time and reason for the mute. For example '1h rule 7' or 'rule 10'")] string timeAndReason = "No reason specified."
        )
        {
            DiscordMember targetMember = default;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // is this worth logging?
            }

            if (targetMember != default && GetPermLevel(ctx.Member) == ServerPermLevel.TrialModerator && (GetPermLevel(targetMember) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
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

        [Command("tqsmute")]
        [Description(
            "Temporarily mutes a user, preventing them from sending messages in #tech-support and related channels until they're unmuted.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TechnicalQueriesSlayer)]
        public async Task TqsMuteCmd(
            CommandContext ctx, [Description("The user to mute")] DiscordUser targetUser,
            [RemainingText, Description("The reason for the mute")] string reason = "No reason specified.")
        {
            // Only allow usage in #tech-support, #tech-support-forum, and their threads
            if (ctx.Channel.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Id != Program.cfgjson.SupportForumId &&
                ctx.Channel.Parent.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Parent.Id != Program.cfgjson.SupportForumId)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command can only be used in <#{Program.cfgjson.TechSupportChannel}>, <#{Program.cfgjson.SupportForumId}>, and threads in those channels!");
                return;
            }

            // Check if the user is already muted; disallow TQS-mute if so

            DiscordRole mutedRole = ctx.Guild.GetRole(Program.cfgjson.MutedRole);
            DiscordRole tqsMutedRole = ctx.Guild.GetRole(Program.cfgjson.TqsMutedRole);

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
            
            if (await Program.db.HashExistsAsync("mutes", targetUser.Id) || (targetMember != default && (targetMember.Roles.Contains(mutedRole) || targetMember.Roles.Contains(tqsMutedRole))))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, that user is already muted.");
                return;
            }

            // Check if user to be muted is staff or TQS, and disallow if so
            if (targetMember != default && GetPermLevel(ctx.Member) == ServerPermLevel.TechnicalQueriesSlayer && (GetPermLevel(targetMember) >= ServerPermLevel.TechnicalQueriesSlayer || targetMember.IsBot))
            {
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, you cannot mute other TQS or staff members.");
                return;
            }

            await ctx.Message.DeleteAsync();

            // mute duration is static for TQS mutes
            TimeSpan muteDuration = TimeSpan.FromHours(Program.cfgjson.TqsMuteDurationHours);

            MuteHelpers.MuteUserAsync(targetUser, reason, ctx.User.Id, ctx.Guild, ctx.Channel, muteDuration, true, true);
        }
    }
}
