namespace Cliptok.Commands
{
    internal class Debug : BaseCommandModule
    {
        public static Dictionary<ulong, PendingUserOverride> OverridesPendingAddition = new();

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
                    await ctx.RespondAsync(await StringHelpers.CodeOrHasteBinAsync(strOut, "json"));
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
                    await ctx.RespondAsync(await StringHelpers.CodeOrHasteBinAsync(strOut, "json"));
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
                bool warns = await Tasks.PunishmentTasks.CheckAutomaticWarningsAsync();
                bool reminders = await Tasks.ReminderTasks.CheckRemindersAsync();
                bool raidmode = await Tasks.RaidmodeTasks.CheckRaidmodeAsync(ctx.Guild.Id);
                bool unlocks = await Tasks.LockdownTasks.CheckUnlocksAsync();
                bool channelUpdateEvents = await Tasks.EventTasks.HandlePendingChannelUpdateEventsAsync();
                bool channelDeleteEvents = await Tasks.EventTasks.HandlePendingChannelDeleteEventsAsync();

                await msg.ModifyAsync($"Unban check result: `{bans}`\nUnmute check result: `{mutes}`\nAutomatic warning message check result: `{warns}`\nReminders check result: `{reminders}`\nRaidmode check result: `{raidmode}`\nUnlocks check result: `{unlocks}`\nPending Channel Update events check result: `{channelUpdateEvents}`\nPending Channel Delete events check result: `{channelDeleteEvents}`");
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

                string msgContent = await StringHelpers.CodeOrHasteBinAsync(result, charLimit: 1947);

                msgContent += $"\nProcess exited with code `{finishedShell.proc.ExitCode}`.";

                await msg.ModifyAsync(msgContent);
            }

            [Command("logs")]
            public async Task Logs(CommandContext ctx)
            {
                if (Program.cfgjson.LogLevel is Level.Verbose)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Verbose logging is enabled, so the in-memory logger is disabled. Please access the logs through another method.");
                    return;
                }

                await DiscordHelpers.SafeTyping(ctx.Channel);

                string result = Regex.Replace(Program.outputCapture.ToString(), "ghp_[0-9a-zA-Z]{36}", "ghp_REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_TOKEN"), "REDACTED");

                if (Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") is not null)
                {
                    result = result.Replace(Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT"), "REDACTED");
                }

                await ctx.RespondAsync(await StringHelpers.CodeOrHasteBinAsync(result));
            }

            [Command("dumpwarnings"), Description("Dump all warning data. EXTREMELY computationally expensive, use with caution.")]
            [IsBotOwner]
            [RequireHomeserverPerm(ServerPermLevel.Moderator)]
            public async Task MostWarningsCmd(CommandContext ctx)
            {
                await DiscordHelpers.SafeTyping(ctx.Channel);

                var server = Program.redis.GetServer(Program.redis.GetEndPoints()[0]);
                var keys = server.Keys();

                Dictionary<string, Dictionary<long, MemberPunishment>> warningdata = new();
                foreach (var key in keys)
                {
                    if (ulong.TryParse(key.ToString(), out ulong number))
                    {
                        var warnings = Program.db.HashGetAll(key);
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
            [Aliases("checkpendingevents", "pendingevents")]
            [Description("Check pending events to handle in the Channel Update and Channel Delete handlers.")]
            [IsBotOwner]
            public async Task CheckPendingChannelEvents(CommandContext ctx)
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

                await ctx.RespondAsync(await StringHelpers.CodeOrHasteBinAsync(list));
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

                    if (response.Length > 2000)
                    {
                        // I am abusing my own helper here. I know for a fact that it will be over the char limit so I know it won't return a code block.
                        await ctx.RespondAsync(await StringHelpers.CodeOrHasteBinAsync(response));
                    }
                    else
                    {
                        await ctx.RespondAsync(new DiscordMessageBuilder().WithContent(response)
                            .WithAllowedMentions(Mentions.None));
                    }
                }

                [Command("import")]
                [Description("Import overrides from a channel to the database.")]
                public async Task Import(CommandContext ctx,
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
                public async Task ImportAll(CommandContext ctx)
                {
                    var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working...");

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
                public async Task Add(CommandContext ctx,
                    [Description("The user to add an override for.")] DiscordUser user,
                    [Description("The channel to add the override to.")] DiscordChannel channel,
                    [Description("Allowed permissions. Use a permission integer. See https://discordlookup.com/permissions-calculator.")] int allowedPermissions,
                    [Description("Denied permissions. Use a permission integer. See https://discordlookup.com/permissions-calculator.")] int deniedPermissions)
                {
                    // Confirm permission overrides before we do anything.
                    var parsedAllowedPerms = (DiscordPermissions)allowedPermissions;
                    var parsedDeniedPerms = (DiscordPermissions)deniedPermissions;

                    var confirmButton = new DiscordButtonComponent(DiscordButtonStyle.Success, "debug-overrides-add-confirm-callback", "Yes");
                    var cancelButton = new DiscordButtonComponent(DiscordButtonStyle.Danger, "debug-overrides-add-cancel-callback", "No");

                    var confirmationMessage = await ctx.RespondAsync(new DiscordMessageBuilder().WithContent(
                            $"{Program.cfgjson.Emoji.ShieldHelp} Just to confirm, you want to add the following override for {user.Mention} to {channel.Mention}?\n" +
                            $"**Allowed:** {parsedAllowedPerms}\n" +
                            $"**Denied:** {parsedDeniedPerms}\n")
                        .AddComponents([confirmButton, cancelButton]));

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

                // This is the same as what happens on GuildMemberAdded. Command is here to allow for manual sync/apply if needed.
                [Command("apply")]
                [Description("Apply a user's overrides from the db.")]
                [IsBotOwner]
                public async Task Apply(CommandContext ctx,
                    [Description("The user whose overrides to apply.")] DiscordUser user)
                {
                    var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it...");

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

                    var userOverwrites = await Program.db.HashGetAsync("overrides", member.Id.ToString());
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
            }

            [Command("dumpchanneloverrides")]
            [Description("Dump all of a channel's overrides. This pulls from Discord, not the database.")]
            [IsBotOwner]
            public async Task DumpChannelOverrides(CommandContext ctx,
                [Description("The channel to dump overrides for.")] DiscordChannel channel)
            {
                var overwrites = channel.PermissionOverwrites;

                string output = "";
                foreach (var overwrite in overwrites)
                {
                    output += $"{JsonConvert.SerializeObject(overwrite)}\n";
                }

                await ctx.RespondAsync(await StringHelpers.CodeOrHasteBinAsync(output, "json"));
            }

            [Command("dmchannel")]
            [Description("Create or find a DM channel ID for a user.")]
            [IsBotOwner]
            public async Task GetDMChannel(CommandContext ctx, DiscordUser user)
            {
                var dmChannel = await user.CreateDmChannelAsync();
                await ctx.RespondAsync(dmChannel.Id.ToString());
            }

            [Command("dumpdmchannels")]
            [Description("Dump all DM channels")]
            [IsBotOwner]
            public async Task DumpDMChannels(CommandContext ctx)
            {
                var dmChannels = ctx.Client.PrivateChannels;

                var json = JsonConvert.SerializeObject(dmChannels, Formatting.Indented);

                await ctx.RespondAsync(await StringHelpers.CodeOrHasteBinAsync(json, "json"));
            }

            [Command("searchmembers")]
            [Description("Search member list with a regex. Restricted to bot owners bc regexes are scary.")]
            [IsBotOwner]
            public async Task SearchMembersCmd(CommandContext ctx, string regex)
            {
                var rx = new Regex(regex);

                var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it. This will take a while.");
                var discordMembers = await ctx.Guild.GetAllMembersAsync().ToListAsync();

                var matchedMembers = discordMembers.Where(discordMember => discordMember.Username is not null && rx.IsMatch(discordMember.Username)).ToList();

                Dictionary<ulong, string> memberIdsTonames = matchedMembers.Select(member => new KeyValuePair<ulong, string>(member.Id, member.Username)).ToDictionary(x => x.Key, x => x.Value);

                _ = msg.DeleteAsync();
                await ctx.Channel.SendMessageAsync(await StringHelpers.CodeOrHasteBinAsync(JsonConvert.SerializeObject(memberIdsTonames, Formatting.Indented), "json"));
            }

            [Command("rawmessage")]
            [Description("Dumps the raw data for a message.")]
            [Aliases("rawmsg")]
            [IsBotOwner]
            public async Task DumpRawMessage(CommandContext ctx, [Description("The message whose raw data to get.")] string msgLinkOrId)
            {
                DiscordMessage message;
                if (Constants.RegexConstants.discord_link_rx.IsMatch(msgLinkOrId))
                {
                    // Assume the user provided a message link. Extract channel and message IDs to get message content.

                    // Pattern to extract channel and message IDs from URL
                    var idPattern = new Regex(@"(?:.*\/)([0-9]+)\/([0-9]+)$");

                    // Get channel ID
                    var targetChannelId = Convert.ToUInt64(idPattern.Match(msgLinkOrId).Groups[1].ToString().Replace("/", ""));

                    // Try to fetch channel
                    DiscordChannel channel;
                    try
                    {
                        channel = await ctx.Client.GetChannelAsync(targetChannelId);
                    }
                    catch
                    {
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't fetch the channel from your message link! Please try again.");
                        return;
                    }

                    // Get message ID
                    var targetMessage = Convert.ToUInt64(idPattern.Match(msgLinkOrId).Groups[2].ToString().Replace("/", ""));

                    // Try to fetch message
                    try
                    {
                        message = await channel.GetMessageAsync(targetMessage);
                    }
                    catch
                    {
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't fetch the message from your message link! Please try again.");
                        return;
                    }
                }
                else
                {
                    if (msgLinkOrId.Length < 17)
                    {
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That doesn't look right. Try again.");
                        return;
                    }

                    ulong messageId;
                    try
                    {
                        messageId = Convert.ToUInt64(msgLinkOrId);
                    }
                    catch
                    {
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That doesn't look like a valid message ID. Try again.");
                        return;
                    }

                    try
                    {
                        message = await ctx.Channel.GetMessageAsync(messageId);
                    }
                    catch
                    {
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I wasn't able to read that message! Please try again.");
                        return;
                    }
                }

                var rawMsgData = JsonConvert.SerializeObject(message, Formatting.Indented);
                await ctx.RespondAsync(await StringHelpers.CodeOrHasteBinAsync(rawMsgData, "json"));
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
                            return (false, overwrite.Id);
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

                return (true, 0);
            }

        }

    }
}
