namespace Cliptok.Commands
{
    [HomeServer]
    [Command("roles")]
    [Description("Opt in/out of roles.")]
    [AllowedProcessors(typeof(SlashCommandProcessor))]
    internal class RoleCmds
    {
        [Command("grant")]
        [Description("Opt into a role.")]
        public async Task GrantRole(
            SlashCommandContext ctx,
            [SlashAutoCompleteProvider(typeof(Providers.RolesAutocompleteProvider))]
            [Parameter("role"), Description("The role to opt into.")] string role)
        {
            await ctx.DeferResponseAsync(ephemeral: true);

            DiscordMember member = ctx.Member;

            ulong roleId;
            try
            {
                roleId = Convert.ToUInt64(role);
            }
            catch (FormatException)
            {
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Invalid role! Please choose from the list.", ephemeral: true);
                return;
            }

            if ((Program.cfgjson.InsiderRoles is null || !Program.cfgjson.InsiderRoles.Contains(roleId)) &&
                roleId != Program.cfgjson.CommunityTechSupportRoleID &&
                roleId != Program.cfgjson.GiveawaysRole)
            {
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Invalid role! Please choose from the list.", ephemeral: true);
                return;
            }

            if (roleId == Program.cfgjson.CommunityTechSupportRoleID && await GetPermLevelAsync(ctx.Member) < ServerPermLevel.TechnicalQueriesSlayer)
            {
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.NoPermissions} You must be a TQS member to get the CTS role!", ephemeral: true);
                return;
            }

            var roleData = await ctx.Guild.GetRoleAsync(roleId);

            await member.GrantRoleAsync(roleData, $"/roles grant used by {DiscordHelpers.UniqueUsername(ctx.User)}");
            await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Success} The role {roleData.Mention} has been successfully granted!", ephemeral: true);
        }

        [Command("remove")]
        [Description("Opt out of a role.")]
        public async Task RemoveRole(
            SlashCommandContext ctx,
            [SlashAutoCompleteProvider(typeof(Providers.RolesAutocompleteProvider))]
            [Parameter("role"), Description("The role to opt out of.")] string role)
        {
            await ctx.DeferResponseAsync(ephemeral: true);

            DiscordMember member = ctx.Member;

            ulong roleId;
            try
            {
                roleId = Convert.ToUInt64(role);
            }
            catch (FormatException)
            {
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Invalid role! Please choose from the list.", ephemeral: true);
                return;
            }

            if ((Program.cfgjson.InsiderRoles is null || !Program.cfgjson.InsiderRoles.Contains(roleId)) &&
                roleId != Program.cfgjson.CommunityTechSupportRoleID &&
                roleId != Program.cfgjson.GiveawaysRole)
            {
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Invalid role! Please choose from the list.", ephemeral: true);
                return;
            }

            var roleData = await ctx.Guild.GetRoleAsync(roleId);

            await member.RevokeRoleAsync(roleData, $"/roles remove used by {DiscordHelpers.UniqueUsername(ctx.User)}");
            await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Success} The role {roleData.Mention} has been successfully removed!", ephemeral: true);
        }
    }
}