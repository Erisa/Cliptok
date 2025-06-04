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
                await ctx.RespondAsync($"Your dumb message didn't want to send. Congrats, I'm proud of you.", ephemeral: true);
                ctx.Client.Logger.LogError(e, "An error ocurred trying to send a tellraw message.");
                return;
            }
            await ctx.RespondAsync($"I sent your stupid message to {channelObj.Mention}.", ephemeral: true);
            await LogChannelHelper.LogMessageAsync("secret",
                new DiscordMessageBuilder()
                .WithContent($"{ctx.User.Mention} used tellraw in {channelObj.Mention}:")
                .WithAllowedMentions(Mentions.None)
                .AddEmbed(new DiscordEmbedBuilder().WithDescription(input))
            );
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