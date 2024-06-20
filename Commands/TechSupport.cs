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
                .WithColor(13920845);
            if (ctx.Channel.Id == Program.cfgjson.TechSupportChannel || ctx.Channel.ParentId == Program.cfgjson.SupportForumId)
            {
                embed.Title = "**__Need help?__**";
                embed.Description = $"You are in the right place! Please state your question with *plenty of detail* and mention the <@&{Program.cfgjson.CommunityTechSupportRoleID}> role and someone may be able to help you.\n\n" +
                                   $"Details includes error codes and other specific information.";
            }
            else
            {
                embed.Title = "**__Need Help Or Have a Problem?__**";
                embed.Description = $"You're probably looking for <#{Program.cfgjson.TechSupportChannel}> or <#{Program.cfgjson.SupportForumId}>!\n\n" +
                                   $"Once there, please be sure to provide **plenty of details**, ping the <@&{Program.cfgjson.CommunityTechSupportRoleID}> role, and *be patient!*\n\n" +
                                   $"Look under the `🔧 Support` category for the appropriate channel for your issue. See <#413274922413195275> for more info.";
            }

            if (user != default)
            {
                await ctx.Channel.SendMessageAsync(user.Mention, embed);
            }
            else if (ctx.Message.ReferencedMessage is not null)
            {
                var messageBuild = new DiscordMessageBuilder()
                    .AddEmbed(embed)
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
