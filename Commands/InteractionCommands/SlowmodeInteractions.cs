namespace Cliptok.Commands.InteractionCommands
{
    internal class SlowmodeInteractions : ApplicationCommandModule
    {
        [SlashCommand("slowmode", "Slow down the channel...", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [SlashCommandPermissions(DiscordPermissions.ModerateMembers)]
        public async Task SlowmodeSlashCommand(
            InteractionContext ctx,
            [Option("slow_time", "Allowed time between each users messages. 0 for off. A number of seconds or a parseable time.")] string timeToParse,
            [Option("channel", "The channel to slow down, if not the current one.")] DiscordChannel channel = default
        )
        {
            if (channel == default)
                channel = ctx.Channel;

            TimeSpan slowmodeTime;

            if (int.TryParse(timeToParse, out int seconds))
            {
                await channel.ModifyAsync(ch => ch.PerUserRateLimit = seconds);
                if (seconds > 0)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.ClockTime} Slowmode has been set in {channel.Mention}!"
                        + $"\nUsers will only be send messages once every **{TimeHelpers.TimeToPrettyFormat(TimeSpan.FromSeconds(seconds), false)}** until the setting is disabled or changed.");
                }
                else if (seconds == 0)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.ClockTime} Slowmode has been disabled in {channel.Mention}!");
                }
                else
                {
                    await ctx.RespondAsync("I didn't understand your input...", ephemeral: true);
                }
            }
            else
            {
                try
                {
                    DateTime anchorTime = DateTime.Now;
                    slowmodeTime = HumanDateParser.HumanDateParser.Parse(timeToParse, anchorTime).Subtract(anchorTime);

                    seconds = (int)slowmodeTime.TotalSeconds;

                    if (seconds > 0 && seconds <= 21600)
                    {
                        await channel.ModifyAsync(ch => ch.PerUserRateLimit = seconds);
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.ClockTime} Slowmode has been set in {channel.Mention}!"
                            + $"\nUsers will only be send messages once every **{TimeHelpers.TimeToPrettyFormat(TimeSpan.FromSeconds(seconds), false)}** until the setting is disabled or changed.");
                    }
                    else if (seconds > 21600)
                    {
                        await ctx.RespondAsync("Time cannot be longer than 6 hours.", ephemeral: true);
                    }
                }
                catch (Exception ex)
                {
                    var embed = new DiscordEmbedBuilder
                    {
                        Color = new DiscordColor("#FF0000"),
                        Title = "An exception occurred when executing a command",
                        Description = $"`{ex.GetType()}` occurred when executing.",
                        Timestamp = DateTime.UtcNow
                    };
                    embed.WithFooter(Program.discord.CurrentUser.Username, Program.discord.CurrentUser.AvatarUrl)
                        .AddField("Message", ex.Message);
                    if (ex is ArgumentException)
                        embed.AddField("Note", "This usually means that you used the command incorrectly.\n" +
                            "Please double-check how to use this command.");
                    await ctx.RespondAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
                }
            }
        }

    }
}
