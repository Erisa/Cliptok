using Microsoft.EntityFrameworkCore;

namespace Cliptok.Commands
{
    public class DebugCmds
    {
        public static Dictionary<ulong, PendingUserOverride> OverridesPendingAddition = new();

        [Command("debugtextcmd")]
        [TextAlias("debug", "troubleshoot", "unbug", "bugn't", "helpsomethinghasgoneverywrong")]
        [Description("Commands and things for fixing the bot in the unlikely event that it breaks a bit.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        class DebugCmd
        {
            [Command("logprint")]
            public async Task LogPrint(TextCommandContext ctx)
            {
                if (!Program.cfgjson.EnablePersistentDb)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Database support is not enabled.");
                    return;
                }

                using (var dbContext = new CliptokDbContext())
                {
                    var records = (await dbContext.Messages.Include(m => m.User).Include(m => m.Sticker).OrderByDescending(m => m.Id).Take(100).ToListAsync());
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(records, Formatting.Indented)));
                    await ctx.RespondAsync(new DiscordMessageBuilder()
                        .WithContent($"100 most recent message logs")
                        .AddFile("messages.json", stream));
                }
            }

            [Command("mutestatus")]
            public async Task MuteStatus(TextCommandContext ctx, DiscordUser targetUser = default)
            {
                if (targetUser == default)
                    targetUser = ctx.User;

                await ctx.RespondAsync(await MuteHelpers.MuteStatusEmbed(targetUser, ctx.Guild));
            }

            [Command("mutes")]
            [TextAlias("mute")]
            [Description("Debug the list of mutes.")]
            public async Task MuteDebug(TextCommandContext ctx, DiscordUser targetUser = default)
            {

                await DiscordHelpers.SafeTyping(ctx.Channel);


                string strOut = "";
                if (targetUser == default)
                {
                    var muteList = (await Program.redis.HashGetAllAsync("mutes")).ToDictionary();
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
                    var hasteResult = await StringHelpers.CodeOrHasteBinAsync(strOut, "json");
                    if (hasteResult.Success)
                        await ctx.RespondAsync(hasteResult.Text);
                    else
                    {
                        var stream = new MemoryStream(Encoding.UTF8.GetBytes(strOut));
                        await ctx.RespondAsync(new DiscordMessageBuilder().AddFile("mutes.json", stream));
                    }
                }
                else // if (targetUser != default)
                {
                    var userMute = Program.redis.HashGet("mutes", targetUser.Id);
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
            [TextAlias("ban")]
            [Description("Debug the list of bans.")]
            public async Task BanDebug(TextCommandContext ctx, DiscordUser targetUser = default)
            {
                await DiscordHelpers.SafeTyping(ctx.Channel);

                string strOut = "";
                if (targetUser == default)
                {
                    var banList = (await Program.redis.HashGetAllAsync("bans")).ToDictionary();
                    if (banList is null | banList.Keys.Count == 0)
                    {
                        await ctx.RespondAsync("No bans found in database!");
                        return;
                    }
                    else
                    {
                        foreach (var entry in banList)
                        {
                            strOut += $"{entry.Value}\n";
                        }
                    }
                    var haste = await StringHelpers.CodeOrHasteBinAsync(strOut, "json");
                    if (haste.Success)
                        await ctx.Channel.SendMessageAsync(haste.Text);
                    else
                    {
                        var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(strOut, Formatting.Indented)));
                        await ctx.Channel.SendMessageAsync(new DiscordMessageBuilder()
                            .AddFile("bans.txt", stream));
                    }

                }
                else // if (targetUser != default)
                {
                    var userMute = Program.redis.HashGet("bans", targetUser.Id);
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
            public async Task Restart(TextCommandContext ctx)
            {
                await ctx.RespondAsync("Bot is restarting. Please hold.");
                Environment.Exit(1);
            }

            [Command("shutdown")]
            [RequireHomeserverPerm(ServerPermLevel.Admin, ownerOverride: true), Description("Panics and shuts the bot down. Check the arguments for usage.")]
            public async Task Shutdown(TextCommandContext ctx, [Description("This MUST be set to \"I understand what I am doing\" for the command to work."), RemainingText] string verificationArgument)
            {
                if (verificationArgument == "I understand what I am doing")
                {
                    await ctx.RespondAsync("**The bot is now shutting down. This action is permanent. You will have to start it up again manually.**");
                    Environment.Exit(0);
                }
                else
                {
                    await ctx.RespondAsync("Invalid argument. Make sure you know what you are doing.");

                }
                ;
            }

            [Command("refresh")]
            [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
            [Description("Manually run all the automatic actions.")]
            public async Task Refresh(TextCommandContext ctx)
            {
                await ctx.RespondAsync("Checking for pending scheduled tasks...");
                var msg = await ctx.GetResponseAsync();
                bool bans = await Tasks.PunishmentTasks.CheckBansAsync();
                bool mutes = await Tasks.PunishmentTasks.CheckMutesAsync();
                bool punishmentMessages = await Tasks.PunishmentTasks.CleanUpPunishmentMessagesAsync();
                bool reminders = await Tasks.ReminderTasks.CheckRemindersAsync();
                bool raidmode = await Tasks.RaidmodeTasks.CheckRaidmodeAsync(ctx.Guild.Id);
                bool unlocks = await Tasks.LockdownTasks.CheckUnlocksAsync();
                bool channelUpdateEvents = await Tasks.EventTasks.HandlePendingChannelUpdateEventsAsync();
                bool channelDeleteEvents = await Tasks.EventTasks.HandlePendingChannelDeleteEventsAsync();
                bool checkAndMassDehoist = await Tasks.MassDehoistTasks.CheckAndMassDehoistTask();

                await msg.ModifyAsync($"Unban check result: `{bans}`\nUnmute check result: `{mutes}`\nPunishment message cleanup check result: `{punishmentMessages}`\nReminders check result: `{reminders}`\nRaidmode check result: `{raidmode}`\nUnlocks check result: `{unlocks}`\nPending Channel Update events check result: `{channelUpdateEvents}`\nPending Channel Delete events check result: `{channelDeleteEvents}`\nMass dehoist check result: `{checkAndMassDehoist}`");
            }

            [Command("sh")]
            [TextAlias("cmd")]
            [IsBotOwner]
            [Description("Run shell commands! Bash for Linux/macOS, batch for Windows!")]
            public async Task Shell(TextCommandContext ctx, [RemainingText] string command)
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Please try actually giving a command.");
                    return;
                }

                await ctx.RespondAsync("executing..");
                DiscordMessage msg = await ctx.GetResponseAsync();

                ShellResult finishedShell = RunShellCommand(command);
                string result = Regex.Replace(finishedShell.result, "ghp_[0-9a-zA-Z]{36}", "ghp_REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_TOKEN"), "REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") ?? "DUMMYVALUE", "REDACTED");

                string msgContent = (await StringHelpers.CodeOrHasteBinAsync(result, charLimit: 1947)).Text;

                msgContent += $"\nProcess exited with code `{finishedShell.proc.ExitCode}`.";

                await msg.ModifyAsync(msgContent);
            }

            [Command("logs")]
            public async Task Logs(TextCommandContext ctx)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command has been removed! Please find logs through other means.");
            }

            [Command("dumpwarnings"), Description("Dump all warning data. EXTREMELY computationally expensive, use with caution.")]
            [IsBotOwner]
            [RequireHomeserverPerm(ServerPermLevel.Moderator)]
            public async Task MostWarningsCmd(TextCommandContext ctx)
            {
                await DiscordHelpers.SafeTyping(ctx.Channel);

                var server = Program.redisConnection.GetServer(Program.redisConnection.GetEndPoints()[0]);
                var keys = server.Keys();

                Dictionary<string, Dictionary<long, MemberPunishment>> warningdata = new();
                foreach (var key in keys)
                {
                    if (ulong.TryParse(key.ToString(), out ulong number))
                    {
                        var warnings = await Program.redis.HashGetAllAsync(key);
                        Dictionary<long, MemberPunishment> warningdict = new();
                        foreach (var warning in warnings)
                        {
                            var warnobject = JsonConvert.DeserializeObject<MemberPunishment>(warning.Value);
                            warningdict[(long)warning.Name] = warnobject;
                        }
                        warningdata[key.ToString()] = warningdict;
                    }
                }
                StringWriter dummyWriter = new();
                dummyWriter.Flush();

                var output = JsonConvert.SerializeObject(warningdata, Formatting.Indented);
                dummyWriter.Write(output);
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(dummyWriter.ToString()));
                await ctx.RespondAsync(new DiscordMessageBuilder().AddFile("warnings.json", stream).WithContent("I'm not so sure this was a good idea.."));
            }

            [Command("checkpendingchannelevents")]
            [TextAlias("checkpendingevents", "pendingevents")]
            [Description("Check pending events to handle in the Channel Update and Channel Delete handlers.")]
            [IsBotOwner]
            public async Task CheckPendingChannelEvents(TextCommandContext ctx)
            {
                var pendingUpdateEvents = Tasks.EventTasks.PendingChannelUpdateEvents;
                var pendingDeleteEvents = Tasks.EventTasks.PendingChannelDeleteEvents;

                if (pendingUpdateEvents.Count == 0 && pendingDeleteEvents.Count == 0)
                {
                    await ctx.RespondAsync("There are no pending channel events left to handle!");
                    return;
                }

                string list = "";
                if (pendingUpdateEvents.Count > 0)
                {
                    list += "Channel Update:\n```\n";
                    foreach (var e in pendingUpdateEvents)
                    {
                        list += $"{e.Key.ToString("o")}, {e.Value.ChannelAfter.Id}\n";
                    }
                    list += "```";
                }

                if (pendingDeleteEvents.Count > 0)
                {
                    list += "\nChannel Delete:\n```\n";
                    foreach (var e in pendingDeleteEvents)
                    {
                        list += $"{e.Key.ToString("o")}, {e.Value.Channel.Id}\n";
                    }
                    list += "```\n";
                }
                var haste = await StringHelpers.CodeOrHasteBinAsync(list, "json");
                if (haste.Success)
                    await ctx.Channel.SendMessageAsync(haste.Text);
                else
                {
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(list, Formatting.Indented)));
                    await ctx.Channel.SendMessageAsync(new DiscordMessageBuilder()
                        .AddFile("events.txt", stream));
                }
            }

            [Command("dmchannel")]
            [Description("Create or find a DM channel ID for a user.")]
            [IsBotOwner]
            public async Task GetDMChannel(TextCommandContext ctx, DiscordUser user)
            {
                var dmChannel = await user.CreateDmChannelAsync();
                await ctx.RespondAsync(dmChannel.Id.ToString());
            }

            [Command("dumpdmchannels")]
            [Description("Dump all DM channels")]
            [IsBotOwner]
            public async Task DumpDMChannels(TextCommandContext ctx)
            {
                var dmChannels = ctx.Client.PrivateChannels;

                var json = JsonConvert.SerializeObject(dmChannels, Formatting.Indented);

                var haste = await StringHelpers.CodeOrHasteBinAsync(json, "json");
                if (haste.Success)
                    await ctx.Channel.SendMessageAsync(haste.Text);
                else
                {
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(json, Formatting.Indented)));
                    await ctx.Channel.SendMessageAsync(new DiscordMessageBuilder()
                        .AddFile("members.json", stream));
                }
            }

            [Command("searchmembers")]
            [Description("Search member list with a regex. Restricted to bot owners bc regexes are scary.")]
            [IsBotOwner]
            public async Task SearchMembersCmd(TextCommandContext ctx, string regex)
            {
                var rx = new Regex(regex);

                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it. This will take a while.");
                var msg = await ctx.GetResponseAsync();
                var discordMembers = await ctx.Guild.GetAllMembersAsync().ToListAsync();

                var matchedMembers = discordMembers.Where(discordMember => discordMember.Username is not null && rx.IsMatch(discordMember.Username)).ToList();

                Dictionary<ulong, string> memberIdsTonames = matchedMembers.Select(member => new KeyValuePair<ulong, string>(member.Id, member.Username)).ToDictionary(x => x.Key, x => x.Value);

                _ = msg.DeleteAsync();
                var json = JsonConvert.SerializeObject(memberIdsTonames, Formatting.Indented);
                var haste = await StringHelpers.CodeOrHasteBinAsync(json, "json");
                if (haste.Success)
                    await ctx.Channel.SendMessageAsync(haste.Text);
                else
                {
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(json, Formatting.Indented)));
                    await ctx.Channel.SendMessageAsync(new DiscordMessageBuilder()
                        .AddFile("members.json", stream));
                }
            }

            [Command("testnre")]
            [Description("throw a System.NullReferenceException error. dont spam this please.")]
            [IsBotOwner]
            public async Task ThrowNRE(TextCommandContext ctx, bool catchAsWarning = false)
            {
                if (catchAsWarning)
                {
                    try
                    {
                        throw new NullReferenceException();
                    }
                    catch (NullReferenceException e)
                    {
                        ctx.Client.Logger.LogWarning(e, "logging test NRE as warning");
                        await ctx.RespondAsync("thrown NRE and logged as warning, check logs");
                    }
                }
                else
                {
                    throw new NullReferenceException();
                }
            }

            [Command("warningcache")]
            [Description("Dump the most recent manual warning")]
            public async Task WarningCacheCmd(TextCommandContext ctx)
            {
                if (WarningHelpers.mostRecentWarning is null)
                {
                    await ctx.RespondAsync("No cached warning found.");
                    return;
                }
                await ctx.RespondAsync((await StringHelpers.CodeOrHasteBinAsync(JsonConvert.SerializeObject(WarningHelpers.mostRecentWarning, Formatting.Indented), "json")).Text);
            }

            [Command("bancache")]
            [Description("Dump the most recent manual warning")]
            public async Task BanCacheCmd(TextCommandContext ctx)
            {
                if (BanHelpers.MostRecentBan is null)
                {
                    await ctx.RespondAsync("No cached ban found.");
                    return;
                }
                await ctx.RespondAsync((await StringHelpers.CodeOrHasteBinAsync(JsonConvert.SerializeObject(BanHelpers.MostRecentBan, Formatting.Indented), "json")).Text);
            }


            [Command("mutecache")]
            [Description("Dump the most recent manual warning")]
            public async Task MuteCacheCmd(TextCommandContext ctx)
            {
                if (MuteHelpers.MostRecentMute is null)
                {
                    await ctx.RespondAsync("No cached mute found.");
                    return;
                }
                await ctx.RespondAsync((await StringHelpers.CodeOrHasteBinAsync(JsonConvert.SerializeObject(MuteHelpers.MostRecentMute, Formatting.Indented), "json")).Text);
            }

        }

        class OverridesCmd
        {
            // This is outside of the debug class/group to avoid issues caused by DSP.Commands that are out of our control
            [Command("debugoverrides")]
            [TextAlias("overrides")]
            [Description("Commands for managing stored permission overrides.")]
            [AllowedProcessors(typeof(TextCommandProcessor))]
            [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
            public class Overrides
            {
                [DefaultGroupCommand]
                [Command("show")]
                public async Task ShowOverrides(TextCommandContext ctx,
                    [Description("The user whose overrides to show.")] DiscordUser user)
                {
                    var userOverrides = await Program.redis.HashGetAsync("overrides", user.Id.ToString());
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
                        var allowedPermissions = string.IsNullOrWhiteSpace(overwrite.Value.Allowed.ToString("name")) ? "none" : overwrite.Value.Allowed.ToString("name");
                        var deniedPermissions = string.IsNullOrWhiteSpace(overwrite.Value.Denied.ToString("name")) ? "none" : overwrite.Value.Denied.ToString("name");

                        response +=
                            $"<#{overwrite.Key}>:\n**Allowed**: {allowedPermissions}\n**Denied**: {deniedPermissions}\n\n";
                    }

                    if (response.Length > 2000)
                    {
                        // I am abusing my own helper here. I know for a fact that it will be over the char limit so I know it won't return a code block.
                        await ctx.RespondAsync((await StringHelpers.CodeOrHasteBinAsync(response)).Text);
                    }
                    else
                    {
                        await ctx.RespondAsync(new DiscordMessageBuilder().WithContent(response)
                            .WithAllowedMentions(Mentions.None));
                    }
                }

                [Command("import")]
                [Description("Import overrides from a channel to the database.")]
                public async Task Import(TextCommandContext ctx,
                    [Description("The channel to import overrides from.")] DiscordChannel channel)
                {
                    // Import overrides
                    var (success, failedOverwrite) = await ImportOverridesFromChannelAsync(channel);

                    if (success)
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Overrides for {channel.Mention} imported successfully!");
                    else
                        await ctx.RespondAsync(
                            $"{Program.cfgjson.Emoji.Error} Something went wrong while trying to fetch the overrides for {failedOverwrite}!" +
                            " There are overrides in the database but I could not parse them. Check the database manually for details.");
                }

                [Command("importall")]
                [Description("Import all overrides from all channels to the database.")]
                public async Task ImportAll(TextCommandContext ctx)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working...");
                    var msg = await ctx.GetResponseAsync();

                    // Get all channels
                    var channels = await ctx.Guild.GetChannelsAsync();

                    bool anyImportFailed = false;

                    foreach (var channel in channels)
                    {
                        // Import overrides
                        var (success, failedOverwrite) = await ImportOverridesFromChannelAsync(channel);

                        if (!success) anyImportFailed = true;
                    }

                    if (anyImportFailed)
                        await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} Some overrides failed to import. Most likely this means I found overrides in the database but couldn't parse them. Check the database manually for details.");
                    else
                        await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} All overrides imported successfully!");
                }

                [Command("add")]
                [Description("Insert an override into the db. Useful if you want to add an override for a user who has left.")]
                [IsBotOwner]
                public async Task Add(TextCommandContext ctx,
                    [Description("The user to add an override for.")] DiscordUser user,
                    [Description("The channel to add the override to.")] DiscordChannel channel,
                    [Description("Allowed permissions. Use a permission integer. See https://discordlookup.com/permissions-calculator.")] int allowedPermissions,
                    [Description("Denied permissions. Use a permission integer. See https://discordlookup.com/permissions-calculator.")] int deniedPermissions)
                {
                    // Confirm permission overrides before we do anything.
                    var parsedAllowedPerms = new DiscordPermissions(allowedPermissions);
                    var parsedDeniedPerms = new DiscordPermissions(deniedPermissions);

                    var allowedPermsStr = parsedAllowedPerms.ToString("name");
                    if (string.IsNullOrWhiteSpace(allowedPermsStr))
                        allowedPermsStr = "None";

                    var deniedPermsStr = parsedDeniedPerms.ToString("name");
                    if (string.IsNullOrWhiteSpace(deniedPermsStr))
                        deniedPermsStr = "None";

                    var confirmButton = new DiscordButtonComponent(DiscordButtonStyle.Success, "debug-overrides-add-confirm-callback", "Yes");
                    var cancelButton = new DiscordButtonComponent(DiscordButtonStyle.Danger, "debug-overrides-add-cancel-callback", "No");

                    await ctx.RespondAsync(new DiscordMessageBuilder().WithContent(
                            $"{Program.cfgjson.Emoji.ShieldHelp} Just to confirm, you want to add the following override for {user.Mention} to {channel.Mention}?\n" +
                            $"**Allowed:** {allowedPermsStr}\n" +
                            $"**Denied:** {deniedPermsStr}\n")
                        .AddActionRowComponent([confirmButton, cancelButton]));
                    var confirmationMessage = await ctx.GetResponseAsync();

                    OverridesPendingAddition.Add(confirmationMessage.Id, new PendingUserOverride
                    {
                        ChannelId = channel.Id,
                        Overwrite = new MockUserOverwrite
                        {
                            Id = user.Id,
                            Allowed = parsedAllowedPerms,
                            Denied = parsedDeniedPerms
                        }
                    });
                }

                [Command("remove")]
                [Description("Remove a user's overrides for a channel from the database.")]
                public async Task Remove(TextCommandContext ctx,
                    [Description("The user whose overrides to remove.")] DiscordUser user,
                    [Description("The channel to remove overrides from.")] DiscordChannel channel)
                {
                    // Remove user's overrides for channel from db
                    foreach (var overwriteHash in await Program.redis.HashGetAllAsync("overrides"))
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
                                await Program.redis.HashSetAsync("overrides", user.Id,
                                    JsonConvert.SerializeObject(overwriteDict));
                            else
                                await Program.redis.HashDeleteAsync("overrides", user.Id);
                        }
                    }

                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Overrides for {user.Mention} in {channel.Mention} removed successfully!");
                }

                // This is the same as what happens on GuildMemberAdded. Command is here to allow for manual sync/apply if needed.
                [Command("apply")]
                [Description("Apply a user's overrides from the db.")]
                [IsBotOwner]
                public async Task Apply(TextCommandContext ctx,
                    [Description("The user whose overrides to apply.")] DiscordUser user)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it...");
                    var msg = await ctx.GetResponseAsync();

                    // Try fetching member to determine whether they are in the server. If they are not, we can't apply overrides for them.
                    DiscordMember member;
                    try
                    {
                        member = await ctx.Guild.GetMemberAsync(user.Id);
                    }
                    catch (DSharpPlus.Exceptions.NotFoundException)
                    {
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user isn't in the server! I can't apply overrides for them.");
                        return;
                    }

                    var userOverwrites = await Program.redis.HashGetAsync("overrides", member.Id.ToString());
                    if (string.IsNullOrWhiteSpace(userOverwrites))
                    {
                        // User has no overrides saved
                        await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} {user.Mention} has no overrides to apply!");
                        return;
                    }
                    var dictionary = JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(userOverwrites);
                    if (dictionary is null)
                    {
                        // User has no overrides saved
                        await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} {user.Mention} has no overrides to apply!");
                        return;
                    }
                    var numAppliedOverrides = dictionary.Count;

                    foreach (var overwrite in dictionary)
                    {
                        DiscordChannel channel;
                        try
                        {
                            channel = await Program.discord.GetChannelAsync(overwrite.Key);
                        }
                        catch
                        {
                            continue;
                        }

                        try
                        {
                            await channel.AddOverwriteAsync(member, overwrite.Value.Allowed, overwrite.Value.Denied, "Restoring saved overrides for member.");
                        }
                        catch (DSharpPlus.Exceptions.UnauthorizedException)
                        {
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to add overrides in {channel.Mention}! Continuing...");
                            numAppliedOverrides--;
                        }

                    }

                    await msg.ModifyAsync(x => x.Content = $"{Program.cfgjson.Emoji.Success} Successfully applied {numAppliedOverrides}/{dictionary.Count} overrides for {user.Mention}!");
                }

                [Command("dump")]
                [Description("Dump all of a channel's overrides from Discord or the database.")]
                [IsBotOwner]
                [AllowedProcessors(typeof(TextCommandProcessor))]
                public class DumpChannelOverrides
                {
                    [DefaultGroupCommand]
                    [Command("discord")]
                    [Description("Dump all of a channel's overrides as they exist on the Discord channel. Does not read from db.")]
                    public async Task DumpFromDiscord(TextCommandContext ctx,
                        [Description("The channel to dump overrides for.")] DiscordChannel channel)
                    {
                        var overwrites = channel.PermissionOverwrites;

                        string output = "";
                        foreach (var overwrite in overwrites)
                        {
                            output += $"{JsonConvert.SerializeObject(overwrite)}\n";
                        }

                        await ctx.RespondAsync($"Dump from Discord:\n{(await StringHelpers.CodeOrHasteBinAsync(output, "json")).Text}");
                    }

                    [Command("db")]
                    [TextAlias("database")]
                    [Description("Dump all of a channel's overrides as they are stored in the db.")]
                    public async Task DumpFromDb(TextCommandContext ctx,
                        [Description("The channel to dump overrides for.")] DiscordChannel channel)
                    {
                        List<DiscordOverwrite> overwrites = new();
                        try
                        {
                            var allOverwrites = await Program.redis.HashGetAllAsync("overrides");
                            foreach (var overwrite in allOverwrites)
                            {
                                var overwriteDict = JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(overwrite.Value);
                                if (overwriteDict is null) continue;
                                if (overwriteDict.TryGetValue(channel.Id, out var value))
                                    overwrites.Add(value);
                            }
                        }
                        catch (Exception ex)
                        {
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Something went wrong while trying to fetch the overrides for {channel.Mention}!" +
                                                  " There are overrides in the database but I could not parse them. Check the database manually for details.");

                            Program.discord.Logger.LogError(ex, "Failed to read overrides from db for 'debug overrides dump'!");

                            return;
                        }

                        if (overwrites.Count == 0)
                        {
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} No overrides found for {channel.Mention} in the database!");
                            return;
                        }

                        string output = "";
                        foreach (var overwrite in overwrites)
                        {
                            output += $"{JsonConvert.SerializeObject(overwrite)}\n";
                        }

                        await ctx.RespondAsync($"Dump from db:\n{(await StringHelpers.CodeOrHasteBinAsync(output, "json")).Text}");
                    }
                }

                [Command("cleanup")]
                [TextAlias("clean", "prune")]
                [Description("Removes overrides from the db for channels that no longer exist.")]
                [IsBotOwner]
                public async Task CleanUpOverrides(CommandContext ctx)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it...");
                    var msg = await ctx.GetResponseAsync();
                    var removedOverridesCount = 0;

                    var dbOverwrites = await Program.redis.HashGetAllAsync("overrides");
                    foreach (var userOverwrites in dbOverwrites)
                    {
                        var overwriteDict = JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(userOverwrites.Value);
                        foreach (var overwrite in overwriteDict)
                        {
                            bool channelExists = Program.discord.Guilds.Any(g => g.Value.Channels.Any(c => c.Key == overwrite.Key));

                            if (!channelExists)
                            {
                                // Channel no longer exists, remove the override
                                overwriteDict.Remove(overwrite.Key);
                                removedOverridesCount++;
                            }
                        }

                        // Write back to db
                        // If the user now has no overrides, remove them from the db entirely
                        if (overwriteDict.Count == 0)
                        {
                            await Program.redis.HashDeleteAsync("overrides", userOverwrites.Name);
                        }
                        else
                        {
                            // Otherwise, update the user's overrides in the db
                            await Program.redis.HashSetAsync("overrides", userOverwrites.Name, JsonConvert.SerializeObject(overwriteDict));
                        }
                    }

                    await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Done! Cleaned up {removedOverridesCount} overrides.");
                }
            }

            private static async Task<(bool success, ulong failedOverwrite)> ImportOverridesFromChannelAsync(DiscordChannel channel)
            {
                // Imports overrides from the specified channel to the database. See 'debug overrides import' and 'debug overrides importall'
                // Return (true, 0) on success, (false, <ID of failed overwrite>) on failure

                // Import all overrides for channel to db
                foreach (var overwrite in channel.PermissionOverwrites)
                {
                    // Ignore role overrides
                    if (overwrite.Type == DiscordOverwriteType.Role) continue;

                    // Get user's current overrides from db
                    var userOverrides = await Program.redis.HashGetAsync("overrides", overwrite.Id.ToString());
                    if (string.IsNullOrWhiteSpace(userOverrides))
                    {
                        // User doesn't have any overrides in db, so just add the new one
                        await Program.redis.HashSetAsync("overrides", overwrite.Id.ToString(),
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
                            return (false, overwrite.Id);
                        }

                        if (overwrites.ContainsKey(channel.Id))
                            overwrites[channel.Id] = overwrite;
                        else
                            overwrites.Add(channel.Id, overwrite);

                        // Update the db
                        await Program.redis.HashSetAsync("overrides", overwrite.Id.ToString(),
                            JsonConvert.SerializeObject(overwrites));
                    }
                }

                return (true, 0);
            }
        }
    }
}