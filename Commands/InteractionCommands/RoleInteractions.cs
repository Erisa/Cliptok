namespace Cliptok.Commands.InteractionCommands
{
    internal class RoleInteractions : ApplicationCommandModule
    {
        [SlashCommand("grant", "Grant a user Tier 1, bypassing any verification requirements.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task SlashGrant(InteractionContext ctx, [Option("user", "THe user to grant Tier 1 to.")] DiscordUser user)
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

            await member.GrantRoleAsync(tierOne, $"/grant used by {ctx.User.Username}#{ctx.User.Discriminator}");
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} {member.Mention} can now access the server!");
        }

    }
}
