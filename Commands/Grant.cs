namespace Cliptok.Commands
{
    internal class Grant : BaseCommandModule
    {
        [Command("grant")]
        [Description("Grant a user access to the server, by giving them the Tier 1 role.")]
        [Aliases("clipgrant", "verify")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task GrantCommand(CommandContext ctx, [Description("The member to grant Tier 1 role to.")] DiscordMember member)
        {
            var tierOne = ctx.Guild.GetRole(Program.cfgjson.TierRoles[0]);
            await member.GrantRoleAsync(tierOne, $"!grant used by {ctx.User.Username}#{ctx.User.Discriminator}");
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} {member.Mention} can now access the server!");
        }
    }
}
