using static Cliptok.Helpers.BanHelpers;

namespace Cliptok.Commands.InteractionCommands
{
    internal class BanInteractions : ApplicationCommandModule
    {
        [SlashCommand("ban", "Bans a user from the server, either permanently or temporarily.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.Moderator), SlashCommandPermissions(Permissions.BanMembers)]
        public async Task BanSlashCommand(InteractionContext ctx,
            [Option("user", "The user to ban")] DiscordUser user,
            [Option("reason", "The reason the user is being banned")] string reason,
            [Option("keep_messages", "Whether to keep the users messages when banning")] bool keepMessages = false,
            [Option("time", "The length of time the user is banned for")] string time = null,
            [Option("appeal_link", "Whether to show the user an appeal URL in the DM")] bool appealable = false
        )
        {
            // Initial response to avoid the 3 second timeout, will edit later.
            var eout = new DiscordInteractionResponseBuilder().AsEphemeral(true);
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource, eout);

            // Edits need a webhook rather than interaction..?
            DiscordWebhookBuilder webhookOut = new();
            int messageDeleteDays = 7;
            if (keepMessages)
                messageDeleteDays = 0;

            if (user.IsBot)
            {
                webhookOut.Content = $"{Program.cfgjson.Emoji.Error} To prevent accidents, I won't ban bots. If you really need to do this, do it manually in Discord.";
                await ctx.EditResponseAsync(webhookOut);
                return;
            }

            DiscordMember targetMember;

            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(user.Id);
                if (GetPermLevel(ctx.Member) == ServerPermLevel.TrialModerator && (GetPermLevel(targetMember) >= ServerPermLevel.TrialModerator))
                {
                    webhookOut.Content = $"{Program.cfgjson.Emoji.Error} As a Trial Moderator you cannot perform moderation actions on other staff members.";
                    await ctx.EditResponseAsync(webhookOut);
                    return;
                }
            }
            catch
            {
                // do nothing :/
            }

            TimeSpan banDuration;
            if (time is null)
                banDuration = default;
            else
            {
                try
                {
                    banDuration = HumanDateParser.HumanDateParser.Parse(time).Subtract(ctx.Interaction.CreationTimestamp.DateTime);
                }
                catch
                {
                    webhookOut.Content = $"{Program.cfgjson.Emoji.Error} There was an error parsing your supplied ban length!";
                    await ctx.EditResponseAsync(webhookOut);
                    return;
                }

            }

            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(user.Id);
            }
            catch
            {
                member = null;
            }

            if (member is null)
            {
                await BanHelpers.BanFromServerAsync(user.Id, reason, ctx.User.Id, ctx.Guild, messageDeleteDays, ctx.Channel, banDuration, appealable);
            }
            else
            {
                if (DiscordHelpers.AllowedToMod(ctx.Member, member))
                {
                    if (DiscordHelpers.AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
                    {
                        await BanHelpers.BanFromServerAsync(user.Id, reason, ctx.User.Id, ctx.Guild, messageDeleteDays, ctx.Channel, banDuration, appealable);
                    }
                    else
                    {
                        webhookOut.Content = $"{Program.cfgjson.Emoji.Error} I don't have permission to ban **{DiscordHelpers.UniqueUsername(user)}**!";
                        await ctx.EditResponseAsync(webhookOut);
                        return;
                    }
                }
                else
                {
                    webhookOut.Content = $"{Program.cfgjson.Emoji.Error} You don't have permission to ban **{DiscordHelpers.UniqueUsername(user)}**!";
                    await ctx.EditResponseAsync(webhookOut);
                    return;
                }
            }
            reason = reason.Replace("`", "\\`").Replace("*", "\\*");
            if (banDuration == default)
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {user.Mention} has been banned: **{reason}**");
            else
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {user.Mention} has been banned for **{TimeHelpers.TimeToPrettyFormat(banDuration, false)}**: **{reason}**");

            webhookOut.Content = $"{Program.cfgjson.Emoji.Success} User was successfully bonked.";
            await ctx.EditResponseAsync(webhookOut);
        }

        [SlashCommand("unban", "Unbans a user who has been previously banned.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.Moderator), SlashCommandPermissions(Permissions.BanMembers)]
        public async Task SlashUnbanCommand(InteractionContext ctx, [Option("user", "The ID or mention of the user to unban. Ignore the suggestions, IDs work.")] SnowflakeObject userId, [Option("reason", "Used in audit log only currently")] string reason = "No reason specified.")
        {
            DiscordUser targetUser = default;
            try
            {
                targetUser = await ctx.Client.GetUserAsync(userId.Id);
            }
            catch (Exception ex)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Exception of type `{ex.GetType()}` thrown fetching user:\n```\n{ex.Message}\n{ex.StackTrace}```", ephemeral: true);
                return;
            }
            if ((await Program.db.HashExistsAsync("bans", targetUser.Id)))
            {
                await UnbanUserAsync(ctx.Guild, targetUser, $"[Unban by {DiscordHelpers.UniqueUsername(ctx.User)}]: {reason}");
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Unbanned} Successfully unbanned **{DiscordHelpers.UniqueUsername(targetUser)}**.");
            }
            else
            {
                bool banSuccess = await UnbanUserAsync(ctx.Guild, targetUser);
                if (banSuccess)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Unbanned} Successfully unbanned **{DiscordHelpers.UniqueUsername(targetUser)}**.");
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be banned, *and* an error occurred while attempting to unban them anyway.\nPlease contact the bot owner if this wasn't expected, the error has been logged.");
                }
            }
        }

        [SlashCommand("kick", "Kicks a user, removing them from the server until they rejoin.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.Moderator), SlashCommandPermissions(Permissions.KickMembers)]
        public async Task KickCmd(InteractionContext ctx, [Option("user", "The user you want to kick from the server.")] DiscordUser target, [Option("reason", "The reason for kicking this user.")] string reason = "No reason specified.")
        {
            if (target.IsBot)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} To prevent accidents, I won't kick bots. If you really need to do this, do it manually in Discord.");
                return;
            }

            reason = reason.Replace("`", "\\`").Replace("*", "\\*");

            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(target.Id);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be in the server!");
                return;
            }

            if (DiscordHelpers.AllowedToMod(ctx.Member, member))
            {
                if (DiscordHelpers.AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
                {
                    await Kick.KickAndLogAsync(member, reason, ctx.Member);
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Ejected} {target.Mention} has been kicked: **{reason}**");
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Done!", ephemeral: true);
                    return;
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to kick **{DiscordHelpers.UniqueUsername(target)}**!", ephemeral: true);
                    return;
                }
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You aren't allowed to kick **{DiscordHelpers.UniqueUsername(target)}**!", ephemeral: true);
                return;
            }
        }

    }
}
