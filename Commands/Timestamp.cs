namespace Cliptok.Commands
{
    internal class Timestamp : BaseCommandModule
    {
        [Group("timestamp")]
        [Aliases("ts", "time")]
        [Description("Returns various timestamps for a given Discord ID/snowflake")]
        [HomeServer]
        class TimestampCmds : BaseCommandModule
        {
            [GroupCommand]
            [Aliases("u", "unix", "epoch")]
            [Description("Returns the Unix timestamp of a given Discord ID/snowflake")]
            public async Task TimestampUnixCmd(CommandContext ctx, [Description("The ID/snowflake to fetch the Unix timestamp for")] ulong snowflake)
            {
                var msSinceEpoch = snowflake >> 22;
                var msUnix = msSinceEpoch + 1420070400000;
                await ctx.RespondAsync($"{msUnix / 1000}");
            }

            [Command("relative")]
            [Aliases("r")]
            [Description("Returns the amount of time between now and a given Discord ID/snowflake")]
            public async Task TimestampRelativeCmd(CommandContext ctx, [Description("The ID/snowflake to fetch the relative timestamp for")] ulong snowflake)
            {
                var msSinceEpoch = snowflake >> 22;
                var msUnix = msSinceEpoch + 1420070400000;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.ClockTime} <t:{msUnix / 1000}:R>");
            }

            [Command("fulldate")]
            [Aliases("f", "datetime")]
            [Description("Returns the fully-formatted date and time of a given Discord ID/snowflake")]
            public async Task TimestampFullCmd(CommandContext ctx, [Description("The ID/snowflake to fetch the full timestamp for")] ulong snowflake)
            {
                var msSinceEpoch = snowflake >> 22;
                var msUnix = msSinceEpoch + 1420070400000;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.ClockTime} <t:{msUnix / 1000}:F>");
            }

        }

    }
}
