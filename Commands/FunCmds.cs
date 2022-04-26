namespace Cliptok.Commands
{
    internal class FunCmds : BaseCommandModule
    {
        [Command("tellraw")]
        [Description("Nothing of interest.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task TellRaw(CommandContext ctx, [Description("???")] DiscordChannel discordChannel, [RemainingText, Description("???")] string output)
        {
            try
            {
                await discordChannel.SendMessageAsync(output);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Your dumb message didn't want to send. Congrats, I'm proud of you.");
                return;
            }
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} I sent your stupid message to {discordChannel.Mention}.");

        }

        [Command("no")]
        [Description("Makes Cliptok choose something for you. Outputs either Yes or No.")]
        [Aliases("yes")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Tier5)]
        public async Task No(CommandContext ctx)
        {
            List<string> noResponses = new()
            {
                "Processing...",
                "Considering it...",
                "Hmmm...",
                "Give me a moment...",
                "Calculating...",
                "Generating response...",
                "Asking the Oracle...",
                "Loading...",
                "Please wait..."
            };

            await ctx.Message.DeleteAsync();
            var msg = await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Loading} Thinking about it...");
            await Task.Delay(2000);

            for (int thinkCount = 1; thinkCount <= 3; thinkCount++)
            {
                int r = Program.rand.Next(noResponses.Count);
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Loading} {noResponses[r]}");
                await Task.Delay(2000);
            }

            if (Program.rand.Next(10) == 3)
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Yes.");
            }
            else
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} No.");
            }
        }

    }
}
