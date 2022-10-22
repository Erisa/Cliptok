namespace Cliptok.Commands
{
    internal class Utility : BaseCommandModule
    {
        [Command("ping")]
        [Description("Pong? This command lets you know whether I'm working well.")]
        public async Task Ping(CommandContext ctx)
        {
            DiscordMessage return_message = await ctx.Message.RespondAsync("Pinging...");
            ulong ping = (return_message.Id - ctx.Message.Id) >> 22;
            char[] choices = new char[] { 'a', 'e', 'o', 'u', 'i', 'y' };
            char letter = choices[Program.rand.Next(0, choices.Length)];
            await return_message.ModifyAsync($"P{letter}ng! 🏓\n" +
                $"• It took me `{ping}ms` to reply to your message!\n" +
                $"• Last Websocket Heartbeat took `{ctx.Client.Ping}ms`!");
        }

        [Command("edit")]
        [Description("Edit a message.")]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task Edit(
            CommandContext ctx,
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

        [Command("editappend")]
        [Description("Append content to an existing bot message with a newline.")]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task EditAppend(
            CommandContext ctx,
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

        [Command("userinfo")]
        [Description("Show info about a user.")]
        [Aliases("user-info", "whois")]
        public async Task UserInfoCommand(
            CommandContext ctx,
            DiscordUser user = null)
        {
            if (user is null)
                user = ctx.User;

            await ctx.RespondAsync(embed: await DiscordHelpers.GenerateUserEmbed(user, ctx.Guild));
        }
    }
}
