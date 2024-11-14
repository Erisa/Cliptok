namespace Cliptok.Commands
{
    internal class TechSupportCommands : BaseCommandModule
    {
        [Command("on-call")]
        [Description("Give yourself the CTS role.")]
        [RequireHomeserverPerm(ServerPermLevel.TechnicalQueriesSlayer)]
        public async Task OnCallCommand(CommandContext ctx)
        {
            var ctsRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.CommunityTechSupportRoleID);
            await ctx.Member.GrantRoleAsync(ctsRole, "Used !on-call");
            await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"{Program.cfgjson.Emoji.On} Received Community Tech Support Role")
                .WithDescription($"{ctx.User.Mention} is available to help out in **#tech-support**.\n(Use `!off-call` when you're no longer available)")
                .WithColor(DiscordColor.Green)
            ));
        }

        [Command("off-call")]
        [Description("Remove the CTS role.")]
        [RequireHomeserverPerm(ServerPermLevel.TechnicalQueriesSlayer)]
        public async Task OffCallCommand(CommandContext ctx)
        {
            var ctsRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.CommunityTechSupportRoleID);
            await ctx.Member.RevokeRoleAsync(ctsRole, "Used !off-call");
            await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"{Program.cfgjson.Emoji.Off} Removed Community Tech Support Role")
                .WithDescription($"{ctx.User.Mention} is no longer available to help out in **#tech-support**.\n(Use `!on-call` again when you're available)")
                .WithColor(DiscordColor.Red)
            ));
        }
    }
}
