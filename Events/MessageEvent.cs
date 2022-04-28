using static Cliptok.Constants.RegexConstants;

namespace Cliptok.Events
{
    class MessageEvent
    {
        public static Dictionary<string, string[]> wordLists = new();
        public static Dictionary<ulong, DateTime> supportRatelimit = new();

        public static List<string> allowedInviteCodes = new();
        public static List<string> disallowedInviteCodes = new();

        static public readonly HttpClient httpClient = new();

        public static async Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
        {
            MessageHandlerAsync(client, e.Message, e.Channel);
        }

        public static async Task MessageUpdated(DiscordClient client, MessageUpdateEventArgs e)
        {
            MessageHandlerAsync(client, e.Message, e.Channel, true);
        }

        static async Task DeleteAndWarnAsync(DiscordMessage message, string reason, DiscordClient client)
        {
            _ = message.DeleteAsync();
            try
            {
                _ = InvestigationsHelpers.SendInfringingMessaageAsync(Program.logChannel, message, reason, null);
            }
            catch
            {
                // still warn anyway
            }
            DiscordMessage msg = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
            var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: DiscordHelpers.MessageLink(msg), message.Channel, " automatically ");
            await InvestigationsHelpers.SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);
        }
        public static async Task MessageHandlerAsync(DiscordClient client, DiscordMessage message, DiscordChannel channel, bool isAnEdit = false)
        {
            try
            {
                if (message.Timestamp.Year < (DateTime.Now.Year - 2))
                    return;

                if (message.Author == null || message.Author.Id == client.CurrentUser.Id)
                    return;

                if (!isAnEdit && channel.IsPrivate && Program.cfgjson.DmLogChannelId != 0)
                {
                    DirectMessageEvent.DirectMessageEventHandler(message);
                    return;
                }

                if (!isAnEdit && message.Author.Id == Program.cfgjson.ModmailUserId && message.Content == "@here" && message.Embeds[0].Footer.Text.Contains("User ID:"))
                {
                    Program.discord.Logger.LogInformation(Program.CliptokEventID, $"Processing modmail message {message.Id} in {message.Channel} with {isAnEdit}");
                    var idString = modmaiL_rx.Match(message.Embeds[0].Footer.Text).Groups[1].Captures[0].Value;
                    DiscordMember modmailMember = default;
                    try
                    {
                        modmailMember = await message.Channel.Guild.GetMemberAsync(Convert.ToUInt64(idString));
                    }
                    catch (DSharpPlus.Exceptions.NotFoundException)
                    {
                        return;
                    }

                    DiscordRole muted = message.Channel.Guild.GetRole(Program.cfgjson.MutedRole);
                    if (modmailMember.Roles.Contains(muted))
                    {
                        await channel.SendMessageAsync(null, WarningHelpers.GenerateWarningsEmbed(modmailMember));
                    }
                }

                // handle #giveaways
                if (!isAnEdit && message.Author.Id == Program.cfgjson.GiveawayBot && message.Channel.Id == Program.cfgjson.GiveawaysChannel && message.Content == Program.cfgjson.GiveawayTriggerMessage)
                {
                    string giveawayTitle = message.Embeds[0].Author.Name;

                    if (giveawayTitle.Length > 100)
                    {
                        giveawayTitle = StringHelpers.Truncate(giveawayTitle, 100, false);
                    }

                    await message.CreateThreadAsync(giveawayTitle, AutoArchiveDuration.Day, "Automatically creating giveaway thread.");
                }

                // Skip DMs, external guilds, and messages from bots, beyond this point.
                if (message.Channel.IsPrivate || message.Channel.Guild.Id != Program.cfgjson.ServerID || message.Author.IsBot)
                    return;

                DiscordMember member;
                try
                {
                    member = await message.Channel.Guild.GetMemberAsync(message.Author.Id);
                }
                catch (DSharpPlus.Exceptions.NotFoundException)
                {
                    member = default;
                }

                if (member == default)
                    return;

                // Skip messages from moderators beyond this point.
                if (GetPermLevel(member) < ServerPermLevel.TrialModerator)
                {
                    if (message.MentionedUsers.Count > Program.cfgjson.MassMentionBanThreshold)
                    {
                        _ = message.DeleteAsync();
                        await channel.Guild.BanMemberAsync(message.Author.Id, 7, $"Mentioned more than {Program.cfgjson.MassMentionBanThreshold} users in one message.");
                    }

                    bool match = false;

                    // Matching word list
                    foreach (var listItem in Program.cfgjson.WordListList)
                    {
                        if (listItem.ExcludedChannels.Contains(message.Channel.Id) || listItem.Passive)
                        {
                            continue;
                        }
                        else if (Checks.ListChecks.CheckForNaughtyWords(message.Content.ToLower(), listItem))
                        {
                            string reason = listItem.Reason;
                            try
                            {
                                _ = message.DeleteAsync();
                                await InvestigationsHelpers.SendInfringingMessaageAsync(Program.logChannel, message, reason, null);
                            }
                            catch
                            {
                                // still warn anyway
                            }

                            if (listItem.Name == "autoban.txt" && GetPermLevel(member) < ServerPermLevel.Tier4)
                            {
                                _ = message.DeleteAsync();
                                await Bans.BanFromServerAsync(message.Author.Id, reason, client.CurrentUser.Id, message.Channel.Guild, 0, message.Channel, default, true);
                                return;
                            }

                            //var tmp = message.Channel.Type;

                            match = true;

                            DiscordMessage msg = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                            var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: DiscordHelpers.MessageLink(msg), message.Channel, " automatically ");
                            await InvestigationsHelpers.SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);
                            return;
                        }
                        if (match)
                            return;
                    }

                    if (match)
                        return;

                    // Unapproved invites
                    if (GetPermLevel(member) < (ServerPermLevel)Program.cfgjson.InviteTierRequirement)
                    {

                        string checkedMessage = message.Content.Replace('\\', '/');

                        if (checkedMessage.Contains("dsc.gg/") ||
                            checkedMessage.Contains("invite.gg/")
                           )
                        {
                            string reason = "Sent an unapproved invite";
                            _ = message.DeleteAsync();
                            try
                            {
                                _ = InvestigationsHelpers.SendInfringingMessaageAsync(Program.logChannel, message, reason, null);
                            }
                            catch
                            {
                                // still warn anyway
                            }

                            DiscordMessage msg = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                            var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: DiscordHelpers.MessageLink(msg), message.Channel, " automatically ");
                            await InvestigationsHelpers.SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);
                            match = true;
                            return;
                        }

                        var matches = invite_rx.Matches(checkedMessage);

                        if (matches.Count > 3)
                        {
                            string reason = "Sent too many invites";
                            await DeleteAndWarnAsync(message, reason, client);
                            match = true;
                            return;
                        }

                        foreach (Match currentMatch in matches)
                        {
                            string code = currentMatch.Groups[1].Value;

                            if (allowedInviteCodes.Contains(code) || Program.cfgjson.InviteExclusion.Contains(code))
                            {
                                continue;
                            }
                            else if (disallowedInviteCodes.Contains(code))
                            {
                                string reason = "Sent an unapproved invite";
                                await DeleteAndWarnAsync(message, reason, client);
                                match = true;
                                return;
                            }

                            DiscordInvite invite;
                            try
                            {
                                invite = await client.GetInviteByCodeAsync(code);
                            }
                            catch (DSharpPlus.Exceptions.NotFoundException)
                            {
                                allowedInviteCodes.Add(code);
                                continue;
                            }

                            if (invite.Channel.Type == ChannelType.Group || (!Program.cfgjson.InviteExclusion.Contains(code) && !Program.cfgjson.InviteIDExclusion.Contains(invite.Guild.Id)))
                            {
                                disallowedInviteCodes.Add(code);
                                string reason = "Sent an unapproved invite";
                                await DeleteAndWarnAsync(message, reason, client);
                                match = true;
                                return;
                            }
                        }
                    }

                    if (match)
                        return;

                    // Mass emoji
                    if (!Program.cfgjson.UnrestrictedEmojiChannels.Contains(message.ChannelId) && message.Content.Length >= Program.cfgjson.MassEmojiThreshold)
                    {
                        char[] tempArray = message.Content.Replace("🏻", "").Replace("🏼", "").Replace("🏽", "").Replace("🏾", "").Replace("🏿", "").ToCharArray();
                        int pos = 0;
                        foreach (char c in tempArray)
                        {

                            if (c == '™' || c == '®' || c == '©')
                            {
                                tempArray[pos] = ' ';
                            }
                            if (c == '\u200d')
                            {
                                tempArray[pos] = ' ';
                                tempArray[pos + 1] = ' ';
                            }
                            ++pos;
                        }
                        string input = new(tempArray);

                        var matches = emoji_rx.Matches(input);
                        if (matches.Count > Program.cfgjson.MassEmojiThreshold)
                        {
                            string reason = "Mass emoji";
                            _ = message.DeleteAsync();

                            if (GetPermLevel(member) == ServerPermLevel.Nothing && !Program.db.HashExists("emojiPardoned", message.Author.Id.ToString()))
                            {
                                await Program.db.HashSetAsync("emojiPardoned", member.Id.ToString(), false);
                                DiscordMessage msgOut;
                                if (Program.cfgjson.UnrestrictedEmojiChannels.Count > 0)
                                    msgOut = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Information} {message.Author.Mention}, if you want to play around with lots of emoji, please use <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.");
                                else
                                    msgOut = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Information} {message.Author.Mention} Your message was automatically deleted for mass emoji.");

                                await InvestigationsHelpers.SendInfringingMessaageAsync(Program.badMsgLog, message, reason, DiscordHelpers.MessageLink(msgOut));
                                return;
                            }

                            string output = $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**";
                            if (Program.cfgjson.UnrestrictedEmojiChannels.Count > 0 && (!Program.db.HashExists("emojiPardoned", message.Author.Id.ToString()) || Program.db.HashGet("emojiPardoned", message.Author.Id.ToString()) == false))
                            {
                                output += $"\nIf you want to play around with lots of emoji, please use <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.";
                                await Program.db.HashSetAsync("emojiPardoned", member.Id.ToString(), true);
                            }

                            DiscordMessage msg = await message.Channel.SendMessageAsync(output);
                            var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: DiscordHelpers.MessageLink(msg), message.Channel, " automatically ");
                            await InvestigationsHelpers.SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);
                            return;
                        }

                        if (message.Channel.Id == Program.cfgjson.TechSupportChannel && message.Content.Contains($"<@&{Program.cfgjson.CommunityTechSupportRoleID}>"))
                        {
                            if (supportRatelimit.ContainsKey(message.Author.Id))
                            {
                                if (supportRatelimit[message.Author.Id] > DateTime.Now)
                                    return;
                                else
                                    supportRatelimit.Remove(message.Author.Id);
                            }

                            supportRatelimit.Add(message.Author.Id, DateTime.Now.Add(TimeSpan.FromMinutes(Program.cfgjson.SupportRatelimitMinutes)));

                            DiscordChannel supportLogChannel = await client.GetChannelAsync(Program.cfgjson.SupportLogChannel);
                            var embed = new DiscordEmbedBuilder()
                                .WithTimestamp(DateTime.Now)
                                .WithAuthor(message.Author.Username + '#' + message.Author.Discriminator, null, $"https://cdn.discordapp.com/avatars/{message.Author.Id}/{message.Author.AvatarHash}.png?size=128");

                            var lastMsgs = await message.Channel.GetMessagesBeforeAsync(message.Id, 50);
                            var msgMatch = lastMsgs.FirstOrDefault(m => m.Author.Id == message.Author.Id);

                            if (msgMatch != null)
                            {
                                var matchContent = StringHelpers.Truncate(string.IsNullOrWhiteSpace(msgMatch.Content) ? "`[No content]`" : msgMatch.Content, 1020, true);
                                embed.AddField("Previous message", matchContent);
                                if (msgMatch.Attachments.Count != 0)
                                {
                                    embed.WithImageUrl(msgMatch.Attachments[0].Url);
                                }
                            }

                            var messageContent = StringHelpers.Truncate(string.IsNullOrWhiteSpace(message.Content) ? "`[No content]`" : message.Content, 1020, true);
                            embed.AddField("Current message", messageContent);
                            if (message.Attachments.Count != 0)
                            {
                                if (embed.ImageUrl == null)
                                    embed.WithImageUrl(message.Attachments[0].Url);
                                else
                                    embed.ImageUrl = message.Attachments[0].Url;
                            }

                            embed.AddField("Message Link", $"[`Jump to message`](https://discord.com/channels/{message.Channel.Guild.Id}/{message.Channel.Id}/{message.Id})");
                            var logOut = await supportLogChannel.SendMessageAsync(null, embed);
                            _ = logOut.CreateReactionAsync(DiscordEmoji.FromName(client, ":CliptokAcknowledge:", true));
                        }
                    }

                    // phishing API
                    var urlMatches = url_rx.Matches(message.Content);
                    if (urlMatches.Count > 0 && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") != null && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") != "useyourimagination")
                    {
                        var (phishingMatch, httpStatus, responseText, phishingResponse) = await APIs.PhishingAPI.PhishingAPICheckAsync(message.Content);

                        if (httpStatus == HttpStatusCode.OK)
                        {
                            if (phishingMatch)
                            {
                                _ = message.DeleteAsync();
                                string reason = "Sending phishing URL(s)";
                                DiscordMessage msg = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                                var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: DiscordHelpers.MessageLink(msg), message.Channel, " automatically ");

                                string responseToSend = $"```json\n{responseText}\n```";
                                if (responseToSend.Length > 1940)
                                {
                                    try
                                    {
                                        HasteBinResult hasteURL = await Program.hasteUploader.Post(responseText);
                                        if (hasteURL.IsSuccess)
                                            responseToSend = hasteURL.FullUrl + ".json";
                                        else
                                            responseToSend = "Response was too big and Hastebin failed, sorry.";
                                    }
                                    catch
                                    {
                                        responseToSend = "Response was too big and Hastebin failed, sorry.";
                                    }
                                }

                                (string name, string value, bool inline) extraField = new("API Response", responseToSend, false);
                                await InvestigationsHelpers.SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink, extraField);
                                return;
                            }
                        }
                    }

                    // attempted to ping @everyone/@here
                    if ((Program.cfgjson.EveryoneExcludedChannels == null || !Program.cfgjson.EveryoneExcludedChannels.Contains(message.Channel.Id)) && (message.Content.Contains("@everyone") || message.Content.Contains("@here")))
                    {
                        string reason = "Attempted to ping everyone/here";
                        _ = message.DeleteAsync();
                        DiscordMessage msg = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                        var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: DiscordHelpers.MessageLink(msg), message.Channel, " automatically ");
                        await InvestigationsHelpers.SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);
                        return;
                    }

                    // Mass mentions
                    if (message.MentionedUsers.Count >= Program.cfgjson.MassMentionThreshold && GetPermLevel(member) < ServerPermLevel.Tier3)
                    {
                        string reason = "Mass mentions";
                        try
                        {
                            _ = message.DeleteAsync();
                            _ = InvestigationsHelpers.SendInfringingMessaageAsync(Program.logChannel, message, reason, null);
                        }
                        catch
                        {
                            // still warn anyway
                        }

                        DiscordMessage msg = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                        var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: DiscordHelpers.MessageLink(msg), message.Channel, " automatically ");
                        await InvestigationsHelpers.SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);
                        return;
                    }

                    // line limit
                    var lineCount = CountNewlines(message.Content);

                    if (!Program.cfgjson.LineLimitExcludedChannels.Contains(channel.Id)
                        && (lineCount >= Program.cfgjson.IncreasedLineLimit
                        || (lineCount >= Program.cfgjson.LineLimit && GetPermLevel(member) < (ServerPermLevel)Program.cfgjson.LineLimitTier)))
                    {
                        string reason = "Too many lines in a single message";
                        _ = message.DeleteAsync();

                        var button = new DiscordButtonComponent(ButtonStyle.Secondary, "line-limit-deleted-message-callback", "View message content", false, null);

                        if (!Program.db.HashExists("linePardoned", message.Author.Id.ToString()))
                        {
                            await Program.db.HashSetAsync("linePardoned", member.Id.ToString(), false);
                            string output = $"{Program.cfgjson.Emoji.Information} {message.Author.Mention}, your message was deleted for containing too many lines.\n" +
                                $"Please consider using a Pastebin-style website or <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid further punishment.";
                            DiscordMessageBuilder messageBuilder = new();
                            messageBuilder.WithContent(output).AddComponents(button);
                            DiscordMessage msg = await message.Channel.SendMessageAsync(messageBuilder);
                            await Program.db.HashSetAsync("deletedMessageReferences", msg.Id, message.Content);
                            await InvestigationsHelpers.SendInfringingMessaageAsync(Program.badMsgLog, message, reason, DiscordHelpers.MessageLink(msg));
                            return;
                        }
                        else
                        {
                            string output = $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**\n" +
                                $"Please consider using a Pastebin-style website or <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.";
                            DiscordMessageBuilder messageBuilder = new();
                            messageBuilder.WithContent(output).AddComponents(button);

                            DiscordMessage msg = await message.Channel.SendMessageAsync(messageBuilder);
                            var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: DiscordHelpers.MessageLink(msg), message.Channel, " automatically ");
                            await Program.db.HashSetAsync("deletedMessageReferences", msg.Id, message.Content);
                            await InvestigationsHelpers.SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);

                            return;
                        }

                    }
                }

                if (!isAnEdit && message.Channel.Id == Program.cfgjson.FeedbackHubChannelId)
                {
                    var captures = bold_rx.Match(message.Content).Groups[1].Captures;

                    if (captures == null || captures.Count == 0 || (!message.Content.Contains("aka.ms/") && !message.Content.Contains("feedback-hub:")))
                    {
                        if (GetPermLevel(member) >= ServerPermLevel.TrialModerator)
                        {
                            return;
                        }

                        await message.DeleteAsync();
                        var msg = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} {message.Author.Mention}, please read the pinned messages in this channel and follow the message format given.");
                        await Task.Delay(5000);
                        await msg.DeleteAsync();
                    }
                    else
                    {
                        var title = captures[0].Value;

                        if (title.Length > 100)
                            title = StringHelpers.Truncate(title, 100, false);

                        AutoArchiveDuration autoArchiveTime;

                        autoArchiveTime = message.Channel.Guild.Features.Contains("THREE_DAY_THREAD_ARCHIVE") ? AutoArchiveDuration.ThreeDays : AutoArchiveDuration.Day;

                        await message.CreateThreadAsync(title, autoArchiveTime, "Automatically creating feedback hub thread.");

                        await Task.Delay(2000);
                        await message.ModifyEmbedSuppressionAsync(true);
                    }
                }

                // Check the passive lists AFTER all other checks.
                if (GetPermLevel(member) >= ServerPermLevel.TrialModerator)
                    return;

                foreach (var listItem in Program.cfgjson.WordListList)
                {
                    if (!listItem.Passive)
                    {
                        continue;
                    }
                    else if (Checks.ListChecks.CheckForNaughtyWords(message.Content.ToLower(), listItem))
                    {
                        DiscordChannel logChannel = Program.badMsgLog;

                        if (listItem.ChannelId != null)
                        {
                            logChannel = await Program.discord.GetChannelAsync((ulong)listItem.ChannelId);
                        }

                        string content = $"{Program.cfgjson.Emoji.Warning} Detected potentially suspicious message by {message.Author.Mention} in {message.Channel.Mention}:";

                        await InvestigationsHelpers.SendInfringingMessaageAsync(logChannel, message, listItem.Reason, DiscordHelpers.MessageLink(message), content: content, colour: new DiscordColor(0xFEC13D), jumpText: "Jump to message");

                    }
                }
            }
            catch (Exception e)
            {
                client.Logger.LogError(eventId: Program.CliptokEventID, message: e.ToString());

                var exs = new List<Exception>();
                if (e is AggregateException ae)
                    exs.AddRange(ae.InnerExceptions);
                else
                    exs.Add(e);

                var cliptokChannel = await client.GetChannelAsync(Program.cfgjson.HomeChannel);

                foreach (var ex in exs)
                {

                    var embed = new DiscordEmbedBuilder
                    {
                        Color = new DiscordColor("#FF0000"),
                        Title = "An exception occurred when processing a message event.",
                        Description = $"{Program.cfgjson.Emoji.BSOD} `{e.GetType()}` occurred when processing [this message]({DiscordHelpers.MessageLink(message)})!",
                        Timestamp = DateTime.UtcNow
                    };
                    embed.WithFooter(client.CurrentUser.Username, client.CurrentUser.AvatarUrl)
                        .AddField("Message", ex.Message);
                    await Program.errorLogChannel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
                }
            }

        }

        public static int CountNewlines(string input)
        {
            int count = 0;
            int len = input.Length;
            for (int i = 0; i != len; ++i)
                switch (input[i])
                {
                    case '\r':
                        ++count;
                        if (i + 1 != len && input[i + 1] == '\n')
                            ++i;
                        break;
                    case '\n':
                        ++count;
                        break;
                }
            return count;
        }

    }
}
