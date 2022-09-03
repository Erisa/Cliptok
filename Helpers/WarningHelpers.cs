namespace Cliptok.Helpers
{
    public class WarningHelpers
    {
        public static async Task<DiscordEmbed> GenerateWarningsEmbedAsync(DiscordUser targetUser)
        {
            var warningsOutput = Program.db.HashGetAll(targetUser.Id.ToString()).ToDictionary(
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
                .WithTimestamp(DateTime.Now)
                .WithFooter(
                    $"User ID: {targetUser.Id}",
                    null
                )
                .WithAuthor(
                    $"Warnings for {targetUser.Username}#{targetUser.Discriminator}",
                    null,
                    await LykosAvatarMethods.UserOrMemberAvatarURL(targetUser, Program.homeGuild, "png")
                );

            if (warningsOutput.Count == 0)
                embed.WithDescription("This user has no warnings on record.")
                    .WithColor(color: DiscordColor.DarkGreen);
            else
            {
                foreach (string key in keys)
                {
                    UserWarning warning = warningsOutput[key];
                    TimeSpan span = DateTime.Now - warning.WarnTimestamp;
                    if (span.Days < 31)
                    {
                        recentCount += 1;
                    }
                    if (count == 66)
                    {
                        str += $"+ {keys.Count() - 65} more…";
                        count += 1;
                    }
                    else if (count < 66)
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

                }

                if (Program.cfgjson.RecentWarningsPeriodHours != 0)
                {
                    var hourRecentMatches = keys.Where(key =>
                    {
                        TimeSpan span = DateTime.Now - warningsOutput[key].WarnTimestamp;
                        return (span.TotalHours < Program.cfgjson.RecentWarningsPeriodHours);
                    }
                    );

                    embed.AddField($"Last {Program.cfgjson.RecentWarningsPeriodHours} hours", hourRecentMatches.Count().ToString(), true);

                    embed.AddField("Last 30 days", recentCount.ToString(), true)
                        .AddField("Total", keys.Count().ToString(), true);
                }

                embed.WithDescription(str);
            }

            return embed;
        }

        public static async Task<DiscordEmbed> FancyWarnEmbedAsync(UserWarning warning, bool detailed = false, int colour = 0xFEC13D, bool showTime = true, ulong userID = default)
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
            .WithTimestamp(DateTime.Now)
            .WithFooter(
                $"User ID: {userID}",
                null
            )
            .WithAuthor(
                $"Warning for {targetUser.Username}#{targetUser.Discriminator}",
                null,
                await LykosAvatarMethods.UserOrMemberAvatarURL(targetUser, Program.homeGuild, "png")
            )
            .AddField("Warning ID", StringHelpers.Pad(warning.WarningId), true);
            if (detailed)
            {
                embed.AddField("Responsible moderator", $"<@{warning.ModUserId}>")
                .AddField("Message link", warning.ContextLink == null ? "N/A" : $"[`Jump to warning`]({warning.ContextLink})");
            }
            if (showTime)
                embed.AddField("Time", detailed ? $"<t:{TimeHelpers.ToUnixTimestamp(warning.WarnTimestamp)}:f>" : $"<t:{TimeHelpers.ToUnixTimestamp(warning.WarnTimestamp)}:R>", true);

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
                rules = screeningForm.Fields.FirstOrDefault(field => field.Type is MembershipScreeningFieldType.Terms).Values;
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
                    var probablyRuleSplit = probablyRule.Split()[0];
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
            long warningId = Program.db.StringIncrement("totalWarnings");

            DiscordMessage? dmMessage = null;
            try
            {
                DiscordMember member = await guild.GetMemberAsync(targetUser.Id);
                dmMessage = await member.SendMessageAsync(await GenerateWarningDM(reason, channel.Guild, extraWord));
            }
            catch
            {
                // We failed to DM the user, this isn't important to note.
            }

            UserWarning warning = new()
            {
                TargetUserId = targetUser.Id,
                ModUserId = modUser.Id,
                WarnReason = reason,
                WarnTimestamp = DateTime.Now,
                WarningId = warningId,
                ContextLink = DiscordHelpers.MessageLink(contextMessage),
                ContextMessageReference = new()
                {
                    MessageId = contextMessage.Id,
                    ChannelId = contextMessage.ChannelId
                }
            };

            if (dmMessage is not null)
                warning.DmMessageReference = new()
                {
                    MessageId = dmMessage.Id,
                    ChannelId = dmMessage.ChannelId
                };

            Program.db.HashSet(targetUser.Id.ToString(), warning.WarningId, JsonConvert.SerializeObject(warning));

            LogChannelHelper.LogMessageAsync("mod",
                new DiscordMessageBuilder()
                    .WithContent($"{Program.cfgjson.Emoji.Warning} New warning for {targetUser.Mention}!")
                    .WithEmbed(await FancyWarnEmbedAsync(warning, true, 0xFEC13D, false, targetUser.Id))
                    .WithAllowedMentions(Mentions.None)
            );

            // automute handling
            var warningsOutput = Program.db.HashGetAll(targetUser.Id.ToString()).ToDictionary(
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

            return warning;
        }

        public static async Task<bool> EditWarning(DiscordUser targetUser, long warnId, DiscordUser modUser, string reason)
        {

            if (Program.db.HashExists(targetUser.Id.ToString(), warnId))
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

                await Program.db.HashSetAsync(targetUser.Id.ToString(), warning.WarningId, JsonConvert.SerializeObject(warning));
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

            if (Program.db.HashExists(userID.ToString(), warning.WarningId))
            {
                var contextMessage = await DiscordHelpers.GetMessageFromReferenceAsync(warning.ContextMessageReference);
                if (contextMessage is not null)
                    await contextMessage.DeleteAsync();

                var dmMessage = await DiscordHelpers.GetMessageFromReferenceAsync(warning.DmMessageReference);
                if (dmMessage is not null)
                {
                    var guild = await Program.discord.GetGuildAsync(Program.cfgjson.ServerID);
                    await dmMessage.ModifyAsync($"{Program.cfgjson.Emoji.Success} You were warned in **{guild.Name}**, but the warning was revoked by a Moderator.");
                }

                Program.db.HashDelete(userID.ToString(), warning.WarningId);
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
                return JsonConvert.DeserializeObject<UserWarning>(Program.db.HashGet(targetUserId.ToString(), warnId));
            }
            catch (ArgumentNullException)
            {
                return null;
            }
        }

    }
}
