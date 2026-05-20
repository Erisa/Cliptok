namespace Cliptok.Commands
{
    public class UserInfoCmds
    {
        [Command("userinfo")]
        [TextAlias("user-info", "whois")]
        [Description("Show info about a user.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        public async Task UserInfoSlashCommand(CommandContext ctx, [Parameter("user"), Description("The user to retrieve information about.")] DiscordUser user = null, [Parameter("public"), Description("Whether to show the output publicly.")] bool publicMessage = false)
        {
            if (user is null)
                user = ctx.User;

            await ctx.RespondAsync(embed: await DiscordHelpers.GenerateUserEmbed(user, ctx.Guild), ephemeral: !publicMessage);
        }
    }
}
