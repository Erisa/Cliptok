namespace Cliptok.Commands
{
    public class DehoistCmds
    {
        [Command("dehoist")]
        [Description("Dehoist a member, dropping them to the bottom of the list. Lasts until they change nickname.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task DehoistCmd(CommandContext ctx, [Parameter("member"), Description("The member to dehoist.")] DiscordUser user)
        {
            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(user.Id);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to find {user.Mention} as a member! Are they in the server?", ephemeral: true);
                return;
            }

            if (member.DisplayName[0] == DehoistHelpers.dehoistCharacter)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {member.Mention} is already dehoisted!", ephemeral: true);
                return;
            }

            if (member.MemberFlags.Value.HasFlag(DiscordMemberFlags.AutomodQuarantinedUsername) || member.MemberFlags.Value.HasFlag(DiscordMemberFlags.AutomodQuarantinedGuildTag))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {member.Mention} is quarantined because their profile is in violation of AutoMod rules! Discord will not let me dehoist them. Please change their nickname manually.", ephemeral: true);
                return;
            }

            try
            {
                await member.ModifyAsync(a =>
                {
                    a.Nickname = DehoistHelpers.DehoistName(member.DisplayName);
                    a.AuditLogReason = $"[Dehoist by {DiscordHelpers.UniqueUsername(ctx.User)}]";
                });
                await Program.redis.SetAddAsync("manualDehoists", user.Id);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to dehoist {member.Mention}! Do I have permission?", ephemeral: true);
                return;
            }
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfuly dehoisted {member.Mention}!", mentions: false);
        }
    }
}