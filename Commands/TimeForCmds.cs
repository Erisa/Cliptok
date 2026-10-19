namespace Cliptok.Commands
{
    public static class TimeForCmds
    {
        [Command("timefor")]
        [TextAlias("tf", "thefuck")]
        [Description("Get the time for yourself or someone else.")]
        public static async Task TimeForCommand(CommandContext ctx,
            [Parameter("user"), Description("The user to get the time for.")] DiscordUser user = default)
        {
            await ctx.DeferResponseAsync();

            var userIsSelf = false;
            if (user == default)
                user = ctx.User;
            if (user == ctx.User)
                userIsSelf = true;

            var userTz = await Program.redis.HashGetAsync("userTimeZones", user.Id);

            if (!userTz.HasValue)
            {
                await ctx.RespondAsync(new DiscordMessageBuilder()
                    .WithContent($"{Program.cfgjson.Emoji.Error} {(userIsSelf ? "You do" : $"{user.Mention} does")} not have a time zone set. {(userIsSelf ? "You": "They")} can set it with `timezone set`.")
                    .WithAllowedMentions(Mentions.None));
                return;
            }

            await ctx.RespondAsync(new DiscordMessageBuilder()
                .WithContent($"{Program.cfgjson.Emoji.ClockTime} "
                    + (userIsSelf ? "Your current time is " : $"The current time for {user.Mention} is ")
                    + $"`{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, userTz).ToString("h:mm tt, yyyy-MM-dd")}`")
                .WithAllowedMentions(Mentions.None));
        }
    }
}