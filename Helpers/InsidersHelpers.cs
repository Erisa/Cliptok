namespace Cliptok.Helpers
{
    internal class InsiderAnnouncementException : Exception
    {
        internal InsiderAnnouncementException(string message) : base(message)
        {
        }
    }

    internal static class InsidersHelpers
    {
        internal static string GetInsiderChannelNameFromRole(DiscordRole insiderRole)
        {
            return Regex.Match(insiderRole.Name, @"\((.+)\)").Groups[1].Value;
        }

        internal static async Task<Models.InsiderReleaseNotesJson> GetInsiderReleaseNotesAsync()
        {
            var response = await Program.httpClient.GetAsync(Constants.InsidersConstants.insider_release_notes_json);
            
            return JsonConvert.DeserializeObject<Models.InsiderDocsTableOfContents>(await response.Content.ReadAsStringAsync()).Items
                    .First(x => x.Href == "release-notes/");
        }

        internal static async Task<List<Models.InsiderReleaseNotesJson>> GetInsiderReleaseNotesForRoleAsync(DiscordRole insiderRole, List<DiscordRole> allInsiderRoles, int limit = 0)
        {
            var channelName = GetInsiderChannelNameFromRole(insiderRole);
            if (string.IsNullOrWhiteSpace(channelName))
                throw new ArgumentException("Invalid insider role provided!");

            List<Models.InsiderReleaseNotesJson> releaseNotes = [];

            var guildInsiderChannels = allInsiderRoles.Select(x => GetInsiderChannelNameFromRole(x)).ToList();

            // "channel" here is split the way msft does it, since this comes from release notes;
            // so "Beta" and "Beta (26H1)", or "Experimental" and "Experimental (Future Platforms)" are different (!!)
            var allReleaseNotes = (await GetInsiderReleaseNotesAsync()).Children;
            foreach (var channel in allReleaseNotes)
            {
                var matchingChannels = guildInsiderChannels.Where(ch => channel.TocTitle.Contains(ch));

                // Narrow down to the most specific match
                // This way we get (for example) Future Platforms builds only in Experimental (Future Platforms) and not in
                // other Experimental channels, and Experimental builds only in Experimental and not in Experimental (Future Platforms)
                var mostSpecificMatch = matchingChannels.MinBy(ch => allReleaseNotes.Count(s => s.TocTitle.Contains(ch)));

                // If that match ends up being for another channel, skip
                if (mostSpecificMatch != channelName)
                    continue;

                if (limit < 1)
                    releaseNotes.AddRange(channel.Children);
                else
                    releaseNotes.AddRange(channel.Children.Take(limit));
            }
            return releaseNotes;
        }

        // Returns build numbers from the post title and from the URL, if they differ. If they are the same, only buildNumber is returned.
        // Sometimes the build number in the URL is incorrect and doesn't match the one in the title. If they differ, you probably
        // want to consider/check both.
        internal static (string buildNumber, string urlBuildNumber) GetBuildNumberFromReleaseNotes(Models.InsiderReleaseNotesJson releaseNotes)
        {
            string buildNumberRegex = "[0-9]{5,}(?:\\.[0-9]{4,})?";

            string buildNumber;
            string urlBuildNumber;

            // For releases with two build numbers, such as 26100.9267 / 26200.9267, check the higher number.
            if (releaseNotes.TocTitle.Contains('/'))
            {
                var buildNumberCandidates = Regex.Matches(releaseNotes.TocTitle, buildNumberRegex);
                buildNumber = buildNumberCandidates.OrderByDescending(x => x.Value).First().Value;
            }
            else
            {
                buildNumber = Regex.Match(releaseNotes.TocTitle, buildNumberRegex).Value;
            }

            var urlBuildNumberCandidates = Regex.Matches(releaseNotes.Href.Replace('-', '.'), buildNumberRegex);
            if (urlBuildNumberCandidates.Count == 1)
            {
                urlBuildNumber = urlBuildNumberCandidates.First().Value;
            }
            else
            {
                urlBuildNumber = urlBuildNumberCandidates.OrderByDescending(x => x.Value).First().Value;
            }

            if (buildNumber == urlBuildNumber)
                return (buildNumber, default);
            else
                return (buildNumber, urlBuildNumber);
        }


        // Announce an Insider build.
        // This is the core of /announcebuild, but broken into a function so that it can be called to automatically announce builds.
        internal static async Task AnnounceInsiderBuildAsync(string buildNumber, string blogLink, string insiderChannel1, string insiderChannel2,
            bool createNewThread = false, DiscordChannel threadChannel = default, DiscordChannel threadChannel2 = default,
            string flavourText = "", string autothreadName = "Build {0} ({1})", string lockdownTime = "auto", bool forceReannounce = false,
            DiscordUser responsibleModerator = default)
        {
            if (Program.cfgjson.InsidersChannel == 0)
            {
                throw new InsiderAnnouncementException($"The Insiders channel is not configured! Please contact a bot maintainer if this is unexpected.");
            }
            var insidersChannel = await Program.discord.GetChannelAsync(Program.cfgjson.InsidersChannel);

            if (insiderChannel1 == insiderChannel2)
            {
                throw new InsiderAnnouncementException($"Both insider channels cannot be the same! Simply set one instead.");
            }

            if (threadChannel != default && threadChannel == threadChannel2)
            {
                throw new InsiderAnnouncementException($"Both threads cannot be the same! Simply set one instead.");
            }

            if (Program.cfgjson.InsiderRoles is null)
            {
                throw new InsiderAnnouncementException($"Insider roles are not set up in config.json! Unable to announce builds.");
            }

            if (threadChannel == default && threadChannel2 != default)
            {
                threadChannel = threadChannel2;
                threadChannel2 = default;
            }

            // Avoid duplicate announcements
            if (await Program.redis.SetContainsAsync("announcedInsiderBuilds", buildNumber) && !forceReannounce)
            {
                throw new InsiderAnnouncementException($"Build {buildNumber} has already been announced! If you are sure you want to announce it again, set `force_reannounce` to True.");
            }

            await Program.redis.SetAddAsync("announcedInsiderBuilds", buildNumber);

            if (flavourText == "")
            {
                flavourText = Program.cfgjson.Emoji.Insider;
            }

            DiscordRole insiderRole1;
            DiscordRole insiderRole2 = default;
            try
            {
                insiderRole1 = await insidersChannel.Guild.GetRoleAsync(Convert.ToUInt64(insiderChannel1));
            }
            catch (Exception ex) when (ex is FormatException or DSharpPlus.Exceptions.NotFoundException)
            {
                throw new InsiderAnnouncementException($"An invalid Insider role was provided!");
            }

            StringBuilder channelString = new();

            string insiderChannel1Pretty = GetInsiderChannelNameFromRole(insiderRole1);

            if (string.IsNullOrWhiteSpace(insiderChannel1Pretty))
            {
                throw new InsiderAnnouncementException($"The Insider roles in this server do not match the expected format! Unable to announce builds.");
            }

            channelString.Append("the ");

            channelString.Append($"**{insiderChannel1Pretty}");

            if (insiderChannel2 != default)
            {
                try
                {
                    insiderRole2 = await insidersChannel.Guild.GetRoleAsync(Convert.ToUInt64(insiderChannel2));
                }
                catch (Exception ex) when (ex is FormatException or DSharpPlus.Exceptions.NotFoundException)
                {
                    throw new InsiderAnnouncementException($"An invalid Insider role was provided!");
                }
                string insiderChannel2Pretty = GetInsiderChannelNameFromRole(insiderRole2);

                if (string.IsNullOrWhiteSpace(insiderChannel2Pretty))
                {
                    throw new InsiderAnnouncementException($"The Insider roles in this server do not match the expected format! Unable to announce builds.");
                }

                channelString.Append($" **and **{insiderChannel2Pretty}** Channels");
            }
            else
            {
                channelString.Append("** Channel");
            }

            string pingMsgBareString = $"{insiderRole1.Mention}{(insiderChannel2 != default ? $" {insiderRole2.Mention}\n" : " - ")}Hi Insiders!\n\n" +
                $"Windows 11 Build **{buildNumber}** has just been released to {channelString}! {flavourText}\n\n" +
                $"Check it out here: {blogLink}";

            string innerThreadMsgString = $"Hi Insiders!\n\n" +
                $"Windows 11 Build **{buildNumber}** has just been released to {channelString}! {flavourText}\n\n" +
                $"Check it out here: {blogLink}";

            string noPingMsgString = $"{Program.cfgjson.Emoji.Windows11} Windows 11 Build **{buildNumber}** has just been released to {channelString}! {flavourText}\n\n" +
                $"Check it out here: <{blogLink}>";

            string pingMsgString = pingMsgBareString;

            DiscordMessage messageSent;
            if (Program.cfgjson.InsiderAnnouncementChannel == 0)
            {
                if (threadChannel != default)
                {
                    pingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                    if (threadChannel2 != default)
                        pingMsgString += $" & {threadChannel2.Mention}";
                }
                else if (!createNewThread)
                {
                    threadChannel = insidersChannel.Threads.FirstOrDefault(t => t.Name.Contains(GetInsiderChannelNameFromRole(insiderRole1), StringComparison.OrdinalIgnoreCase));
                    threadChannel2 = insiderRole2 == default
                        ? default
                        : insidersChannel.Threads.FirstOrDefault(t => t.Name.Contains(GetInsiderChannelNameFromRole(insiderRole2), StringComparison.OrdinalIgnoreCase));

                    if (threadChannel == default)
                    {
                        throw new InsiderAnnouncementException($"Couldn't find an Insider thread for the {insiderRole1.Mention} channel! Please set it manually or check the thread names.");
                    }
                    if (threadChannel2 == default)
                    {
                        throw new InsiderAnnouncementException($"Couldn't find an Insider thread for the {insiderRole2.Mention} channel! Please set it manually or check the thread names.");
                    }

                    pingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                    if (threadChannel2 != default)
                        pingMsgString += $" & {threadChannel2.Mention}";
                    var msg = await threadChannel.SendMessageAsync(innerThreadMsgString);
                    await DiscordHelpers.UpdateInsiderThreadPinsAsync(threadChannel, msg);
                }
                else
                {
                    pingMsgString += "\n\nDiscuss it in the thread below:";
                }

                await insiderRole1.ModifyAsync(mentionable: true, reason: "Making role mentionable for Insider build announcement.");
                if (insiderChannel2 != default)
                    await insiderRole2.ModifyAsync(mentionable: true, reason: "Making role mentionable for Insider build announcement.");

                messageSent = await insidersChannel.SendMessageAsync(pingMsgString);

                await insiderRole1.ModifyAsync(mentionable: false, reason: "Restoring role state after Insider build announcement.");
                if (insiderChannel2 != default)
                    await insiderRole2.ModifyAsync(mentionable: false, reason: "Restoring role state after Insider build announcement.");
            }
            else
            {
                if (threadChannel != default)
                {
                    noPingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                    if (threadChannel2 == default && insiderChannel2 != default)
                    {
                        throw new InsiderAnnouncementException($"Couldn't find an Insider thread for the {insiderRole2.Mention} channel! Please set it manually or check the thread names.");
                    }
                    else if (threadChannel2 != default)
                    {
                        noPingMsgString += $" & {threadChannel2.Mention}";
                    }
                }
                else if (!createNewThread)
                {
                    threadChannel = insidersChannel.Threads.FirstOrDefault(t => t.Name.Contains(GetInsiderChannelNameFromRole(insiderRole1), StringComparison.OrdinalIgnoreCase));
                    threadChannel2 =  insiderRole2 == default
                        ? default
                        : insidersChannel.Threads.FirstOrDefault(t => t.Name.Contains(GetInsiderChannelNameFromRole(insiderRole2), StringComparison.OrdinalIgnoreCase));

                    if (threadChannel == default)
                    {
                        throw new InsiderAnnouncementException($"Couldn't find an Insider thread for the \"{GetInsiderChannelNameFromRole(insiderRole1)}\" channel! Please set it manually or check the thread names.");
                    }

                    noPingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                    if (threadChannel2 == default && insiderChannel2 != default)
                    {
                        throw new InsiderAnnouncementException($"Couldn't find an Insider thread for the \"{GetInsiderChannelNameFromRole(insiderRole2)}\" channel! Please set it manually or check the thread names.");
                    }
                    else if (threadChannel2 != default)
                    {
                        noPingMsgString += $" & {threadChannel2.Mention}";
                    }
                    var msg = await threadChannel.SendMessageAsync(innerThreadMsgString);
                    await DiscordHelpers.UpdateInsiderThreadPinsAsync(threadChannel, msg);
                    DiscordMessage msg2 = default;
                    if (threadChannel2 != default)
                    {
                        msg2 = await threadChannel2.SendMessageAsync(innerThreadMsgString);
                        await DiscordHelpers.UpdateInsiderThreadPinsAsync(threadChannel2, msg2);
                    }
                }
                else
                {
                    noPingMsgString += "\n\nDiscuss it in the thread below:";
                }

                messageSent = await insidersChannel.SendMessageAsync(noPingMsgString);
            }

            if (threadChannel == default)
            {
                string threadBrackets = GetInsiderChannelNameFromRole(insiderRole1);
                if (insiderChannel2 != default)
                    threadBrackets = $"{GetInsiderChannelNameFromRole(insiderRole1)} & {GetInsiderChannelNameFromRole(insiderRole2)}";

                string threadName = string.Format(autothreadName, buildNumber, threadBrackets);
                threadChannel = await messageSent.CreateThreadAsync(threadName, DiscordAutoArchiveDuration.Week, "Creating thread for Insider build.");

                var initialMsg = await threadChannel.SendMessageAsync($"{blogLink}");
                await initialMsg.PinAsync(reason: "Pinning Insider build in thread.");
            }

            if (Program.cfgjson.InsiderAnnouncementChannel != 0)
            {
                pingMsgString = pingMsgBareString + $"\n\nDiscuss it here: {threadChannel.Mention}";
                if (threadChannel2 != default)
                    pingMsgString += $" & {threadChannel2.Mention}";

                var announcementChannel = await Program.discord.GetChannelAsync(Program.cfgjson.InsiderAnnouncementChannel);
                await insiderRole1.ModifyAsync(mentionable: true, reason: "Making role mentionable for Insider build announcement.");
                if (insiderChannel2 != default)
                    await insiderRole2.ModifyAsync(mentionable: true, reason: "Making role mentionable for Insider build announcement.");

                var msg = await announcementChannel.SendMessageAsync(pingMsgString);

                await insiderRole1.ModifyAsync(mentionable: false, reason: "Restoring role state after Insider build announcement.");
                if (insiderChannel2 != default)
                    await insiderRole2.ModifyAsync(mentionable: false, reason: "Restoring role state after Insider build announcement.");

                if (announcementChannel.Type is DiscordChannelType.News)
                    await announcementChannel.CrosspostMessageAsync(msg);
            }

            if (lockdownTime == "auto")
            {
                if (Program.cfgjson.InsiderAnnouncementChannel == 0)
                    lockdownTime = "1h";
                else
                    lockdownTime = "0";
            }

            if (lockdownTime != "0")
            {
                TimeSpan lockDuration;
                try
                {
                    lockDuration = HumanDateParser.HumanDateParser.Parse(lockdownTime).ToUniversalTime().Subtract(DateTime.UtcNow);
                }
                catch
                {
                    lockDuration = TimeSpan.FromHours(2);
                }

                await LockdownHelpers.LockChannelAsync(user: responsibleModerator, channel: insidersChannel, duration: lockDuration);
            }
        }
    }
}
