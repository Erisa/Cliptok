namespace Cliptok.Commands
{
    internal class Grant : BaseCommandModule
    {
        [Command("grant")]
        [Description("Grant a user access to the server, by giving them the Tier 1 role.")]
        [Aliases("clipgrant", "verify")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task GrantCommand(CommandContext ctx, [Description("The member to grant Tier 1 role to.")] DiscordUser user)
        {
            DiscordMember member = default;
            try
            {
                member = await ctx.Guild.GetMemberAsync(user.Id);
            }
            catch (Exception)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user does not appear to be in the server!");
                return;
            }

            var tierOne = ctx.Guild.GetRole(Program.cfgjson.TierRoles[0]);
            await member.GrantRoleAsync(tierOne, $"!grant used by {ctx.User.Username}#{ctx.User.Discriminator}");
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} {member.Mention} can now access the server(?)\n**WARNING**: This probably did not work due to recent Discord changes. Please use a desktop or web Discord client, right click the user and click \"Verify Member\" if available.");
        }
    }
}
