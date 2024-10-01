namespace Cliptok.Commands.InteractionCommands
{
    internal class RoleInteractions
    {
        [Command("grant")]
        [Description("Grant a user Tier 1, bypassing any verification requirements.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermissions.ModerateMembers)]
        public async Task SlashGrant(SlashCommandContext ctx, [Parameter("user"), Description("The user to grant Tier 1 to.")] DiscordUser _)
        {
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command is deprecated and no longer works. Please right click (or tap and hold on mobile) the user and click \"Verify Member\" if available.");
        }

        [HomeServer]
        [Command("roles")]
        [Description("Opt in/out of roles.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        internal class RoleSlashCommands
        {
            [Command("grant")]
			[Description("Opt into a role.")]
            public async Task GrantRole(
                SlashCommandContext ctx,
                [SlashChoiceProvider(typeof(RoleCommandChoiceProvider))]
                [Parameter("role"), Description("The role to opt into.")] string role) // TODO(#202): test choices!!!
            {
                DiscordMember member = ctx.Member;

                var roleId = role switch
                {
                    "insiderCanary" => Program.cfgjson.UserRoles.InsiderCanary,
                    "insiderDev" => Program.cfgjson.UserRoles.InsiderDev,
                    "insiderBeta" => Program.cfgjson.UserRoles.InsiderBeta,
                    "insiderRP" => Program.cfgjson.UserRoles.InsiderRP,
                    "insider10RP" => Program.cfgjson.UserRoles.Insider10RP,
                    "insider10Beta" => Program.cfgjson.UserRoles.Insider10Beta,
                    "patchTuesday" => Program.cfgjson.UserRoles.PatchTuesday,
                    "giveaways" => Program.cfgjson.UserRoles.Giveaways,
                    _ => throw new NotSupportedException()
                };

                var roleData = await ctx.Guild.GetRoleAsync(roleId);

                await member.GrantRoleAsync(roleData, $"/roles grant used by {DiscordHelpers.UniqueUsername(ctx.User)}");
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} The role {roleData.Mention} has been successfully granted!", ephemeral: true, mentions: false);
            }

            [Command("remove")]
			[Description("Opt out of a role.")]
            public async Task RemoveRole(
                SlashCommandContext ctx,
                [SlashChoiceProvider(typeof(RoleCommandChoiceProvider))]
                [Parameter("role"), Description("The role to opt out of.")] string role) // TODO(#202): test choices!!!
            {
                DiscordMember member = ctx.Member;

                var roleId = role switch
                {
                    "insiderCanary" => Program.cfgjson.UserRoles.InsiderCanary,
                    "insiderDev" => Program.cfgjson.UserRoles.InsiderDev,
                    "insiderBeta" => Program.cfgjson.UserRoles.InsiderBeta,
                    "insiderRP" => Program.cfgjson.UserRoles.InsiderRP,
                    "insider10RP" => Program.cfgjson.UserRoles.Insider10RP,
                    "insider10Beta" => Program.cfgjson.UserRoles.Insider10Beta,
                    "patchTuesday" => Program.cfgjson.UserRoles.PatchTuesday,
                    "giveaways" => Program.cfgjson.UserRoles.Giveaways,
                    _ => throw new NotSupportedException()
                };

                var roleData = await ctx.Guild.GetRoleAsync(roleId);

                await member.RevokeRoleAsync(roleData, $"/roles remove used by {DiscordHelpers.UniqueUsername(ctx.User)}");
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} The role {roleData.Mention} has been successfully removed!", ephemeral: true, mentions: false);
            }
        }
        
        internal class RoleCommandChoiceProvider : IChoiceProvider
        {
            public async ValueTask<IReadOnlyDictionary<string, object>> ProvideAsync(CommandParameter _)
            {
                return new Dictionary<string, object>
                {
                    { "Windows 11 Insiders (Canary)", "insiderCanary" },
                    { "Windows 11 Insiders (Dev)", "insiderDev" },
                    { "Windows 11 Insiders (Beta)", "insiderBeta" },
                    { "Windows 11 Insiders (Release Preview)", "insiderRP" },
                    { "Windows 10 Insiders (Release Preview)", "insider10RP" },
                    { "Windows 10 Insiders (Beta)", "insider10Beta" },
                    { "Patch Tuesday", "patchTuesday" },
                    { "Giveaways", "giveaways" }  
                };
            }
        }
    }
}
