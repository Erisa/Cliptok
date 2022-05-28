namespace Cliptok.Commands
{
    internal class TechSupport : BaseCommandModule
    {
        [Command("ask")]
        [Description("Outputs information on how and where to ask tech support questions. Replying to a message while triggering the command will mirror the reply in the response.")]
        [HomeServer]
        public async Task AskCmd(CommandContext ctx, [Description("Optional, a user to ping with the information")] DiscordUser user = default)
        {
            await ctx.Message.DeleteAsync();
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithTitle("**__Need Help Or Have a Problem?__**")
                .WithDescription(
                $"You're probably looking for <#{Program.cfgjson.TechSupportChannel}>.\n" +
                $"{Program.cfgjson.Emoji.Windows11} Need help with **Windows 11**? Go to <#894699119195619379>.\n\n" +
                $"Once there, please be sure to provide **plenty of details**, ping the <@&{Program.cfgjson.CommunityTechSupportRoleID}> role, and *be patient!*\n\n" +
                $"Look under the `🔧 Support` category for the appropriate channel for your issue. See <#413274922413195275> for more info."
                )
                .WithColor(13920845);

            if (user != default)
            {
                await ctx.Channel.SendMessageAsync(user.Mention, embed);
            }
            else if (ctx.Message.ReferencedMessage != null)
            {
                var messageBuild = new DiscordMessageBuilder()
                    .WithEmbed(embed)
                    .WithReply(ctx.Message.ReferencedMessage.Id, mention: true);

                await ctx.Channel.SendMessageAsync(messageBuild);
            }
            else
            {
                await ctx.Channel.SendMessageAsync(embed);
            }
        }

    }
}
