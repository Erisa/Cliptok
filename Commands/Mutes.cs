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
            reason = $"[Manual unmute by {ctx.User.Username}#{ctx.User.Discriminator}]: {reason}";

            // todo: store per-guild
            DiscordRole mutedRole = ctx.Guild.GetRole(Program.cfgjson.MutedRole);

            DiscordMember member = default;
            try
            {
                member = await ctx.Guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException ex)
            {
                Program.discord.Logger.LogWarning(eventId: Program.CliptokEventID, exception: ex, message: "Failed to unmute {user} in {server} because they weren't in the server.", $"{targetUser.Username}#{targetUser.Discriminator}", ctx.Guild.Name);
            }

            if ((await Program.db.HashExistsAsync("mutes", targetUser.Id)) || (member != default && member.Roles.Contains(mutedRole)))
            {
                await MuteHelpers.UnmuteUserAsync(targetUser, reason);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Information} Successfully unmuted **{targetUser.Username}#{targetUser.Discriminator}**.");
            }
            else
                try
                {
                    await MuteHelpers.UnmuteUserAsync(targetUser, reason);
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
    }
}
