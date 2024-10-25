﻿namespace Cliptok.Commands
{
    internal class Announcements
    {

        [Command("editannounce")]
        [Description("Edit an announcement, preserving the ping highlight.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task EditAnnounce(
            TextCommandContext ctx,
            [Description("The ID of the message to edit.")] ulong messageId,
            [Description("The short name for the role to ping.")] string roleName,
            [RemainingText, Description("The new message content, excluding the ping.")] string content
        )
        {
            DiscordRole discordRole;

            if (Program.cfgjson.AnnouncementRoles.ContainsKey(roleName))
            {
                discordRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles[roleName]);
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
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That role name isn't recognised!");
                return;
            }
        }

        [Command("announce")]
        [Description("Announces something in the current channel, pinging an Insider role in the process.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task AnnounceCmd(TextCommandContext ctx, [Description("'canary', 'dev', 'beta', 'beta10', 'rp', 'rp10', 'patch', 'rpbeta', 'rpbeta10', 'betadev', 'candev'")] string roleName, [RemainingText, Description("The announcement message to send.")] string announcementMessage)
        {
            DiscordRole discordRole;

            if (Program.cfgjson.AnnouncementRoles.ContainsKey(roleName))
            {
                discordRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles[roleName]);
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
                var rpRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["rp"]);
                var betaRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["beta"]);

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
            else if (roleName == "rpbeta10")
            {
                var rpRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["rp10"]);
                var betaRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["beta10"]);

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
            else if (roleName == "betadev")
            {
                var betaRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["beta"]);
                var devRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["dev"]);

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
            else if (roleName == "candev")
            {
                var canaryRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["canary"]);
                var devRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["dev"]);

                await canaryRole.ModifyAsync(mentionable: true);
                await devRole.ModifyAsync(mentionable: true);

                try
                {
                    await ctx.Message.DeleteAsync();
                    await ctx.Channel.SendMessageAsync($"{canaryRole.Mention} {devRole.Mention}\n{announcementMessage}");
                }
                catch
                {
                    // We still need to remember to make it unmentionable even if the msg fails.
                }

                await canaryRole.ModifyAsync(mentionable: false);
                await devRole.ModifyAsync(mentionable: false);
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That role name isn't recognised!");
                return;
            }

        }
    }
}
