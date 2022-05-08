namespace Cliptok.Commands
{
    internal class Announcements : BaseCommandModule
    {

        [Command("editannounce")]
        [Description("Edit an announcement, preserving the ping highlight.")]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task EditAnnounce(
            CommandContext ctx,
            [Description("The ID of the message to edit.")] ulong messageId,
            [Description("The short name for the role to ping.")] string roleName,
            [RemainingText, Description("The new message content, excluding the ping.")] string content
        )
        {
            DiscordRole discordRole;

            if (Program.cfgjson.AnnouncementRoles.ContainsKey(roleName))
            {
                discordRole = ctx.Guild.GetRole(Program.cfgjson.AnnouncementRoles[roleName]);
                await discordRole.ModifyAsync(mentionable: true);
                try
                {
                    await ctx.Message.DeleteAsync();
                    var msg = await ctx.Channel.GetMessageAsync(messageId);
                    await msg.ModifyAsync($"{discordRole.Mention} {content}");
                }
                catch
                {
                    // We still need to remember to make it unmentionable even if the msg fails.
                }
                await discordRole.ModifyAsync(mentionable: false);
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That role name isnt recognised!");
                return;
            }
        }

        [Command("announce")]
        [Description("Announces something in the current channel, pinging an Insider role in the process.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task AnnounceCmd(CommandContext ctx, [Description("'dev','beta','rp', 'rp10, 'patch', 'rpbeta', 'betadev'")] string roleName, [RemainingText, Description("The announcement message to send.")] string announcementMessage)
        {
            DiscordRole discordRole;

            if (Program.cfgjson.AnnouncementRoles.ContainsKey(roleName))
            {
                discordRole = ctx.Guild.GetRole(Program.cfgjson.AnnouncementRoles[roleName]);
                await discordRole.ModifyAsync(mentionable: true);
                try
                {
                    await ctx.Message.DeleteAsync();
                    await ctx.Channel.SendMessageAsync($"{discordRole.Mention} {announcementMessage}");
                }
                catch
                {
                    // We still need to remember to make it unmentionable even if the msg fails.
                }
                await discordRole.ModifyAsync(mentionable: false);
            }
            else if (roleName == "rpbeta")
            {
                var rpRole = ctx.Guild.GetRole(Program.cfgjson.AnnouncementRoles["rp"]);
                var betaRole = ctx.Guild.GetRole(Program.cfgjson.AnnouncementRoles["beta"]);

                await rpRole.ModifyAsync(mentionable: true);
                await betaRole.ModifyAsync(mentionable: true);

                try
                {
                    await ctx.Message.DeleteAsync();
                    await ctx.Channel.SendMessageAsync($"{rpRole.Mention} {betaRole.Mention}\n{announcementMessage}");
                }
                catch
                {
                    // We still need to remember to make it unmentionable even if the msg fails.
                }

                await rpRole.ModifyAsync(mentionable: false);
                await betaRole.ModifyAsync(mentionable: false);
            }
            // this is rushed pending an actual solution
            else if (roleName == "betadev")
            {
                var betaRole = ctx.Guild.GetRole(Program.cfgjson.AnnouncementRoles["beta"]);
                var devRole = ctx.Guild.GetRole(Program.cfgjson.AnnouncementRoles["dev"]);

                await betaRole.ModifyAsync(mentionable: true);
                await devRole.ModifyAsync(mentionable: true);

                try
                {
                    await ctx.Message.DeleteAsync();
                    await ctx.Channel.SendMessageAsync($"{betaRole.Mention} {devRole.Mention}\n{announcementMessage}");
                }
                catch
                {
                    // We still need to remember to make it unmentionable even if the msg fails.
                }

                await betaRole.ModifyAsync(mentionable: false);
                await devRole.ModifyAsync(mentionable: false);
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That role name isnt recognised!");
                return;
            }

        }
    }
}
