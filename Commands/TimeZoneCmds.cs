namespace Cliptok.Commands;

[Command("timezone")]
[TextAlias("tz")]
[Description("Check or set your time zone.")]
public static class TimeZoneCmds
{
    [Command("set")]
    [Description("Set your time zone.")]
    public static async Task TimeZoneSetCommand(CommandContext ctx,
        [Parameter("timezone"), Description("The time zone to set, in tz database format.")] string timezone = default)
    {
        await ctx.DeferResponseAsync();

        if (timezone == default)
        {
            await ctx.RespondAsync("Please provide a time zone from the `TZ identifier` column of the following page:  <https://en.wikipedia.org/wiki/List_of_tz_database_time_zones>\nExample: `Asia/Tokyo`");
            return;
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out var tzInfo))
        {
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Invalid time zone! Please provide a time zone from the `TZ identifier` column of the following page: <https://en.wikipedia.org/wiki/List_of_tz_database_time_zones>\nExample: `Asia/Tokyo`");
            return;
        }

        await Program.redis.HashSetAsync("userTimeZones", ctx.User.Id, tzInfo.Id);
        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Your time zone has been set to `{tzInfo.Id}`.");
    }

    // this behaves like gg's `pls tz` without any arguments, or with a user
    [DefaultGroupCommand]
    [Command("get")]
    [Description("Get the time zone for yourself or someone else.")]
    public static async Task TimeZoneGetCommand(CommandContext ctx,
        [Parameter("user"), Description("The user to get the time zone for.")] DiscordUser user = default)
    {
        await ctx.DeferResponseAsync();

        var userIsSelf = false;
        if (user == default)
            user = ctx.User;
        if (user == ctx.User)
            userIsSelf = true;

        var timezone = await Program.redis.HashGetAsync("userTimeZones", user.Id);

        if (!timezone.HasValue)
        {
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {(userIsSelf ? "You do" : $"{user.Username} does")} not have a time zone set.\n\n{(userIsSelf ? "You" : "They")} can set one with the `set` argument.");
            return;
        }
        
        await ctx.RespondAsync($"{Program.cfgjson.Emoji.ClockTime} {(userIsSelf ? "Your" : $"{user.Username}'s")} timezone is `{timezone}`.\n\n{(userIsSelf ? "You" : "They")} can change it with the `set` argument or {(userIsSelf ? "check the time" : "you can check their time")} with the `timefor` command.");
    }

    [Command("reset")]
    [Description("Reset your time zone.")]
    public static async Task TimeZoneResetCommand(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        await Program.redis.HashDeleteAsync("userTimeZones", ctx.User.Id);

        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Your time zone has been reset.");
    }
}