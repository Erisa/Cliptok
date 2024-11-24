namespace Cliptok.Commands
{
    public class UtilityCmds
    {
        [Command("Show Avatar")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task ContextAvatar(SlashCommandContext ctx, DiscordUser targetUser)
        {
            string avatarUrl = await LykosAvatarMethods.UserOrMemberAvatarURL(targetUser, ctx.Guild);

            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithColor(new DiscordColor(0xC63B68))
                .WithTimestamp(DateTime.UtcNow)
                .WithImageUrl(avatarUrl)
                .WithAuthor(
                    $"Avatar for {targetUser.Username} (Click to open in browser)",
                    avatarUrl
                );

            await ctx.RespondAsync(null, embed, ephemeral: true);
        }

        [Command("User Information")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task ContextUserInformation(SlashCommandContext ctx, DiscordUser targetUser)
        {
            await ctx.RespondAsync(embed: await DiscordHelpers.GenerateUserEmbed(targetUser, ctx.Guild), ephemeral: true);
        }

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

        [Command("pingtextcmd")]
        [TextAlias("ping")]
        [Description("Pong? This command lets you know whether I'm working well.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        public async Task Ping(TextCommandContext ctx)
        {
            ctx.Client.Logger.LogDebug(ctx.Client.GetConnectionLatency(Program.cfgjson.ServerID).ToString());
            DiscordMessage return_message = await ctx.Message.RespondAsync("Pinging...");
            ulong ping = (return_message.Id - ctx.Message.Id) >> 22;
            char[] choices = new char[] { 'a', 'e', 'o', 'u', 'i', 'y' };
            char letter = choices[Program.rand.Next(0, choices.Length)];
            await return_message.ModifyAsync($"P{letter}ng! 🏓\n" +
                $"• It took me `{ping}ms` to reply to your message!\n" +
                $"• Last Websocket Heartbeat took `{Math.Round(ctx.Client.GetConnectionLatency(0).TotalMilliseconds, 0)}ms`!");
        }

        [Command("edittextcmd")]
        [TextAlias("edit")]
        [Description("Edit a message.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task Edit(
            TextCommandContext ctx,
            [Description("The ID of the message to edit.")] ulong messageId,
            [RemainingText, Description("New message content.")] string content
        )
        {
            var msg = await ctx.Channel.GetMessageAsync(messageId);

            if (msg is null || msg.Author.Id != ctx.Client.CurrentUser.Id)
                return;

            await ctx.Message.DeleteAsync();

            await msg.ModifyAsync(content);
        }

        [Command("editappendtextcmd")]
        [TextAlias("editappend")]
        [Description("Append content to an existing bot message with a newline.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task EditAppend(
            TextCommandContext ctx,
            [Description("The ID of the message to edit")] ulong messageId,
            [RemainingText, Description("Content to append on the end of the message.")] string content
        )
        {
            var msg = await ctx.Channel.GetMessageAsync(messageId);

            if (msg is null || msg.Author.Id != ctx.Client.CurrentUser.Id)
                return;

            var newContent = msg.Content + "\n" + content;
            if (newContent.Length > 2000)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} New content exceeded 2000 characters.");
            }
            else
            {
                await ctx.Message.DeleteAsync();
                await msg.ModifyAsync(newContent);
            }
        }

        [Command("timestamptextcmd")]
        [TextAlias("timestamp", "ts", "time")]
        [Description("Returns various timestamps for a given Discord ID/snowflake")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer]
        class TimestampCmds
        {
            [DefaultGroupCommand]
            [Command("unix")]
            [TextAlias("u", "epoch")]
            [Description("Returns the Unix timestamp of a given Discord ID/snowflake")]
            public async Task TimestampUnixCmd(TextCommandContext ctx, [Description("The ID/snowflake to fetch the Unix timestamp for")] ulong snowflake)
            {
                var msSinceEpoch = snowflake >> 22;
                var msUnix = msSinceEpoch + 1420070400000;
                await ctx.RespondAsync($"{msUnix / 1000}");
            }

            [Command("relative")]
            [TextAlias("r")]
            [Description("Returns the amount of time between now and a given Discord ID/snowflake")]
            public async Task TimestampRelativeCmd(TextCommandContext ctx, [Description("The ID/snowflake to fetch the relative timestamp for")] ulong snowflake)
            {
                var msSinceEpoch = snowflake >> 22;
                var msUnix = msSinceEpoch + 1420070400000;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.ClockTime} <t:{msUnix / 1000}:R>");
            }

            [Command("fulldate")]
            [TextAlias("f", "datetime")]
            [Description("Returns the fully-formatted date and time of a given Discord ID/snowflake")]
            public async Task TimestampFullCmd(TextCommandContext ctx, [Description("The ID/snowflake to fetch the full timestamp for")] ulong snowflake)
            {
                var msSinceEpoch = snowflake >> 22;
                var msUnix = msSinceEpoch + 1420070400000;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.ClockTime} <t:{msUnix / 1000}:F>");
            }

        }
    }
}