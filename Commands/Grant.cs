namespace Cliptok.Commands
{
    internal class Grant : BaseCommandModule
    {
        [Command("grant")]
        [Description("Grant a user access to the server, by giving them the Tier 1 role.")]
        [Aliases("clipgrant", "verify")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task GrantCommand(CommandContext ctx, [Description("The member to grant Tier 1 role to.")] DiscordUser _)
        {
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command is deprecated and no longer works. Please right click (or tap and hold on mobile) the user and click \"Verify Member\" if available.");
        }
    }
}
