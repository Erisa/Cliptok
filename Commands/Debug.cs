namespace Cliptok.Commands
{
    internal class Debug : BaseCommandModule
    {
        [Group("debug")]
        [Aliases("troubleshoot", "unbug", "bugn't", "helpsomethinghasgoneverywrong")]
        [Description("Commands and things for fixing the bot in the unlikely event that it breaks a bit.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        class DebugCmds : BaseCommandModule
        {
            [Command("mutes")]
            [Aliases("mute")]
            [Description("Debug the list of mutes.")]
            public async Task MuteDebug(CommandContext ctx, DiscordUser targetUser = default)
            {

                await DiscordHelpers.SafeTyping(ctx.Channel);


                string strOut = "";
                if (targetUser == default)
                {
                    var muteList = Program.db.HashGetAll("mutes").ToDictionary();
                    if (muteList == null | muteList.Keys.Count == 0)
                    {
                        await ctx.RespondAsync("No mutes found in database!");
                        return;
                    }
                    else
                    {
                        foreach (var entry in muteList)
                        {
                            strOut += $"{entry.Value}\n";
                        }
                    }
                    if (strOut.Length > 1930)
                    {
                        HasteBinResult hasteResult = await Program.hasteUploader.Post(strOut);
                        if (hasteResult.IsSuccess)
                        {
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} Output exceeded character limit: {hasteResult.FullUrl}.json");
                        }
                        else
                        {
                            Console.WriteLine(strOut);
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Unknown error occurred during upload to Hastebin.\nPlease try again or contact the bot owner.");
                        }
                    }
                    else
                    {
                        await ctx.RespondAsync($"```json\n{strOut}\n```");
                    }
                }
                else // if (targetUser != default)
                {
                    var userMute = Program.db.HashGet("mutes", targetUser.Id);
                    if (userMute.IsNull)
                    {
                        await ctx.RespondAsync("That user has no mute registered in the database!");
                    }
                    else
                    {
                        await ctx.RespondAsync($"```json\n{userMute}\n```");
                    }
                }
            }

            [Command("bans")]
            [Aliases("ban")]
            [Description("Debug the list of bans.")]
            public async Task BanDebug(CommandContext ctx, DiscordUser targetUser = default)
            {
                await DiscordHelpers.SafeTyping(ctx.Channel);

                string strOut = "";
                if (targetUser == default)
                {
                    var banList = Program.db.HashGetAll("bans").ToDictionary();
                    if (banList == null | banList.Keys.Count == 0)
                    {
                        await ctx.RespondAsync("No mutes found in database!");
                        return;
                    }
                    else
                    {
                        foreach (var entry in banList)
                        {
                            strOut += $"{entry.Value}\n";
                        }
                    }
                    if (strOut.Length > 1930)
                    {
                        HasteBinResult hasteResult = await Program.hasteUploader.Post(strOut);
                        if (hasteResult.IsSuccess)
                        {
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} Output exceeded character limit: {hasteResult.FullUrl}.json");
                        }
                        else
                        {
                            Console.WriteLine(strOut);
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Unknown error occurred during upload to Hastebin.\nPlease try again or contact the bot owner.");
                        }
                    }
                    else
                    {
                        await ctx.RespondAsync($"```json\n{strOut}\n```");
                    }
                }
                else // if (targetUser != default)
                {
                    var userMute = Program.db.HashGet("bans", targetUser.Id);
                    if (userMute.IsNull)
                    {
                        await ctx.RespondAsync("That user has no ban registered in the database!");
                    }
                    else
                    {
                        await ctx.RespondAsync($"```json\n{userMute}\n```");
                    }
                }
            }

            [Command("restart")]
            [RequireHomeserverPerm(ServerPermLevel.Admin), Description("Restart the bot. If not under Docker (Cliptok is, dw) this WILL exit instead.")]
            public async Task Restart(CommandContext ctx)
            {
                await ctx.RespondAsync("Now restarting bot.");
                Environment.Exit(1);
            }

            [Command("shutdown")]
            [RequireHomeserverPerm(ServerPermLevel.Admin), Description("Panics and shuts the bot down. Check the arguments for usage.")]
            public async Task Shutdown(CommandContext ctx, [Description("This MUST be set to \"I understand what I am doing\" for the command to work."), RemainingText] string verificationArgument)
            {
                if (verificationArgument == "I understand what I am doing")
                {
                    await ctx.RespondAsync("WARNING: The bot is now shutting down. This action is permanent.");
                    Environment.Exit(0);
                }
                else
                {
                    await ctx.RespondAsync("Invalid argument. Make sure you know what you are doing.");

                };
            }

            [Command("phishing")]
            [RequireHomeserverPerm(ServerPermLevel.Moderator)]
            [Description("Debug the scam list. See also: scamcheck command.")]
            public async Task DebugScams(CommandContext ctx)
            {
                int size = await Program.PhishChecker.DatabaseSize();
                string[] phishingDatabase = await Program.PhishChecker.GetPhishingDomains();

                var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", phishingDatabase)));
                await ctx.RespondAsync(
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Information} The phishing database contains `{size}` domains!")
                        .WithFile("phishes.txt", stream
                    )
                );
            }

            [Command("refresh")]
            [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
            [Description("Manually run all the automatic actions.")]
            public async Task Refresh(CommandContext ctx)
            {
                var msg = await ctx.RespondAsync("Checking for pending scheduled tasks...");
                bool bans = await Tasks.PunishmentTasks.CheckBansAsync();
                bool mutes = await Tasks.PunishmentTasks.CheckMutesAsync();
                bool reminders = await Tasks.ReminderTasks.CheckRemindersAsync();
                bool raidmode = await Tasks.RaidmodeTasks.CheckRaidmodeAsync(ctx.Guild.Id);
                bool unlocks = await Tasks.LockdownTasks.CheckUnlocksAsync();

                await msg.ModifyAsync($"Unban check result: `{bans}`\nUnmute check result: `{mutes}`\nReminders check result: `{reminders}`\nRaidmode check result: `{raidmode}`\nUnlocks check result: `{unlocks}`");
            }

            [Command("sh")]
            [Aliases("cmd")]
            [Description("Run shell commands! Bash for Linux/macOS, batch for Windows!")]
            public async Task Shell(CommandContext ctx, [RemainingText] string command)
            {
                if (ctx.User.Id != 228574821590499329)
                {
                    await ctx.RespondAsync("Nope, you're not Erisa.");
                    return;
                }

                DiscordMessage msg = await ctx.RespondAsync("executing..");

                ShellResult finishedShell = RunShellCommand(command);
                string result = Regex.Replace(finishedShell.result, "ghp_[0-9a-zA-Z]{36}", "ghp_REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_TOKEN"), "REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT"), "REDACTED");

                if (result.Length > 1947)
                {
                    HasteBinResult hasteURL = await Program.hasteUploader.Post(result);
                    if (hasteURL.IsSuccess)
                    {
                        await msg.ModifyAsync($"Done, but output exceeded character limit! (`{result.Length}`/`1947`)\n" +
                            $"Full output can be viewed here: https://haste.erisa.uk/{hasteURL.Key}\nProcess exited with code `{finishedShell.proc.ExitCode}`.");
                    }
                    else
                    {
                        Console.WriteLine(finishedShell.result);
                        await msg.ModifyAsync($"Error occurred during upload to Hastebin.\nAction was executed regardless, shell exit code was `{finishedShell.proc.ExitCode}`. Hastebin status code is `{hasteURL.StatusCode}`.\nPlease check the console/log for the command output.");
                    }
                }
                else
                {
                    await msg.ModifyAsync($"Done, output: ```\n" +
                        $"{result}```Process exited with code `{finishedShell.proc.ExitCode}`.");
                }
            }

            [Command("logs")]
            public async Task Logs(CommandContext ctx)
            {
                await DiscordHelpers.SafeTyping(ctx.Channel);

                string result = Regex.Replace(Program.outputCapture.ToString(), "ghp_[0-9a-zA-Z]{36}", "ghp_REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_TOKEN"), "REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT"), "REDACTED");

                if (result.Length > 1947)
                {
                    HasteBinResult hasteURL = await Program.hasteUploader.Post(result);
                    if (hasteURL.IsSuccess)
                    {
                        await ctx.RespondAsync($"Logs: https://haste.erisa.uk/{hasteURL.Key}");
                    }
                    else
                    {
                        await ctx.RespondAsync($"Error occurred during upload to Hastebin. Hastebin status code is `{hasteURL.StatusCode}`.\n");
                    }
                }
                else
                {
                    await ctx.RespondAsync($"Logs:```\n{result}```");
                }
            }
        }

    }
}
