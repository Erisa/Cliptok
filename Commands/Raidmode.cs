namespace Cliptok.Commands
{
    internal class Raidmode : BaseCommandModule
    {
        [Group("clipraidmode")]
        [Description("Manage the server's raidmode, preventing joins while on.")]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        class RaidmodeCommands : BaseCommandModule
        {
            [GroupCommand]
            [Description("Check whether raidmode is enabled or not, and when it ends.")]
            [Aliases("status")]
            public async Task RaidmodeStatus(CommandContext ctx)
            {
                if (Program.db.HashExists("raidmode", ctx.Guild.Id))
                {
                    string output = $"{Program.cfgjson.Emoji.On} Raidmode is currently **enabled**.";
                    ulong expirationTimeUnix = (ulong)Program.db.HashGet("raidmode", ctx.Guild.Id);
                    output += $"\nRaidmode ends <t:{expirationTimeUnix}>";
                    await ctx.RespondAsync(output);
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Banned} Raidmode is currently **disabled**.");
                }
            }

            [Command("on")]
            [Description("Enable raidmode.")]
            public async Task RaidmodeOn(CommandContext ctx, [Description("The amount of time to keep raidmode enabled for. Default is 3 hours.")] string duration = default)
            {
                if (Program.db.HashExists("raidmode", ctx.Guild.Id))
                {
                    string output = $"{Program.cfgjson.Emoji.On} Raidmode is already **enabled**.";

                    ulong expirationTimeUnix = (ulong)Program.db.HashGet("raidmode", ctx.Guild.Id);
                    output += $"\nRaidmode ends <t:{expirationTimeUnix}>";
                    await ctx.RespondAsync(output);
                }
                else
                {
                    DateTime parsedExpiration;

                    if (duration == default)
                        parsedExpiration = DateTime.Now.AddHours(3);
                    else
                        parsedExpiration = HumanDateParser.HumanDateParser.Parse(duration);

                    long unixExpiration = TimeHelpers.ToUnixTimestamp(parsedExpiration);
                    Program.db.HashSet("raidmode", ctx.Guild.Id, unixExpiration);

                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.On} Raidmode is now **enabled** and will end <t:{unixExpiration}:R>.");
                    await Program.logChannel.SendMessageAsync(
                        new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.On} Raidmode was **enabled** by {ctx.User.Mention} and ends <t:{unixExpiration}:R>.")
                            .WithAllowedMentions(Mentions.None)
                   );
                }
            }

            [Command("off")]
            [Description("Disable raidmode.")]
            public async Task RaidmdodeOff(CommandContext ctx)
            {
                if (Program.db.HashExists("raidmode", ctx.Guild.Id))
                {
                    long expirationTimeUnix = (long)Program.db.HashGet("raidmode", ctx.Guild.Id);
                    Program.db.HashDelete("raidmode", ctx.Guild.Id);
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Off} Raidmode is now **disabled**.\nIt was supposed to end <t:{expirationTimeUnix}:R>.");
                    await Program.logChannel.SendMessageAsync(
                        new DiscordMessageBuilder()
                           .WithContent($"{Program.cfgjson.Emoji.Off} Raidmode was **disabled** by {ctx.User.Mention}.\nIt was supposed to end <t:{expirationTimeUnix}:R>.")
                            .WithAllowedMentions(Mentions.None)
                    );
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Off} Raidmode is already **disabled**.");
                }
            }
        }
    }
}
