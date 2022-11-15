using System.Linq;

namespace Cliptok.Commands.InteractionCommands
{
    public class RoleInteractions : ApplicationCommandModule
    {
        [SlashCommand("grant", "Grant a user Tier 1, bypassing any verification requirements.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task SlashGrant(InteractionContext ctx, [Option("user", "The user to grant Tier 1 to.")] DiscordUser user)
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

        [HomeServer]
        [SlashCommandGroup("roles", "Opt in/out of roles.")]
        public class RoleSlashCommands
        {
            [SlashCommand("grant", "Opt into a role.")]
            public async Task GrantRole(
                InteractionContext ctx,
                [Choice("Windows 11 Insiders (Dev)", "dev")]
                [Choice("Windows 11 Insiders (Beta)", "beta")]
                [Choice("Windows 11 Insiders (Release Preview)", "rp")]
                [Choice("Windows 10 Insiders (Release Preview)", "rp10")]
                [Choice("Patch Tuesday", "patch")]
                [Option("role", "The role to opt into.")] string role)
            {
                DiscordMember member = ctx.Member;

                var roleData = Program.cfgjson.GrantableRoles.FirstOrDefault(pair => pair.Key == role);

                await member.GrantRoleAsync(ctx.Guild.GetRole(roleData.Value), $"/roles grant used by {ctx.User.Username}#{ctx.User.Discriminator}");
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} The role <@&{roleData.Value}> has been successfully granted!", ephemeral: true, mentions: false);
            }

            [SlashCommand("remove", "Opt out of a role.")]
            public async Task RemoveRole(
                InteractionContext ctx,
                [Choice("Windows 11 Insiders (Dev)", "dev")]
                [Choice("Windows 11 Insiders (Beta)", "beta")]
                [Choice("Windows 11 Insiders (Release Preview)", "rp")]
                [Choice("Windows 10 Insiders (Release Preview)", "rp10")]
                [Choice("Patch Tuesday", "patch")]
                [Option("role", "The role to opt out of.")] string role)
            {
                DiscordMember member = ctx.Member;

                var roleData = Program.cfgjson.GrantableRoles.FirstOrDefault(pair => pair.Key == role);

                await member.RevokeRoleAsync(ctx.Guild.GetRole(roleData.Value), $"/roles remove used by {ctx.User.Username}#{ctx.User.Discriminator}");
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} The role <@&{roleData.Value}> has been successfully removed!", ephemeral: true, mentions: false);
            }
        }
    }
}
