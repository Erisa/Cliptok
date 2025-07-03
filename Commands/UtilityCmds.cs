using Cliptok.Constants;

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

        [Command("tellraw")]
        [Description("You know what you're here for.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task TellRaw(CommandContext ctx, [Parameter("channel"), Description("Either mention or ID. Not a name.")] string discordChannel, [Parameter("input"), Description("???")] string input, [Parameter("reply_msg_id"), Description("ID of message to use in a reply context.")] string replyID = "0", [Parameter("pingreply"), Description("Ping pong.")] bool pingreply = true)
        {
            if (ctx is SlashCommandContext)
                await ctx.As<SlashCommandContext>().DeferResponseAsync(ephemeral: true);

            DiscordChannel channelObj = default;
            ulong channelId;
            if (!ulong.TryParse(discordChannel, out channelId))
            {
                var captures = RegexConstants.channel_rx.Match(discordChannel).Groups[1].Captures;
                if (captures.Count > 0)
                    channelId = Convert.ToUInt64(captures[0].Value);
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} The channel you gave can't be parsed. Please give either an ID or a mention of a channel.", ephemeral: true);
                    return;
                }
            }
            try
            {
                channelObj = await ctx.Client.GetChannelAsync(channelId);
            }
            catch
            {
                // caught immediately after
            }
            if (channelObj == default)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I can't find a channel with the provided ID!", ephemeral: true);
                return;
            }

            try
            {
                if (replyID == "0")
                    await channelObj.SendMessageAsync(new DiscordMessageBuilder().WithContent(input));
                else
                    await channelObj.SendMessageAsync(new DiscordMessageBuilder().WithContent(input).WithReply(Convert.ToUInt64(replyID), pingreply, false));
            }
            catch (Exception e)
            {
                await ctx.RespondAsync($"Your message didn't want to send. Congrats, I'm proud of you.", ephemeral: true);
                ctx.Client.Logger.LogError(e, "An error ocurred trying to send a tellraw message.");
                return;
            }
            await ctx.RespondAsync($"I sent your message to {channelObj.Mention}.", ephemeral: true);
            await LogChannelHelper.LogMessageAsync("secret",
                new DiscordMessageBuilder()
                .WithContent($"{ctx.User.Mention} used tellraw in {channelObj.Mention}:")
                .WithAllowedMentions(Mentions.None)
                .AddEmbed(new DiscordEmbedBuilder().WithDescription(input))
            );
        }
    }
}