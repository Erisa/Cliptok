namespace Cliptok.Commands
{
    public class RaidmodeCmds
    {
        [Command("raidmode")]
        [TextAlias("clipraidmode")]
        [Description("Commands relating to Raidmode")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [RequirePermissions(DiscordPermission.ModerateMembers)]
        public class RaidmodeSlashCommands
        {
            [DefaultGroupCommand]
            [Command("status")]
			[Description("Check the current state of raidmode.")]
            public async Task RaidmodeStatus(CommandContext ctx)
            {
                // Avoid conflicts with Modmail's !raidmode
                if (ctx is TextCommandContext
                    && !ctx.As<TextCommandContext>().Message.Content.Contains("clipraidmode")
                    && !ctx.As<TextCommandContext>().Message.Content.Contains("c!raidmode"))
                    return;

                if (Program.db.HashExists("raidmode", ctx.Guild.Id))
                {
                    string output = $"Raidmode is currently **enabled**.";

                    ulong expirationTimeUnix = (ulong)Program.db.HashGet("raidmode", ctx.Guild.Id);
                    output += $"\nRaidmode ends <t:{expirationTimeUnix}>";

                    var newAccountAgeKey = Program.db.StringGet("raidmode-accountage");
                    if (newAccountAgeKey.HasValue)
                        output += $"\nAccounts created before <t:{newAccountAgeKey}> are still allowed to join.";

                    await ctx.RespondAsync(output, ephemeral: true);
                }
                else
                {
                    await ctx.RespondAsync($" Raidmode is currently **disabled**.", ephemeral: true);
                }

            }

            [Command("on")]
			[Description("Enable raidmode. Defaults to 3 hour length if not specified.")]
            public async Task RaidmodeOnSlash(CommandContext ctx,
                [Parameter("duration"), Description("How long to keep raidmode enabled for.")] string duration = default,
                [Parameter("allowed_account_age"), Description("How old an account can be to be allowed to bypass raidmode. Relative to right now.")] string allowedAccountAge = ""
            )
            {
                // Avoid conflicts with Modmail's !raidmode
                if (ctx is TextCommandContext
                    && !ctx.As<TextCommandContext>().Message.Content.Contains("clipraidmode")
                    && !ctx.As<TextCommandContext>().Message.Content.Contains("c!raidmode"))
                    return;

                if (Program.db.HashExists("raidmode", ctx.Guild.Id))
                {
                    string output = $"Raidmode is already **enabled**.";

                    ulong expirationTimeUnix = (ulong)Program.db.HashGet("raidmode", ctx.Guild.Id);
                    output += $"\nRaidmode ends <t:{expirationTimeUnix}>";

                    var newAccountAgeKey = Program.db.StringGet("raidmode-accountage");
                    if (newAccountAgeKey.HasValue)
                        output += $"\nAccounts created before <t:{newAccountAgeKey}> are still allowed to join.";

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

                    DateTime allowedAgeTime;

                    if (allowedAccountAge == "")
                        Program.db.KeyDelete("raidmode-accountage");
                    else
                    {
                        DateTime anchorTime = DateTime.Now;
                        DateTime parseResult = HumanDateParser.HumanDateParser.Parse(allowedAccountAge, anchorTime);
                        TimeSpan timeSpan = parseResult - anchorTime;
                        allowedAgeTime = anchorTime - timeSpan;
                        Program.db.StringSet("raidmode-accountage", TimeHelpers.ToUnixTimestamp(allowedAgeTime));
                    }

                    string userContent = $"Raidmode is now **enabled** and will end <t:{unixExpiration}:R>.";
                    string logContent = $"{Program.cfgjson.Emoji.On} Raidmode was **enabled** by {ctx.User.Mention} and ends <t:{unixExpiration}:R>.";

                    // i dont know why im fetching it back but honestly i get so many weird conditions i just want to be informed on the current state
                    var newAccountAgeKey = Program.db.StringGet("raidmode-accountage");

                    if (newAccountAgeKey.HasValue)
                    {
                        var stringAdd = $"\nAccounts created before <t:{newAccountAgeKey}> will still be allowed to join.";
                        userContent += stringAdd;
                        logContent += stringAdd;
                    }

                    await ctx.RespondAsync(userContent);

                    DiscordMessageBuilder response = new DiscordMessageBuilder()
                        .WithContent(logContent)
                        .WithAllowedMentions(Mentions.None);
                    await LogChannelHelper.LogMessageAsync("mod", response);
                }
            }

            [Command("off")]
			[Description("Disable raidmode immediately.")]
            public async Task RaidmodeOffSlash(CommandContext ctx)
            {
                // Avoid conflicts with Modmail's !raidmode
                if (ctx is TextCommandContext
                    && !ctx.As<TextCommandContext>().Message.Content.Contains("clipraidmode")
                    && !ctx.As<TextCommandContext>().Message.Content.Contains("c!raidmode"))
                    return;

                if (Program.db.HashExists("raidmode", ctx.Guild.Id))
                {
                    long expirationTimeUnix = (long)Program.db.HashGet("raidmode", ctx.Guild.Id);
                    Program.db.HashDelete("raidmode", ctx.Guild.Id);
                    Program.db.KeyDelete("raidmode-accountage");

                    string resp = $"Raidmode is now **disabled**.\nIt was supposed to end <t:{expirationTimeUnix}:R>.";

                    var newAccountAgeKey = Program.db.StringGet("raidmode-accountage");
                    if (newAccountAgeKey.HasValue)
                        resp += $"\nAccounts created before <t:{newAccountAgeKey}> were still allowed to join.";

                    await ctx.RespondAsync(resp);
                    DiscordMessageBuilder response = new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Off} Raidmode was **disabled** by {ctx.User.Mention}.\nIt was supposed to end <t:{expirationTimeUnix}:R>.")
                        .WithAllowedMentions(Mentions.None);
                    await LogChannelHelper.LogMessageAsync("mod", response);
                }
                else
                {
                    await ctx.RespondAsync($"Raidmode is already **disabled**.");
                }
            }
        }
    }
}