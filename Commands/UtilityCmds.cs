namespace Cliptok.Commands
{
    public class UtilityCmds
    {
        [Command("Dump message data")]
        [SlashCommandTypes(DiscordApplicationCommandType.MessageContextMenu)]
        [AllowedProcessors(typeof(MessageCommandProcessor))]
        public async Task DumpMessage(MessageCommandContext ctx, DiscordMessage targetMessage)
        {
            var rawMsgData = JsonConvert.SerializeObject(targetMessage, Formatting.Indented);
            await ctx.RespondAsync(await StringHelpers.CodeOrHasteBinAsync(rawMsgData, "json"), ephemeral: true);
        }

        [Command("Show Avatar")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task ContextAvatar(UserCommandContext ctx, DiscordUser targetUser)
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
        public async Task ContextUserInformation(UserCommandContext ctx, DiscordUser targetUser)
        {
            await ctx.RespondAsync(embed: await DiscordHelpers.GenerateUserEmbed(targetUser, ctx.Guild), ephemeral: true);
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