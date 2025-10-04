namespace Cliptok.Helpers
{
    public class WarningHelpers
    {
        public static UserWarning mostRecentWarning;

        public static async Task<DiscordEmbed> GenerateWarningsEmbedAsync(DiscordUser targetUser)
        {
            var warningsOutput = (await Program.redis.HashGetAllAsync(targetUser.Id.ToString()))
                .Where(x => JsonConvert.DeserializeObject<UserWarning>(x.Value).Type == WarningType.Warning).ToDictionary(
                x => x.Name.ToString(),
                x => JsonConvert.DeserializeObject<UserWarning>(x.Value)
            );

            var keys = warningsOutput.Keys.OrderByDescending(warn => Convert.ToInt64(warn));
            string str = "";
            int count = 1;
            int recentCount = 0;

            var embed = new DiscordEmbedBuilder()
                .WithDescription(str)
                .WithColor(new DiscordColor(0xFEC13D))
                .WithTimestamp(DateTime.UtcNow)
                .WithFooter(
                    $"User ID: {targetUser.Id}",
                    null
                )
                .WithAuthor(
                    $"Warnings for {DiscordHelpers.UniqueUsername(targetUser)}",
                    null,
                    await LykosAvatarMethods.UserOrMemberAvatarURL(targetUser, Program.homeGuild, "png")
                );

            if (warningsOutput.Count == 0)
                embed.WithDescription("This user has no warnings on record.")
                    .WithColor(color: DiscordColor.DarkGreen);
            else
            {
                TimeSpan timeToCheck = TimeSpan.FromDays(Program.cfgjson.WarningDaysThreshold);
                var numHiddenWarnings = 0;
                foreach (string key in keys)
                {
                    UserWarning warning = warningsOutput[key];
                    if (warning.IsPardoned)
                        continue;

                    TimeSpan span = DateTime.UtcNow - warning.WarnTimestamp;
                    if (span <= timeToCheck)
                    {
                        recentCount += 1;
                    }
                    
                    if (4096 - str.Length - 60 - 15 > 0) // each warning entry is max 60 chars; give 15 chars of room for "+ x more…" if needed
                    {
                        var reason = warning.WarnReason;
                        if (string.IsNullOrWhiteSpace(reason))
                        {
                            reason = "No reason provided.";
                        }
                        reason = reason.Replace("`", "\\`").Replace("*", "\\*");

                        if (reason.Length > 29)
                        {
                            reason = StringHelpers.Truncate(reason, 29) + "…";
                        }
                        str += $"`{StringHelpers.Pad(warning.WarningId)}` **{reason}** • <t:{TimeHelpers.ToUnixTimestamp(warning.WarnTimestamp)}:R>\n";
                        count += 1;
                    }
                    else
                    {
                        numHiddenWarnings++;
                    }

                }

                var pardonedWarningList = warningsOutput.Values.Where(x => x.IsPardoned)
                    .GroupBy(x => x.WarnReason)
                    .Select(x => new { Reason = x.Key, Count = x.Count() })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Reason);
                
                foreach (var pardonedWarning in pardonedWarningList)
                {
                    if (4096 - str.Length - 45 - 15 > 0) // each pardoned warning entry is max 45 chars; give 15 chars of room for "+ x more…" if needed
                    {
                        var reason = pardonedWarning.Reason;
                        if (reason.Length > 25)
                        {
                            reason = StringHelpers.Truncate(reason, 25) + "…";
                        }
                        str += $"(Pardoned) {(pardonedWarning.Count > 1 ? pardonedWarning.Count > 99 ? "many " : $"{pardonedWarning.Count}x " : "")}**{reason}**\n";
                    }
                    else
                    {
                        numHiddenWarnings += pardonedWarning.Count;
                    }
                }
                
                if (numHiddenWarnings > 0)
                    str += $"+ {numHiddenWarnings} more…";

                if (Program.cfgjson.RecentWarningsPeriodHours != 0)
                {
                    var hourRecentMatches = keys.Where(key =>
                    {
                        TimeSpan span = DateTime.UtcNow - warningsOutput[key].WarnTimestamp;
                        return (span.TotalHours < Program.cfgjson.RecentWarningsPeriodHours && !warningsOutput[key].IsPardoned);
                    }
                    );

                    embed.AddField($"Last {Program.cfgjson.RecentWarningsPeriodHours} hours", hourRecentMatches.Count().ToString(), true);

                    embed.AddField($"Last {Program.cfgjson.WarningDaysThreshold} days", recentCount.ToString(), true)
                        .AddField("Total", keys.Count().ToString(), true);
                }

                embed.WithDescription(str);
            }

            return embed;
        }

        public static async Task<DiscordEmbed> FancyWarnEmbedAsync(UserWarning warning, bool detailed = false, int colour = 0xFEC13D, bool showTime = true, ulong userID = default, bool showPardonedInline = false)
        {
            if (userID == default)
                userID = warning.TargetUserId;

            string reason = warning.WarnReason;
            if (string.IsNullOrWhiteSpace(reason))
                reason = "No reason provided.";

            DiscordUser targetUser = await Program.discord.GetUserAsync(userID);
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
            .WithDescription($"**Reason**\n{reason}")
            .WithColor(new DiscordColor(colour))
            .WithTimestamp(DateTime.UtcNow)
            .WithFooter(
                $"User ID: {userID}",
                null
            )
            .WithAuthor(
                $"Warning for {DiscordHelpers.UniqueUsername(targetUser)}",
                null,
                await LykosAvatarMethods.UserOrMemberAvatarURL(targetUser, Program.homeGuild, "png")
            )
            .AddField("Warning ID", StringHelpers.Pad(warning.WarningId), true);
            if (detailed)
            {
                embed.AddField("Responsible moderator", $"<@{warning.ModUserId}>")
                .AddField("Message link", warning.ContextLink is null ? "N/A" : $"{warning.ContextLink}");
            }
            if (showTime)
                embed.AddField("Time", detailed ? $"<t:{TimeHelpers.ToUnixTimestamp(warning.WarnTimestamp)}:f>" : $"<t:{TimeHelpers.ToUnixTimestamp(warning.WarnTimestamp)}:R>", true);
            
            embed.AddField("Pardoned", warning.IsPardoned ? "Yes" : "No", showPardonedInline);

            return embed;
        }

        public static async Task<DiscordMessageBuilder> GenerateWarningDM(string reason, DiscordGuild guild, string extraWord = " ")
        {
            DiscordGuildMembershipScreening screeningForm = default;
            IReadOnlyList<string> rules = default;
            var embeds = new List<DiscordEmbed>();

            try
            {
                screeningForm = await guild.GetMembershipScreeningFormAsync();
                rules = screeningForm.Fields.FirstOrDefault(field => field.Type is DiscordMembershipScreeningFieldType.Terms).Values;
            }
            catch
            {
                // that's fine, community must be disabled
            }

            var msg = new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Warning} You were{extraWord}warned in **{guild.Name}**, reason: **{reason}**");

            if (screeningForm != default && rules != default)
            {
                var rulesBaseStr = reason.ToLower().Replace("rules ", "").Replace("rule ", "");
                var rulesBrokenStr = rulesBaseStr.Split("/");
                if (rulesBrokenStr.Length == 1)
                    rulesBrokenStr = rulesBaseStr.Split(",");

                List<int> rulesBroken = new();
                //int ruleInt;
                foreach (var probablyRule in rulesBrokenStr)
                {
                    var probablyRuleSplit = probablyRule.Replace(" ", "");
                    if (int.TryParse(probablyRuleSplit, out int ruleInt) && ruleInt >= 0 && ruleInt <= rules.Count)
                    {
                        rulesBroken.Add(ruleInt);
                    }
                }

                rulesBroken.Sort();
                rulesBroken = rulesBroken.Distinct().ToList();

                foreach (var ruleBroken in rulesBroken)
                {
                    if (embeds.Count == 10)
                        break;

                    string ruleText;

                    if (ruleBroken == 0)
                        ruleText = "Under no circumstances should you ever ██████████████. "
                            + "In the event you're caught ███████ with the █████████ on your ███████ then you will immediately be ████████████ and your ██████ forcefully removed with ██████████.";
                    else
                        ruleText = rules[ruleBroken - 1];

                    embeds.Add(new DiscordEmbedBuilder().AddField($"Rule {ruleBroken}", ruleText).WithColor(0xFEC13D));
                }
                msg.AddEmbeds(embeds.AsEnumerable());
            }

            return msg;
        }

        public static async Task<UserWarning> GiveWarningAsync(DiscordUser targetUser, DiscordUser modUser, string reason, DiscordMessage contextMessage, DiscordChannel channel, string extraWord = " ")
        {
            DiscordGuild guild = channel.Guild;
            long warningId = Program.redis.StringIncrement("totalWarnings");

            DiscordMessage? dmMessage = null;
            try
            {
                DiscordMember member = await guild.GetMemberAsync(targetUser.Id);
                dmMessage = await member.SendMessageAsync(await GenerateWarningDM(reason, channel.Guild, extraWord));
            }
            catch (Exception e)
            {
                // We failed to DM the user.
                // Lets log this if it isn't a known cause.
                if (e is DSharpPlus.Exceptions.NotFoundException)
                {
                    Program.discord.Logger.LogWarning(e, "Failed to send warning DM to user because they are not in the server: {user}", targetUser.Id);
                }
                if (e is not DSharpPlus.Exceptions.UnauthorizedException)
                {
                    Program.discord.Logger.LogWarning(e, "Failed to send warning DM to user: {user}", targetUser.Id);
                }
            }

            UserWarning warning = new()
            {
                TargetUserId = targetUser.Id,
                ModUserId = modUser.Id,
                WarnReason = reason,
                WarnTimestamp = DateTime.UtcNow,
                WarningId = warningId,
                ContextLink = DiscordHelpers.MessageLink(contextMessage),
                ContextMessageReference = new()
                {
                    MessageId = contextMessage.Id,
                    ChannelId = contextMessage.ChannelId
                },
                Type = WarningType.Warning,
                IsPardoned = false
            };

            if (dmMessage is not null)
                warning.DmMessageReference = new()
                {
                    MessageId = dmMessage.Id,
                    ChannelId = dmMessage.ChannelId
                };

            Program.redis.HashSet(targetUser.Id.ToString(), warning.WarningId, JsonConvert.SerializeObject(warning));

            // Now that the warning is in DM, prevent future collisions by caching it.
            if (!modUser.IsBot)
            {
                mostRecentWarning = warning;
                // If warning is automatic (if responsible moderator is a bot), add to list so the context message can be more-easily deleted later
            }
            else
            {
                Program.redis.HashSet("automaticWarnings", warningId, JsonConvert.SerializeObject(warning));
            }

            var logMsg = await LogChannelHelper.LogMessageAsync("mod",
                new DiscordMessageBuilder()
                    .WithContent($"{Program.cfgjson.Emoji.Warning} New warning for {targetUser.Mention}!")
                    .AddEmbed(await FancyWarnEmbedAsync(warning, true, 0xFEC13D, false, targetUser.Id))
                    .WithAllowedMentions(Mentions.None)
            );
            try
            {
                if (Program.cfgjson.ReactionEmoji is not null)
                {
                    var emoji = await Program.discord.GetApplicationEmojiAsync(Program.cfgjson.ReactionEmoji.Delete);
                    await logMsg.CreateReactionAsync(emoji);
                    Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(Program.cfgjson.WarningLogReactionTimeMinutes));
                        await logMsg.DeleteOwnReactionAsync(emoji);
                    });
                }
            }
            catch
            {
                // Don't really care if this fails
            }

            // automute handling
            var warningsOutput = (await Program.redis.HashGetAllAsync(targetUser.Id.ToString())).ToDictionary(
                x => x.Name.ToString(),
                x => JsonConvert.DeserializeObject<UserWarning>(x.Value)
            );

            var autoMuteResult = MuteHelpers.GetHoursToMuteFor(warningDictionary: warningsOutput, timeToCheck: TimeSpan.FromDays(Program.cfgjson.WarningDaysThreshold), autoMuteThresholds: Program.cfgjson.AutoMuteThresholds);

            var acceptedThreshold = Program.cfgjson.WarningDaysThreshold;
            int toMuteHours = autoMuteResult.MuteHours;
            int warnsSinceThreshold = autoMuteResult.WarnsSinceThreshold;
            string thresholdSpan = "days";

            if (toMuteHours != -1 && Program.cfgjson.RecentWarningsPeriodHours != 0)
            {
                var (MuteHours, WarnsSinceThreshold) = MuteHelpers.GetHoursToMuteFor(warningDictionary: warningsOutput, timeToCheck: TimeSpan.FromHours(Program.cfgjson.RecentWarningsPeriodHours), autoMuteThresholds: Program.cfgjson.RecentWarningsAutoMuteThresholds);
                if (MuteHours == -1 || MuteHours >= toMuteHours)
                {
                    toMuteHours = MuteHours;
                    warnsSinceThreshold = WarnsSinceThreshold;
                    thresholdSpan = "hours";
                    acceptedThreshold = Program.cfgjson.RecentWarningsPeriodHours;
                }
            }

            if (toMuteHours > 0)
            {
                await MuteHelpers.MuteUserAsync(targetUser, $"Automatic mute after {warnsSinceThreshold} warnings in the past {acceptedThreshold} {thresholdSpan}.", modUser.Id, guild, channel, TimeSpan.FromHours(toMuteHours));
            }
            else if (toMuteHours <= -1)
            {
                await MuteHelpers.MuteUserAsync(targetUser, $"Automatic permanent mute after {warnsSinceThreshold} warnings in the past {acceptedThreshold} {thresholdSpan}.", modUser.Id, guild, channel);
            }

            // If warning was not automatic (not issued by a bot) and target user has notes to be shown on warn, alert the responsible moderator

            if (!modUser.IsBot)
            {
                // Get notes
                var notes = (await Program.redis.HashGetAllAsync(targetUser.Id.ToString()))
                    .Where(x => JsonConvert.DeserializeObject<UserNote>(x.Value).Type == WarningType.Note).ToDictionary(
                        x => x.Name.ToString(),
                        x => JsonConvert.DeserializeObject<UserNote>(x.Value)
                    );

                // Get notes set to notify on warn
                var notesToNotifyFor = notes.Where(x => x.Value.ShowOnWarn).ToDictionary(x => x.Key, x => x.Value);

                // Get relevant notes ('show all mods' is true, or mod is responsible for note & warning)
                notesToNotifyFor = notesToNotifyFor.Where(x => x.Value.ShowAllMods || x.Value.ModUserId == modUser.Id).ToDictionary(x => x.Key, x => x.Value);

                // Alert moderator if there are relevant notes
                if (notesToNotifyFor.Count != 0)
                {
                    var msg = new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Muted} {modUser.Mention}, {targetUser.Mention} has notes set to show when they are issued a warning!").AddEmbed(await UserNoteHelpers.GenerateUserNotesEmbedAsync(targetUser, true, notesToNotifyFor)).WithAllowedMentions(Mentions.All);

                    // For any notes set to show once, show the full note content in its own embed because it will not be able to be fetched manually
                    foreach (var note in notesToNotifyFor)
                        if (msg.Embeds.Count < 10) // Limit to 10 embeds; this probably won't be an issue because we probably won't have that many 'show once' notes
                            if (note.Value.ShowOnce)
                                msg.AddEmbed(await UserNoteHelpers.GenerateUserNoteSimpleEmbedAsync(note.Value, targetUser));

                    await LogChannelHelper.LogMessageAsync("investigations", msg);
                }

                // If any notes were shown & set to show only once, delete them now
                foreach (var note in notesToNotifyFor.Where(note => note.Value.ShowOnce))
                {
                    // Delete note
                    await Program.redis.HashDeleteAsync(targetUser.Id.ToString(), note.Key);

                    // Log deletion to mod-logs channel
                    var embed = new DiscordEmbedBuilder(await UserNoteHelpers.GenerateUserNoteDetailEmbedAsync(note.Value, targetUser)).WithColor(0xf03916);
                    await LogChannelHelper.LogMessageAsync("mod", $"{Program.cfgjson.Emoji.Deleted} Note `{note.Value.NoteId}` was automatically deleted after a warning (belonging to {targetUser.Mention})", embed);
                }
            }

            return warning;
        }

        public static async Task<bool> EditWarning(DiscordUser targetUser, long warnId, DiscordUser modUser, string reason)
        {

            if (Program.redis.HashExists(targetUser.Id.ToString(), warnId))
            {
                UserWarning warning = GetWarning(targetUser.Id, warnId);

                warning.ModUserId = modUser.Id;
                warning.WarnReason = reason;

                var contextMessage = await DiscordHelpers.GetMessageFromReferenceAsync(warning.ContextMessageReference);
                if (contextMessage is not null)
                {
                    await contextMessage.ModifyAsync(StringHelpers.WarningContextString(targetUser, reason, false));
                }

                var dmMessage = await DiscordHelpers.GetMessageFromReferenceAsync(warning.DmMessageReference);
                if (dmMessage is not null)
                {
                    var guild = await Program.discord.GetGuildAsync(Program.cfgjson.ServerID);
                    await dmMessage.ModifyAsync(await GenerateWarningDM(reason, guild));
                }

                await Program.redis.HashSetAsync(targetUser.Id.ToString(), warning.WarningId, JsonConvert.SerializeObject(warning));
                return true;
            }
            else
            {
                return false;
            }
        }

        public static async Task<bool> DelWarningAsync(UserWarning warning, ulong userID = default)
        {
            if (userID == default)
                userID = warning.TargetUserId;

            if (Program.redis.HashExists("automaticWarnings", warning.WarningId))
                await Program.redis.HashDeleteAsync("automaticWarnings", warning.WarningId);

            if (Program.redis.HashExists(userID.ToString(), warning.WarningId))
            {
                var contextMessage = await DiscordHelpers.GetMessageFromReferenceAsync(warning.ContextMessageReference);
                if (contextMessage is not null)
                    await contextMessage.DeleteAsync();

                var dmMessage = await DiscordHelpers.GetMessageFromReferenceAsync(warning.DmMessageReference);
                if (dmMessage is not null)
                {
                    var guild = await Program.discord.GetGuildAsync(Program.cfgjson.ServerID);
                    await dmMessage.ModifyAsync(new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} You were warned in **{guild.Name}**, but the warning was revoked by a Moderator."), suppressEmbeds: true);
                }

                Program.redis.HashDelete(userID.ToString(), warning.WarningId);
                return true;
            }
            else
            {
                return false;
            }
        }

        public static UserWarning GetWarning(ulong targetUserId, long warnId = default)
        {
            try
            {
                return JsonConvert.DeserializeObject<UserWarning>(Program.redis.HashGet(targetUserId.ToString(), warnId));
            }
            catch (ArgumentNullException)
            {
                return null;
            }
        }

        public static async Task<DiscordMessage> SendPublicWarningMessageAndDeleteInfringingMessageAsync(DiscordMessage infringingMessage, string warningMessageContent, bool wasAutoModBlock = false, int minMessages = 0)
        {
            return await SendPublicWarningMessageAndDeleteInfringingMessageAsync(new MockDiscordMessage(infringingMessage), warningMessageContent, wasAutoModBlock, minMessages);
        }

        public static async Task<DiscordMessage> SendPublicWarningMessageAndDeleteInfringingMessageAsync(MockDiscordMessage infringingMessage, string warningMessageContent, bool wasAutoModBlock = false, int minMessages = 0)
        {
            // If this is a `GuildForum` channel, delete the thread if it is empty; if not empty, just delete the infringing message.
            // Then, based on whether the thread was deleted, send the warning message into the thread or into the configured fallback channel.
            // If this was an AutoMod block, don't delete anything.
            // Return the sent warning message for logging.

            bool wasThreadDeleted = false;
            if (!wasAutoModBlock)
                wasThreadDeleted = await DiscordHelpers.ThreadChannelAwareDeleteMessageAsync(infringingMessage, minMessages);

            return await ThreadAwareSendPublicWarningMessage(warningMessageContent, wasThreadDeleted, infringingMessage.Channel);
        }

        public static async Task<DiscordMessage> ThreadAwareSendPublicWarningMessage(string warningMessageContent, bool wasThreadDeleted, DiscordChannel targetChannel)
        {
            if (wasThreadDeleted || targetChannel.Id == Program.cfgjson.SupportForumId)
            {
                if (Program.cfgjson.ForumChannelAutoWarnFallbackChannel == 0)
                    Program.discord.Logger.LogWarning("A warning in forum channel {channelId} was attempted, but may fail due to the fallback channel not being set. Please set 'forumChannelAutoWarnFallbackChannel' in config.json to avoid this.", targetChannel.Id);
                else
                    targetChannel = Program.ForumChannelAutoWarnFallbackChannel;
            }

            var warningMessage = await targetChannel.SendMessageAsync(warningMessageContent);
            return warningMessage;
        }
    }
}
