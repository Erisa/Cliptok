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
            [Command("mutestatus")]
            public async Task MuteStatus(CommandContext ctx, DiscordUser targetUser = default)
            {
                if (targetUser == default)
                    targetUser = ctx.User;

                await ctx.RespondAsync(await MuteHelpers.MuteStatusEmbed(targetUser, ctx.Guild));
            }

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
                    if (muteList is null | muteList.Keys.Count == 0)
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
                    if (banList is null | banList.Keys.Count == 0)
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
            [RequireHomeserverPerm(ServerPermLevel.Admin, ownerOverride: true), Description("Restart the bot. If not under Docker (Cliptok is, dw) this WILL exit instead.")]
            public async Task Restart(CommandContext ctx)
            {
                await ctx.RespondAsync("Bot is restarting. Please hold.");
                Environment.Exit(1);
            }

            [Command("shutdown")]
            [RequireHomeserverPerm(ServerPermLevel.Admin, ownerOverride: true), Description("Panics and shuts the bot down. Check the arguments for usage.")]
            public async Task Shutdown(CommandContext ctx, [Description("This MUST be set to \"I understand what I am doing\" for the command to work."), RemainingText] string verificationArgument)
            {
                if (verificationArgument == "I understand what I am doing")
                {
                    await ctx.RespondAsync("**The bot is now shutting down. This action is permanent. You will have to start it up again manually.**");
                    Environment.Exit(0);
                }
                else
                {
                    await ctx.RespondAsync("Invalid argument. Make sure you know what you are doing.");

                };
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
            [IsBotOwner]
            [Description("Run shell commands! Bash for Linux/macOS, batch for Windows!")]
            public async Task Shell(CommandContext ctx, [RemainingText] string command)
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Please try actually giving a command.");
                    return;
                }

                DiscordMessage msg = await ctx.RespondAsync("executing..");

                ShellResult finishedShell = RunShellCommand(command);
                string result = Regex.Replace(finishedShell.result, "ghp_[0-9a-zA-Z]{36}", "ghp_REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_TOKEN"), "REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") ?? "DUMMYVALUE", "REDACTED");

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

            [Group("overrides")]
            [Description("Commands for managing stored permission overrides.")]
            public class Overrides : BaseCommandModule
            {
                [GroupCommand]
                public async Task ShowOverrides(CommandContext ctx,
                    [Description("The user whose overrides to show.")] DiscordUser user)
                {
                    var userOverrides = await Program.db.HashGetAsync("overrides", user.Id.ToString());
                    if (string.IsNullOrWhiteSpace(userOverrides))
                    {
                        await ctx.RespondAsync(
                            new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} {user.Mention} doesn't have any overrides set!")
                                .WithAllowedMentions(Mentions.None));
                        return;
                    }

                    var overwrites = JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(userOverrides);
                    if (overwrites is null)
                    {
                        await ctx.RespondAsync(
                            $"{Program.cfgjson.Emoji.Error} Something went wrong while trying to fetch the overrides for {user.Mention}!" +
                            " There are overrides in the database but I could not parse them. Check the database manually for details.");
                        return;
                    }

                    if (overwrites.Count < 1)
                    {
                        await ctx.RespondAsync(
                            new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Error} {user.Mention} doesn't have any overrides set!")
                                .WithAllowedMentions(Mentions.None));
                        return;
                    }

                    var response = $"**Overrides for {user.Mention}:**\n\n";
                    foreach (var overwrite in overwrites)
                    {
                        response +=
                            $"<#{overwrite.Key}>:\n**Allowed**: {overwrite.Value.Allowed}\n**Denied**: {overwrite.Value.Denied}\n\n";
                    }

                    await ctx.RespondAsync(new DiscordMessageBuilder().WithContent(response)
                        .WithAllowedMentions(Mentions.None));
                }

                [Command("import")]
                [Description("Import overrides from a channel to the database.")]
                public async Task Import(CommandContext ctx,
                    [Description("The channel to import overrides from.")] DiscordChannel channel)
                {
                    // Import all overrides for channel to db
                    foreach (var overwrite in channel.PermissionOverwrites)
                    {
                        // Ignore role overrides
                        if (overwrite.Type == OverwriteType.Role) continue;

                        // Get user's current overrides from db
                        var userOverrides = await Program.db.HashGetAsync("overrides", overwrite.Id.ToString());
                        if (string.IsNullOrWhiteSpace(userOverrides))
                        {
                            // User doesn't have any overrides in db, so just add the new one
                            await Program.db.HashSetAsync("overrides", overwrite.Id.ToString(),
                                JsonConvert.SerializeObject(new Dictionary<ulong, DiscordOverwrite>
                                {
                                    { channel.Id, overwrite }
                                }));
                        }
                        else
                        {
                            // User has overrides in db, so add the new one to the existing ones
                            var overwrites =
                                JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(userOverrides);
                            if (overwrites is null)
                            {
                                await ctx.RespondAsync(
                                    $"{Program.cfgjson.Emoji.Error} Something went wrong while trying to fetch the overrides for {overwrite.Id}!" +
                                    " There are overrides in the database but I could not parse them. Check the database manually for details.");
                                return;
                            }

                            if (overwrites.ContainsKey(channel.Id))
                                overwrites[channel.Id] = overwrite;
                            else
                                overwrites.Add(channel.Id, overwrite);

                            // Update the db
                            await Program.db.HashSetAsync("overrides", overwrite.Id.ToString(),
                                JsonConvert.SerializeObject(overwrites));
                        }
                    }

                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Overrides for {channel.Mention} imported successfully!");
                }

                [Command("remove")]
                [Description("Remove a user's overrides for a channel from the database.")]
                public async Task Remove(CommandContext ctx,
                    [Description("The user whose overrides to remove.")] DiscordUser user,
                    [Description("The channel to remove overrides from.")] DiscordChannel channel)
                {
                    // Remove user's overrides for channel from db
                    foreach (var overwriteHash in await Program.db.HashGetAllAsync("overrides"))
                    {
                        var overwriteDict =
                            JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(
                                overwriteHash.Value);
                        if (overwriteDict is null) continue;

                        foreach (var overwrite in overwriteDict.Where(overwrite =>
                                     overwrite.Value.Id == user.Id && overwrite.Key == channel.Id))
                        {
                            overwriteDict.Remove(overwrite.Key);

                            if (overwriteDict.Count > 0)
                                await Program.db.HashSetAsync("overrides", user.Id,
                                    JsonConvert.SerializeObject(overwriteDict));
                            else
                                await Program.db.HashDeleteAsync("overrides", user.Id);
                        }
                    }

                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Overrides for {user.Mention} in {channel.Mention} removed successfully!");
                }
            }
        }

    }
}
