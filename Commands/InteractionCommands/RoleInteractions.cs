namespace Cliptok.Commands.InteractionCommands
{
    internal class RoleInteractions : ApplicationCommandModule
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

            await member.GrantRoleAsync(tierOne, $"/grant used by {DiscordHelpers.UniqueUsername(ctx.User)}");
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} {member.Mention} can now access the server(?)\n**WARNING**: This probably did not work due to recent Discord changes. Please use a desktop or web Discord client, right click the user and click \"Verify Member\" if available.");
        }

        [HomeServer]
        [SlashCommandGroup("roles", "Opt in/out of roles.")]
        internal class RoleSlashCommands
        {
            [SlashCommand("grant", "Opt into a role.")]
            public async Task GrantRole(
                InteractionContext ctx,
                [Choice("Windows 11 Insiders (Canary)", "insiderCanary")]
                [Choice("Windows 11 Insiders (Dev)", "insiderDev")]
                [Choice("Windows 11 Insiders (Beta)", "insiderBeta")]
                [Choice("Windows 11 Insiders (Release Preview)", "insiderRP")]
                [Choice("Windows 10 Insiders (Release Preview)", "insider10RP")]
                [Choice("Patch Tuesday", "patchTuesday")]
                [Option("role", "The role to opt into.")] string role)
            {
                DiscordMember member = ctx.Member;

                var roleId = role switch
                {
                    "insiderCanary" => Program.cfgjson.UserRoles.InsiderCanary,
                    "insiderDev" => Program.cfgjson.UserRoles.InsiderDev,
                    "insiderBeta" => Program.cfgjson.UserRoles.InsiderBeta,
                    "insiderRP" => Program.cfgjson.UserRoles.InsiderRP,
                    "insider10RP" => Program.cfgjson.UserRoles.Insider10RP,
                    "patchTuesday" => Program.cfgjson.UserRoles.PatchTuesday,
                    _ => throw new NotSupportedException()
                };

                var roleData = ctx.Guild.GetRole(roleId);

                await member.GrantRoleAsync(roleData, $"/roles grant used by {DiscordHelpers.UniqueUsername(ctx.User)}");
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} The role {roleData.Mention} has been successfully granted!", ephemeral: true, mentions: false);
            }

            [SlashCommand("remove", "Opt out of a role.")]
            public async Task RemoveRole(
                InteractionContext ctx,
                [Choice("Windows 11 Insiders (Canary)", "insiderCanary")]
                [Choice("Windows 11 Insiders (Dev)", "insiderDev")]
                [Choice("Windows 11 Insiders (Beta)", "insiderBeta")]
                [Choice("Windows 11 Insiders (Release Preview)", "insiderRP")]
                [Choice("Windows 10 Insiders (Release Preview)", "insider10RP")]
                [Choice("Patch Tuesday", "patchTuesday")]
                [Option("role", "The role to opt out of.")] string role)
            {
                DiscordMember member = ctx.Member;

                var roleId = role switch
                {
                    "insiderCanary" => Program.cfgjson.UserRoles.InsiderCanary,
                    "insiderDev" => Program.cfgjson.UserRoles.InsiderDev,
                    "insiderBeta" => Program.cfgjson.UserRoles.InsiderBeta,
                    "insiderRP" => Program.cfgjson.UserRoles.InsiderRP,
                    "insider10RP" => Program.cfgjson.UserRoles.Insider10RP,
                    "patchTuesday" => Program.cfgjson.UserRoles.PatchTuesday,
                    _ => throw new NotSupportedException()
                };

                var roleData = ctx.Guild.GetRole(roleId);

                await member.RevokeRoleAsync(roleData, $"/roles remove used by {DiscordHelpers.UniqueUsername(ctx.User)}");
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} The role {roleData.Mention} has been successfully removed!", ephemeral: true, mentions: false);
            }
        }
    }
}
