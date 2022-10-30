namespace Cliptok.Commands.InteractionCommands
{
    internal class DehoistInteractions : ApplicationCommandModule
    {
        [SlashCommandGroup("permadehoist", "Permanently/persistently dehoist members.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), SlashCommandPermissions(Permissions.ManageNicknames)]
        public class PermadehoistSlashCommands
        {
            [SlashCommand("enable", "Permanently dehoist a member. They will be automatically dehoisted until disabled.")]
            public async Task PermadehoistEnableSlashCmd(InteractionContext ctx, [Option("member", "The member to permadehoist.")] DiscordUser discordUser)
            {
                var (success, isPermissionError) = await DehoistHelpers.PermadehoistMember(discordUser, ctx.User, ctx.Guild);

                if (success)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.On} Successfully permadehoisted {discordUser.Mention}!", mentions: false);

                if (!success & !isPermissionError)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {discordUser.Mention} is already permadehoisted!", mentions: false);

                if (!success && isPermissionError)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to permadehoist {discordUser.Mention}!", mentions: false);
            }

            [SlashCommand("disable", "Disable permadehoist for a member.")]
            public async Task PermadehoistDisableSlashCmd(InteractionContext ctx, [Option("member", "The member to remove the permadehoist for.")] DiscordUser discordUser)
            {
                var (success, isPermissionError) = await DehoistHelpers.UnpermadehoistMember(discordUser, ctx.User, ctx.Guild);

                if (success)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.On} Successfully removed the permadehoist for {discordUser.Mention}!", mentions: false);

                if (!success & !isPermissionError)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {discordUser.Mention} isn't permadehoisted!", mentions: false);

                if (!success && isPermissionError)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to remove the permadehoist for {discordUser.Mention}!", mentions: false);
            }

            [SlashCommand("status", "Check the status of permadehoist for a member.")]
            public async Task PermadehoistStatusSlashCmd(InteractionContext ctx, [Option("member", "The member whose permadehoist status to check.")] DiscordUser discordUser)
            {
                if (await Program.db.SetContainsAsync("permadehoists", discordUser.Id))
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.On} {discordUser.Mention} is permadehoisted.", mentions: false);
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Off} {discordUser.Mention} is not permadehoisted.", mentions: false);
            }
        }
    }
}
