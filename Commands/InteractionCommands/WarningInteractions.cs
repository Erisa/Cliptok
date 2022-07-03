namespace Cliptok.Commands.InteractionCommands
{
    internal class WarningInteractions : ApplicationCommandModule
    {
        [SlashCommand("warn", "Formally warn a user, usually for breaking the server rules.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task WarnSlashCommand(InteractionContext ctx,
         [Option("user", "The user to warn.")] DiscordUser user,
         [Option("reason", "The reason they're being warned.")] string reason,
         [Option("reply_msg_id", "The ID of a message to reply to, must be in the same channel.")] string replyMsgId = "0",
         [Option("channel", "The channel to warn the user in, implied if not supplied.")] DiscordChannel channel = null
        )
        {
            // Initial response to avoid the 3 second timeout, will edit later.
            var eout = new DiscordInteractionResponseBuilder().AsEphemeral(true);
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource, eout);

            // Edits need a webhook rather than interaction..?
            DiscordWebhookBuilder webhookOut;

            DiscordMember targetMember;

            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(user.Id);
                if (GetPermLevel(ctx.Member) == ServerPermLevel.TrialModerator && (GetPermLevel(targetMember) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
                {
                    webhookOut = new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} As a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
                    await ctx.EditResponseAsync(webhookOut);
                    return;
                }
            }
            catch
            {
                // do nothing :/
            }

            if (channel == null)
                channel = ctx.Channel;

            if (channel == null)
                channel = await ctx.Client.GetChannelAsync(ctx.Interaction.ChannelId);

            var messageBuild = new DiscordMessageBuilder()
                .WithContent($"{Program.cfgjson.Emoji.Warning} {user.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");

            if (replyMsgId != "0")
                messageBuild.WithReply(Convert.ToUInt64(replyMsgId), true, false);

            var msg = await channel.SendMessageAsync(messageBuild);

            _ = await WarningHelpers.GiveWarningAsync(user, ctx.User, reason, msg, channel);
            webhookOut = new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Success} User was warned successfully in {channel.Mention}\n[Jump to warning]({DiscordHelpers.MessageLink(msg)})");
            await ctx.EditResponseAsync(webhookOut);
        }

        [SlashCommand("warnings", "Fetch the warnings for a user.")]
        public async Task WarningsSlashCommand(InteractionContext ctx,
            [Option("user", "The user to find the warnings for.")] DiscordUser user,
            [Option("public", "Whether to show the warnings in public chat. Do not disrupt chat with this.")] bool publicWarnings = false
        )
        {
            var eout = new DiscordInteractionResponseBuilder().AddEmbed(await WarningHelpers.GenerateWarningsEmbedAsync(user));
            if (!publicWarnings)
                eout.AsEphemeral(true);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, eout);
        }

        [SlashCommand("transfer_warnings", "Transfer warnings from one user to another.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.Moderator)]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task TransferWarningsSlashCommand(InteractionContext ctx,
            [Option("source_user", "The user currently holding the warnings.")] DiscordUser sourceUser,
            [Option("target_user", "The user receiving the warnings.")] DiscordUser targetUser,
            [Option("merge", "Whether to merge the source user's warnings and the target user's warnings.")] bool merge = false,
            [Option("force_override", "DESTRUCTIVE OPERATION: Whether to OVERRIDE and DELETE the target users existing warnings.")] bool forceOverride = false
        )
        {
            var sourceWarnings = await Program.db.HashGetAllAsync(sourceUser.Id.ToString());
            var targetWarnings = await Program.db.HashGetAllAsync(targetUser.Id.ToString());

            if (sourceWarnings.Length == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} The source user has no warnings to transfer.", await WarningHelpers.GenerateWarningsEmbedAsync(sourceUser));
                return;
            }
            else if (merge)
            {
                foreach (var warning in sourceWarnings)
                {
                    await Program.db.HashSetAsync(targetUser.Id.ToString(), warning.Name, warning.Value);
                }
                await Program.db.KeyDeleteAsync(sourceUser.Id.ToString());
            }
            else if (targetWarnings.Length > 0 && !forceOverride)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} **CAUTION**: The target user has warnings.\n\n" +
                    $"If you are sure you want to **OVERRIDE** and **DELETE** these warnings, please consider the consequences before adding `force_override: True` to the command.\nIf you wish to **NOT** override the target's warnings, please use `merge: True` instead.",
                    await WarningHelpers.GenerateWarningsEmbedAsync(targetUser));
                return;
            }
            else if (targetWarnings.Length > 0 && forceOverride)
            {
                await Program.db.KeyDeleteAsync(targetUser.Id.ToString());
                await Program.db.KeyRenameAsync(sourceUser.Id.ToString(), targetUser.Id.ToString());
            }
            else
            {
                await Program.db.KeyRenameAsync(sourceUser.Id.ToString(), targetUser.Id.ToString());
            }

            string operationText = "";
            if (merge)
                operationText = "merge ";
            else if (forceOverride)
                operationText = "force ";
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully {operationText}transferred warnings from {sourceUser.Mention} to {targetUser.Mention}!");
            await LogChannelHelper.LogMessageAsync("mod",
                new DiscordMessageBuilder()
                    .WithContent($"{Program.cfgjson.Emoji.Information} Warnings from {sourceUser.Mention} were {operationText}transferred to {targetUser.Mention} by `{ctx.User.Username}#{ctx.User.Discriminator}`")
                    .WithEmbed(await WarningHelpers.GenerateWarningsEmbedAsync(targetUser))
           );
        }


    }
}
