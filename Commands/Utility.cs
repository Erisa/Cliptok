namespace Cliptok.Commands
{
    internal class Utility
    {
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

        [Command("userinfotextcmd")]
        [TextAlias("userinfo", "userinfo", "user-info", "whois")]
        [Description("Show info about a user.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        public async Task UserInfoCommand(
            TextCommandContext ctx,
            DiscordUser user = null)
        {
            if (user is null)
                user = ctx.User;

            await ctx.RespondAsync(embed: await DiscordHelpers.GenerateUserEmbed(user, ctx.Guild));
        }
    }
}
