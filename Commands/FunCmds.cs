using Cliptok.Constants;

namespace Cliptok.Commands
{
    public class FunCmds
    {
        [Command("Hug")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task Hug(UserCommandContext ctx, DiscordUser targetUser)
        {
            var user = targetUser;

            if (user is not null)
            {
                switch (new Random().Next(4))
                {
                    case 0:
                        await ctx.RespondAsync($"*{ctx.User.Mention} snuggles {user.Mention}*");
                        break;

                    case 1:
                        await ctx.RespondAsync($"*{ctx.User.Mention} huggles {user.Mention}*");
                        break;

                    case 2:
                        await ctx.RespondAsync($"*{ctx.User.Mention} cuddles {user.Mention}*");
                        break;

                    case 3:
                        await ctx.RespondAsync($"*{ctx.User.Mention} hugs {user.Mention}*");
                        break;
                }
            }
        }

        [Command("notextcmd")]
        [TextAlias("no", "yes")]
        [Description("Makes Cliptok choose something for you. Outputs either Yes or No.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Tier5)]
        public async Task No(TextCommandContext ctx)
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