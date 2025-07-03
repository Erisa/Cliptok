using Microsoft.EntityFrameworkCore;
using static Cliptok.Constants.RegexConstants;

namespace Cliptok.Events
{
    class MessageEvent
    {
        public static Dictionary<string, string[]> wordLists = new();
        public static Dictionary<ulong, DateTime> supportRatelimit = new();

        public static Dictionary<ulong, DiscordThreadChannel> trackingThreadCache = new();
        public static Dictionary<ulong, RecentMessageInfo> duplicateMessageCache = [];
        public static List<ulong> deletedMessageCache = [];

        public static List<string> allowedInviteCodes = new();
        public static List<string> disallowedInviteCodes = new();

        static public readonly HttpClient httpClient = new();

        public static async Task MessageCreated(DiscordClient client, MessageCreatedEventArgs e)
        {
            if (e.Message is null)
            {
                client.Logger.LogError("Got a message create event but the message was null!");
                return;
            }
            else if (e.Message.Author is null)
            {
                client.Logger.LogDebug("Got a message create event for a message with no author: {message}", DiscordHelpers.MessageLink(e.Message));
            }
            else
            {
                client.Logger.LogDebug("Got a message create event for {message} by {user}", DiscordHelpers.MessageLink(e.Message), e.Message.Author.Id);
            }

            await MessageHandlerAsync(client, e.Message, e.Channel);
        }

        public static async Task MessageUpdated(DiscordClient client, MessageUpdatedEventArgs e)
        {
            if (e.Message is null)
            {
                client.Logger.LogError("Got a message update event but the message was null!");
                return;
            }
            else if (e.Message.Author is null)
            {
                client.Logger.LogDebug("Got a message update event for a message with no author: {message}", DiscordHelpers.MessageLink(e.Message));
            }
            else
            {
                client.Logger.LogDebug("Got a message update event for {message} by {user}", DiscordHelpers.MessageLink(e.Message), e.Message.Author.Id);
            }
            await MessageHandlerAsync(client, e.Message, e.Channel, true);
        }

        public static async Task MessageDeleted(DiscordClient client, MessageDeletedEventArgs e)
        {
            if (e.Message is null)
            {
                client.Logger.LogError("Got a message delete event but the message was null!");
                return;
            }
            else if (e.Message.Author is null)
            {
                client.Logger.LogDebug("Got a message delete event for a message with no author: {message}", DiscordHelpers.MessageLink(e.Message));
            }
            else
            {
                client.Logger.LogDebug("Got a message delete event for {message} by {user}", DiscordHelpers.MessageLink(e.Message), e.Message.Author.Id);
            }
            
            foreach (var warning in await Program.redis.HashGetAllAsync("automaticWarnings"))
            {
                if (JsonConvert.DeserializeObject<UserWarning>(warning.Value).ContextMessageReference.MessageId == e.Message.Id)
                    await Program.redis.HashDeleteAsync("automaticWarnings", warning.Name);
            }
            
            foreach (var ban in await Program.redis.HashGetAllAsync("compromisedAccountBans"))
            {
                if (JsonConvert.DeserializeObject<MemberPunishment>(ban.Value).ContextMessageReference.MessageId == e.Message.Id)
                    await Program.redis.HashDeleteAsync("compromisedAccountBans", ban.Name);
            }

            if (Program.cfgjson.EnablePersistentDb)
            {
                var dbContext = new CliptokDbContext();
                var cachedMessage = await dbContext.Messages.Include(m => m.User).FirstOrDefaultAsync(m => m.Id == e.Message.Id);

                // we store bot messages but don't log them right now
                if (cachedMessage is not null && !cachedMessage.User.IsBot)
                {
                    await LogChannelHelper.LogMessageAsync("messages", await DiscordHelpers.GenerateMessageRelay(cachedMessage, "deleted", true, true));
                }

                _ = dbContext.DisposeAsync();

                await DiscordHelpers.DoEmptyThreadCleanupAsync(e.Channel, e.Message);
            }
        }
        
        public static async Task MessagesBulkDeleted(DiscordClient client, MessagesBulkDeletedEventArgs e)
        {
            foreach (var message in e.Messages)
            {
                foreach (var warning in await Program.redis.HashGetAllAsync("automaticWarnings"))
                {
                    if (JsonConvert.DeserializeObject<UserWarning>(warning.Value).ContextMessageReference.MessageId == message.Id)
                        await Program.redis.HashDeleteAsync("automaticWarnings", warning.Name);
                }
            
                foreach (var ban in await Program.redis.HashGetAllAsync("compromisedAccountBans"))
                {
                    if (JsonConvert.DeserializeObject<MemberPunishment>(ban.Value).ContextMessageReference.MessageId == message.Id)
                        await Program.redis.HashDeleteAsync("compromisedAccountBans", ban.Name);
                }
            }

            if (Program.cfgjson.EnablePersistentDb)
            {
                // log the bulk deleted messages
                client.Logger.LogDebug("Got a bulk message delete event for {messagesCount} messages", e.Messages.Count);

                var messageIds = e.Messages.Select(m => m.Id).ToList();

                var dbContext = new CliptokDbContext();
                var cachedMessages = dbContext.Messages.Include(m => m.User).Where(m => messageIds.Contains(m.Id));
                await LogChannelHelper.LogMessageAsync("messages", await LogChannelHelper.CreateDumpMessageAsync($"{Program.cfgjson.Emoji.Deleted} {e.Messages.Count} messages were deleted from {e.Channel.Mention}, {cachedMessages.ToList().Count} were logged:", cachedMessages.ToList(), e.Channel));
                _ = dbContext.DisposeAsync();
            }
        }

        static async Task DeleteAndWarnAsync(DiscordMessage message, string reason, DiscordClient client, string messageContentOverride = default)
        {
            await DeleteAndWarnAsync(new MockDiscordMessage(message), reason, client, messageContentOverride: messageContentOverride);
        }

        static async Task DeleteAndWarnAsync(MockDiscordMessage message, string reason, DiscordClient client, bool wasAutoModBlock = false, string messageContentOverride = default)
        {
            var channel = message.Channel;
            DiscordMessage msg;
            string warnMsgReason = $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**";
            if (!wasAutoModBlock)
            {
                msg = await WarningHelpers.SendPublicWarningMessageAndDeleteInfringingMessageAsync(message, warnMsgReason, wasAutoModBlock, 1);
            }
            else
            {
                if (channel.Type is DiscordChannelType.GuildForum)
                {
                    if (Program.cfgjson.ForumChannelAutoWarnFallbackChannel == 0)
                        Program.discord.Logger.LogWarning("A warning in forum channel {channelId} was attempted, but may fail due to the fallback channel not being set. Please set 'forumChannelAutoWarnFallbackChannel' in config.json to avoid this.", channel.Id);
                    else
                        channel = Program.ForumChannelAutoWarnFallbackChannel;
                }

                msg = await channel.SendMessageAsync(warnMsgReason);
            }

            try
            {
                _ = InvestigationsHelpers.SendInfringingMessaageAsync("mod", message, reason, null, wasAutoModBlock: wasAutoModBlock, messageContentOverride: messageContentOverride);
            }
            catch
            {
                // still warn anyway
            }
            var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextMessage: msg, channel, " automatically ");
            await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, warning.ContextLink, messageContentOverride: messageContentOverride, wasAutoModBlock: wasAutoModBlock);
        }

        public static async Task<Models.CachedDiscordMessage> CacheAndAddMessageAsync(MockDiscordMessage message, CliptokDbContext ctx, bool disposeContext)
        {
            var cachedMessage = await CacheMessageAsync(message, ctx);
            await AddMessageToCacheAsync(cachedMessage, ctx);
            if (disposeContext)
                await ctx.DisposeAsync();
            return cachedMessage;
        }

        public static async Task<Models.CachedDiscordMessage> CacheMessageAsync(MockDiscordMessage message, CliptokDbContext ctx)
        {
            var cachedMessage = new Models.CachedDiscordMessage
            {
                Id = message.Id,
                ChannelId = message.Channel.Id,
                Content = message.Content,
                Timestamp = message.Timestamp.HasValue ? message.Timestamp.Value.UtcDateTime : DateTime.UtcNow,
                AttachmentURLs = message.Attachments?.Select(a => a.Url).ToList(),
            };

            var existingUser = await (ctx.Users.FirstOrDefaultAsync(u => u.Id == message.Author.Id));

            if (existingUser is null)
            {
                cachedMessage.User = new Models.CachedDiscordUser
                {
                    Id = message.Author.Id,
                    Username = message.Author.Username,
                    DisplayName = message.Author.GlobalName ?? message.Author.Username,
                    AvatarUrl = message.Author.AvatarUrl ?? message.Author.DefaultAvatarUrl,
                    IsBot = message.Author.IsBot
                };
                await ctx.Users.AddAsync(cachedMessage.User);
                await ctx.SaveChangesAsync();
            }
            else
            {
                cachedMessage.User = existingUser;
            }

            return cachedMessage;
        }

        public static async Task UpdateMessageAsync(Models.CachedDiscordMessage cachedMessage, CliptokDbContext ctx)
        {
            ctx.Users.Attach(cachedMessage.User);
            ctx.Messages.Update(cachedMessage);
            await ctx.SaveChangesAsync();
        }

        public static async Task AddMessageToCacheAsync(Models.CachedDiscordMessage cachedMessage, CliptokDbContext ctx)
        {
            ctx.Attach(cachedMessage.User);
            await ctx.AddAsync(cachedMessage);
            await ctx.SaveChangesAsync();
        }

        public static async Task MessageHandlerAsync(DiscordClient client, DiscordMessage message, DiscordChannel channel, bool isAnEdit = false, bool limitFilters = false, bool wasAutoModBlock = false)
        {
            await MessageHandlerAsync(client, new MockDiscordMessage(message), channel, isAnEdit, limitFilters, wasAutoModBlock);
        }
        public static async Task MessageHandlerAsync(DiscordClient client, MockDiscordMessage message, DiscordChannel channel, bool isAnEdit = false, bool limitFilters = false, bool wasAutoModBlock = false)
        {
            #region message logging fill to db

            if (Program.cfgjson.EnablePersistentDb)
            {
                if (isAnEdit)
                {
                    var dbContext = new CliptokDbContext();
                    var cachedMessage = dbContext.Messages.Include(m => m.User).FirstOrDefault(m => m.Id == message.Id);
                    if (cachedMessage is not null && cachedMessage.Content != message.Content)
                    {
                        var newMessage = await CacheMessageAsync(message, dbContext);
                        // we store bot messages but don't log them right now
                        if (cachedMessage is not null && !cachedMessage.User.IsBot)
                        {
                            await LogChannelHelper.LogMessageAsync("messages", await DiscordHelpers.GenerateMessageRelay(newMessage, "edited", true, true, cachedMessage));
                        }

                        cachedMessage.Content = newMessage.Content;
                        cachedMessage.AttachmentURLs = newMessage.AttachmentURLs;
                        await UpdateMessageAsync(cachedMessage, dbContext);
                    }
                    _ = dbContext.DisposeAsync();
                }
                else
                {
                    // cache in the background to not impede execution of the message handler
                    _ = CacheAndAddMessageAsync(message, new CliptokDbContext(), true);
                }
            }
            #endregion

            #region combine all message text
            // Get forwarded msg & embeds, if any, and combine with content to evaluate
            // Combined as a single long string

            string msgContentWithEmbedData = message.Content;
            var embeds = new List<DiscordEmbed>();

            if (message.MessageSnapshots is not null)
                foreach (var snapshot in message.MessageSnapshots)
                {
                    msgContentWithEmbedData += $" {snapshot.Message.Content}";
                    embeds.AddRange(snapshot.Message.Embeds);
                }

            foreach (var embed in embeds)
            {
                // Add any text from the embed into the content to be checked

                if (embed.Author is not null)
                {
                    if (embed.Author.Name is not null)
                        msgContentWithEmbedData += $" {embed.Author.Name}";

                    if (embed.Author.Url is not null)
                        msgContentWithEmbedData += $" {embed.Author.Url}";
                }

                if (embed.Title is not null)
                    msgContentWithEmbedData += $" {embed.Title}";

                if (embed.Url is not null)
                    msgContentWithEmbedData += $" {embed.Url}";

                if (embed.Description is not null)
                    msgContentWithEmbedData += $" {embed.Description}";

                if (embed.Footer is not null && embed.Footer.Text is not null)
                    msgContentWithEmbedData += $" {embed.Footer.Text}";

                if (embed.Fields is not null)
                    foreach (var field in embed.Fields)
                    {
                        msgContentWithEmbedData += $" {field.Name} {field.Value}";
                    }
            }
            #endregion

            #region avoid url bypass

            
            var urls = url_rx.Matches(msgContentWithEmbedData);

            // urldecode and replace all urls
            if (urls != null)
            {
                foreach (Match match in urls)
                {
                    string decodedUrl = Uri.UnescapeDataString(match.Value);
                    msgContentWithEmbedData = msgContentWithEmbedData.Replace(match.Value, decodedUrl);
                }
            }

            #endregion

            try
            {
                #region return early checks
                if (message.Timestamp is not null && message.Timestamp.Value.Year < (DateTime.Now.Year - 2))
                    return;

                if (isAnEdit && message.BaseMessage is not null && (message.BaseMessage.EditedTimestamp is null || message.BaseMessage.EditedTimestamp == message.BaseMessage.CreationTimestamp || message.BaseMessage.EditedTimestamp < DateTimeOffset.Now - TimeSpan.FromDays(1)))
                    return;

                if (message.Author is null || message.Author.Id == client.CurrentUser.Id)
                    return;
                #endregion

                #region debug logging
                if (wasAutoModBlock)
                {
                    Program.discord.Logger.LogDebug("Processing AutoMod-blocked message in {channelId} by user {userId}", channel.Id, message.Author.Id);
                    if (channel.Type == DiscordChannelType.GuildForum && Program.cfgjson.ForumChannelAutoWarnFallbackChannel != 0)
                        channel = Program.ForumChannelAutoWarnFallbackChannel;
                }
                else
                {
                    Program.discord.Logger.LogDebug("Processing message {messageId} in {channelId} by user {userId}", message.Id, channel.Id, message.Author.Id);
                }
                #endregion

                if (!limitFilters)
                {
                    #region tracked user relaying
                    if (Program.redis.SetContains("trackedUsers", message.Author.Id))
                    {
                        // Check current channel against tracking channels
                        var trackingChannels = await Program.redis.HashGetAsync("trackingChannels", message.Author.Id);
                        if (trackingChannels.HasValue)
                        {
                            var trackingChannelsList = JsonConvert.DeserializeObject<List<ulong>>(trackingChannels);

                            // Relay if this user's tracking is not filtered to any channels, or if this msg is in a channel the tracking is filtered to
                            var channels = JsonConvert.DeserializeObject<List<ulong>>(trackingChannels);
                            if (trackingChannelsList.Count == 0 || channels.Contains(channel.Id) || (channel.Parent is not null && channels.Contains(channel.Parent.Id)))
                            {
                                await RelayTrackedMessageAsync(client, message);
                            }
                        }
                        else
                        {
                            // This user's tracking is not filtered to channels, so just relay the msg to the tracking thread
                            await RelayTrackedMessageAsync(client, message);
                        }
                    }
                    #endregion

                    #region DM relaying
                    if (!isAnEdit && channel.IsPrivate && Program.cfgjson.LogChannels.ContainsKey("dms"))
                    {
                        DirectMessageEvent.DirectMessageEventHandler(message.BaseMessage);
                        return;
                    }
                    #endregion

                    #region modmail thread handling
                    if (!isAnEdit && message.Author.Id == Program.cfgjson.ModmailUserId && message.Content == "@here" && message.Embeds[0].Footer.Text.Contains("User ID:"))
                    {
                        Program.discord.Logger.LogDebug(Program.CliptokEventID, "Processing modmail message {message} in {channel}", message.Id, channel);
                        var idString = modmaiL_rx.Match(message.Embeds[0].Footer.Text).Groups[1].Captures[0].Value;
                        DiscordMember modmailMember = default;
                        try
                        {
                            modmailMember = await channel.Guild.GetMemberAsync(Convert.ToUInt64(idString));
                        }
                        catch (DSharpPlus.Exceptions.NotFoundException)
                        {
                            return;
                        }

                        DiscordMessageBuilder memberWarnInfo = new();

                        DiscordRole muted = await channel.Guild.GetRoleAsync(Program.cfgjson.MutedRole);
                        if (modmailMember.Roles.Contains(muted))
                        {
                            memberWarnInfo.AddEmbed(await WarningHelpers.GenerateWarningsEmbedAsync(modmailMember)).AddEmbed(await MuteHelpers.MuteStatusEmbed(modmailMember, channel.Guild));
                        }

                        // Add notes to message if any exist & are set to show on modmail

                        // Get user notes
                        var notes = (await Program.redis.HashGetAllAsync(modmailMember.Id.ToString()))
                            .Where(x => JsonConvert.DeserializeObject<UserNote>(x.Value).Type == WarningType.Note).ToDictionary(
                                x => x.Name.ToString(),
                                x => JsonConvert.DeserializeObject<UserNote>(x.Value)
                            );

                        // Filter to notes set to notify on modmail
                        var notesToNotify = notes.Where(x => x.Value.ShowOnModmail).ToDictionary(x => x.Key, x => x.Value);

                        // If there are notes, build embed and add to message
                        if (notesToNotify.Count != 0)
                        {
                            memberWarnInfo.AddEmbed(await UserNoteHelpers.GenerateUserNotesEmbedAsync(modmailMember, notesToUse: notesToNotify));

                            // For any notes set to show once, show the full note content in its own embed because it will not be able to be fetched manually
                            foreach (var note in notesToNotify)
                                if (memberWarnInfo.Embeds.Count < 10) // Limit to 10 embeds; this probably won't be an issue because we probably won't have that many 'show once' notes
                                    if (note.Value.ShowOnce)
                                        memberWarnInfo.AddEmbed(await UserNoteHelpers.GenerateUserNoteSimpleEmbedAsync(note.Value, modmailMember));
                        }

                        // If message was built (if user is muted OR if user has notes to show on modmail), send it
                        if (memberWarnInfo.Embeds.Count != 0)
                            await channel.SendMessageAsync(memberWarnInfo);

                        // If any notes were shown & set to show only once, delete them now
                        foreach (var note in notesToNotify.Where(note => note.Value.ShowOnce))
                        {
                            // Delete note
                            await Program.redis.HashDeleteAsync(modmailMember.Id.ToString(), note.Key);

                            // Log deletion to mod-logs channel
                            var embed = new DiscordEmbedBuilder(await UserNoteHelpers.GenerateUserNoteDetailEmbedAsync(note.Value, modmailMember)).WithColor(0xf03916);
                            await LogChannelHelper.LogMessageAsync("mod", $"{Program.cfgjson.Emoji.Deleted} Note `{note.Value.NoteId}` was automatically deleted after modmail thread creation (belonging to {modmailMember.Mention})", embed);
                        }
                    }
                    #endregion

                    #region giveaways handling
                    // handle #giveaways
                    if (!isAnEdit && message.Author.Id == Program.cfgjson.GiveawayBot && channel.Id == Program.cfgjson.GiveawaysChannel && message.Content == Program.cfgjson.GiveawayTriggerMessage)
                    {
                        string giveawayTitle = message.Embeds[0].Title;

                        if (giveawayTitle.Length > 100)
                        {
                            giveawayTitle = StringHelpers.Truncate(giveawayTitle, 100, false);
                        }

                        await message.BaseMessage.CreateThreadAsync(giveawayTitle, DiscordAutoArchiveDuration.ThreeDays, "Automatically creating giveaway thread.");
                    }
                    #endregion
                }

                #region automatic listupdate for private lists
                if (
                    Program.cfgjson.GitListDirectory is not null
                    && Program.cfgjson.GitListDirectory != ""
                    && message.Channel.Id == Program.cfgjson.HomeChannel
                    && message.Author.Discriminator == "0000"
                    && message.Embeds is not null
                    && message.Embeds.Count > 0
                    && message.Embeds[0].Title.StartsWith(Program.cfgjson.GithubWorkflowSucessString))
                {
                    string command = $"cd Lists/{Program.cfgjson.GitListDirectory} && git pull";
                    var msg = await LogChannelHelper.LogMessageAsync("home", $"{Program.cfgjson.Emoji.Loading} Updating private lists..");

                    ShellResult finishedShell = RunShellCommand(command);

                    string result = Regex.Replace(finishedShell.result, "(?:ghp)|(?:github_pat)_[0-9a-zA-Z_]+", "ghp_REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_TOKEN"), "REDACTED");

                    if (finishedShell.proc.ExitCode != 0)
                    {
                        await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} An error occurred trying to update private lists!\n```\n{result}\n```");
                    }
                    else
                    {
                        Program.UpdateLists();
                        await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully updated and reloaded private lists!\n```\n{result}\n```");
                    }
                }
                #endregion

                #region Skip DMs, external guilds, and messages from bots, beyond this point.
                if (channel.IsPrivate || channel.Guild.Id != Program.cfgjson.ServerID || message.Author.IsBot)
                    return;
                #endregion

                #region mention relaying
                if (!limitFilters && !Program.cfgjson.MentionTrackExcludedChannels.Contains(channel.Id) && (channel.ParentId is null || !Program.cfgjson.MentionTrackExcludedChannels.Contains((ulong)channel.ParentId)))
                {
                    // track mentions
                    if (message.MentionedUsers.Any(x => x.Id == Program.discord.CurrentUser.Id))
                        await LogChannelHelper.LogMessageAsync("mentions", await DiscordHelpers.GenerateMessageRelay(message.BaseMessage, true, true, false));
                }
                #endregion

                #region retrieve member object
                DiscordMember member;
                try
                {
                    member = await channel.Guild.GetMemberAsync(message.Author.Id);
                }
                catch (DSharpPlus.Exceptions.NotFoundException)
                {
                    member = default;
                }

                if (member == default)
                    return;
                #endregion

                #region content filters
                if ((await GetPermLevelAsync(member)) < ServerPermLevel.TrialModerator)
                {
                    #region block messages in forum intro thread
                    if (!limitFilters)
                    {
                        if ((channel.Id == Program.cfgjson.SupportForumIntroThreadId ||
                             Program.cfgjson.ForumIntroPosts.Contains(channel.Id)) &&
                            !member.Roles.Any(role => role.Id == Program.cfgjson.TqsRoleId))
                        {
                            await message.DeleteAsync();
                            var msg = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} {message.Author.Mention}, you can't send messages in this thread!\nTry creating a post on {channel.Parent.Mention} instead.");
                            await Task.Delay(5000);
                            await msg.DeleteAsync();
                            return;
                        }
                    }
                    #endregion

                    #region mass mentions ban filter
                    if ((message.MentionedUsers is not null && message.MentionedUsers.Count > Program.cfgjson.MassMentionBanThreshold) || (message.MentionedUsersCount > Program.cfgjson.MassMentionBanThreshold))
                    {
                        if (wasAutoModBlock)
                        {
                            Program.discord.Logger.LogDebug("AutoMod-blocked message in {channelId} by user {userId} triggered mass-mention filter", channel.Id, message.Author.Id);
                        }
                        else
                        {
                            Program.discord.Logger.LogDebug("Message {messageId} in {channelId} by user {userId} triggered mass-mention filter", message.Id, channel.Id, message.Author.Id);
                            await DiscordHelpers.ThreadChannelAwareDeleteMessageAsync(message);
                        }

                        _ = channel.Guild.BanMemberAsync(message.Author, TimeSpan.FromDays(7), $"Mentioned more than {Program.cfgjson.MassMentionBanThreshold} users in one message.");
                        var mentionCount = message.MentionedUsers is not null && message.MentionedUsers.Count > 0 ? message.MentionedUsers.Count : message.MentionedUsersCount;
                        string content = $"{Program.cfgjson.Emoji.Banned} {message.Author.Mention} was automatically banned for mentioning **{mentionCount}** users.";
                        var chatMsg = await channel.SendMessageAsync(content);
                        _ = InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, "Mass mentions (Ban threshold)", DiscordHelpers.MessageLink(chatMsg), content: content, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                        _ = InvestigationsHelpers.SendInfringingMessaageAsync("mod", message, "Mass mentions (Ban threshold)", DiscordHelpers.MessageLink(chatMsg), content: content, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                        return;
                    }
                    #endregion

                    #region duplicate message handling
                    // skip empty and null content
                    if (Program.cfgjson.DuplicateMessageSeconds != 0 && Program.cfgjson.DuplicateMessageThreshold != 0 && !isAnEdit && !limitFilters && !wasAutoModBlock && msgContentWithEmbedData is not null && msgContentWithEmbedData != "")
                    {
                        if (
                            duplicateMessageCache.ContainsKey(message.Author.Id)
                            && duplicateMessageCache[message.Author.Id].Content == msgContentWithEmbedData
                            && (DateTime.Now - duplicateMessageCache[message.Author.Id].LastMessageTime).TotalSeconds < Program.cfgjson.DuplicateMessageSeconds)
                        {
                            duplicateMessageCache[message.Author.Id].Messages.Add(message);
                            duplicateMessageCache[message.Author.Id].LastMessageTime = message.Timestamp.HasValue ? message.Timestamp.Value.UtcDateTime : DateTime.UtcNow;

                            if (duplicateMessageCache[message.Author.Id].Messages.Count >= Program.cfgjson.DuplicateMessageThreshold)
                            {
                                duplicateMessageCache[message.Author.Id].Messages.ForEach(
                                    // don't delete a message if it was deleted on a past run of this check, but keep it in the list
                                    // also don't delete the current message because we'll do that later
                                    async x => {
                                        if (x.Id != message.Id && !deletedMessageCache.Contains(x.Id))
                                        {
                                            _ = DiscordHelpers.ThreadChannelAwareDeleteMessageAsync(x);
                                            deletedMessageCache.Add(x.Id);
                                        }
                                    }
                                );
                                
                                string reason = "Duplicate message spam";
                                string output = $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason}**";
                                DiscordMessage msg = await WarningHelpers.SendPublicWarningMessageAndDeleteInfringingMessageAsync(message, output, wasAutoModBlock);
                                deletedMessageCache.Add(message.Id);
                                var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextMessage: msg, channel, " automatically ");
                                await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, warning.ContextLink, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                                return;
                            }
                        }
                        else
                        {                                
                            duplicateMessageCache[message.Author.Id] = new RecentMessageInfo
                            {
                                Content = msgContentWithEmbedData,
                                LastMessageTime = message.Timestamp.HasValue ? message.Timestamp.Value.UtcDateTime : DateTime.UtcNow,
                                Messages = [message]
                            };
                        }
                    }
                    #endregion

                    #region restricted word list filters
                    bool match = false;

                    // Matching word list
                    foreach (var listItem in Program.cfgjson.WordListList)
                    {
                        if (listItem.ExcludedChannels.Contains(channel.Id) || listItem.Passive)
                        {
                            continue;
                        }
                        else
                        {
                            (bool success, string flaggedWord) = Checks.ListChecks.CheckForNaughtyWords(msgContentWithEmbedData.ToLower(), listItem);
                            if (success)
                            {
                                if (wasAutoModBlock)
                                    Program.discord.Logger.LogDebug("AutoMod-blocked message in {channelId} by user {userId} triggered word list filter", channel.Id, message.Author.Id);
                                else
                                    Program.discord.Logger.LogDebug("Message {messageId} in {channelId} by user {userId} triggered word list filter", message.Id, channel.Id, message.Author.Id);

                                string reason = listItem.Reason;
                                try
                                {
                                    await InvestigationsHelpers.SendInfringingMessaageAsync("mod", message, reason, null, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                                }
                                catch
                                {
                                    // still warn anyway
                                }

                                if (listItem.Name == "autoban.txt" && (await GetPermLevelAsync(member)) < ServerPermLevel.Tier4)
                                {
                                    await BanHelpers.BanFromServerAsync(message.Author.Id, reason, client.CurrentUser.Id, channel.Guild, 0, channel, default, true);
                                    return;
                                }

                                //var tmp = channel.Type;

                                match = true;

                                DiscordMessage msg = await WarningHelpers.SendPublicWarningMessageAndDeleteInfringingMessageAsync(message, $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**", wasAutoModBlock, 1);
                                var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextMessage: msg, channel, " automatically ");
                                await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, warning.ContextLink, extraField: ("Match", flaggedWord, true), messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                                return;
                            }
                        }
                        if (match)
                            return;
                    }

                    if (match)
                        return;

                    #endregion

                    #region invite filter
                    string checkedMessage = msgContentWithEmbedData.Replace('\\', '/');

                    if ((await GetPermLevelAsync(member)) < (ServerPermLevel)Program.cfgjson.InviteTierRequirement && checkedMessage.Contains("dsc.gg/") ||
                        checkedMessage.Contains("invite.gg/")
                        )
                    {
                        if (wasAutoModBlock)
                        {
                            Program.discord.Logger.LogDebug("AutoMod-blocked message in {channelId} by user {userId} triggered unapproved invite filter", channel.Id, message.Author.Id);
                        }
                        else
                        {
                            Program.discord.Logger.LogDebug("Message {messageId} in {channelId} by user {userId} triggered unapproved invite filter", message.Id, channel.Id, message.Author.Id);
                        }

                        string reason = "Sent an unapproved invite";
                        try
                        {
                            _ = InvestigationsHelpers.SendInfringingMessaageAsync("mod", message, reason, null, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                        }
                        catch
                        {
                            // still warn anyway
                        }

                        DiscordMessage msg = await WarningHelpers.SendPublicWarningMessageAndDeleteInfringingMessageAsync(message, $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**", wasAutoModBlock, 1);
                        var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextMessage: msg, channel, " automatically ");
                        await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, warning.ContextLink, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                        match = true;
                        return;
                    }

                    var inviteMatches = invite_rx.Matches(checkedMessage);

                    if ((await GetPermLevelAsync(member)) < (ServerPermLevel)Program.cfgjson.InviteTierRequirement && inviteMatches.Count > 3)
                    {
                        string reason = "Sent too many invites";
                        await DeleteAndWarnAsync(message, reason, client, wasAutoModBlock, messageContentOverride: msgContentWithEmbedData);
                        match = true;
                        return;
                    }

                    foreach (Match currentMatch in inviteMatches)
                    {
                        string code = currentMatch.Groups[1].Value;

                        if (allowedInviteCodes.Contains(code) || Program.cfgjson.InviteExclusion.Contains(code))
                        {
                            continue;
                        }

                        ServerApiResponseJson maliciousCache = default;

                        maliciousCache = Program.serverApiList.FirstOrDefault(x => x.Vanity == code || x.Invite == code);

                        DiscordInvite invite = default;
                        if (maliciousCache == default)
                        {

                            if ((await GetPermLevelAsync(member)) < (ServerPermLevel)Program.cfgjson.InviteTierRequirement && disallowedInviteCodes.Contains(code))
                            {
                                if (!match)
                                {
                                    string reason = "Sent an unapproved invite";
                                    await DeleteAndWarnAsync(message, reason, client, wasAutoModBlock, messageContentOverride: msgContentWithEmbedData);
                                    match = true;
                                }
                                break;
                            }

                            try
                            {
                                invite = await client.GetInviteByCodeAsync(code);
                            }
                            catch (DSharpPlus.Exceptions.NotFoundException)
                            {
                                allowedInviteCodes.Add(code);
                                continue;
                            }
                        }

                        if (invite != default && invite.Guild is not null && (Program.cfgjson.InviteIDExclusion.Contains(invite.Guild.Id) || invite.Guild.Id == channel.Guild.Id))
                            continue;

                        if (maliciousCache == default && invite != default && invite.Guild is not null)
                            maliciousCache = Program.serverApiList.FirstOrDefault(x => x.ServerID == invite.Guild.Id.ToString());

                        if (maliciousCache != default)
                        {
                            string reason = "Sent a malicious Discord invite";

                            DiscordMessage msg = await WarningHelpers.SendPublicWarningMessageAndDeleteInfringingMessageAsync(message, $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**", wasAutoModBlock);
                            var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextMessage: msg, channel, " automatically ");

                            string responseToSend = $"```json\n{JsonConvert.SerializeObject(maliciousCache)}\n```";

                            (string name, string value, bool inline) extraField = new("Cached API response", responseToSend, false);
                            await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, warning.ContextLink, extraField, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);

                            match = true;
                            break;
                        }

                        if (invite == default || invite.Channel is null)
                        {
                            continue;
                        }


                        if (
                        (await GetPermLevelAsync(member)) < (ServerPermLevel)Program.cfgjson.InviteTierRequirement
                        && (
                            invite.Channel.Type == DiscordChannelType.Group
                            || (
                                !Program.cfgjson.InviteExclusion.Contains(code)
                                && !Program.cfgjson.InviteIDExclusion.Contains(invite.Guild.Id)
                            )
                        )
                        )
                        {
                            disallowedInviteCodes.Add(code);
                            match = await InviteCheck(invite, message, client, msgContentWithEmbedData, wasAutoModBlock);
                            if (!match)
                            {
                                string reason = "Sent an unapproved invite";
                                await DeleteAndWarnAsync(message, reason, client, wasAutoModBlock, messageContentOverride: msgContentWithEmbedData);
                            }
                            return;
                        }
                        else
                        {
                            match = await InviteCheck(invite, message, client, msgContentWithEmbedData, wasAutoModBlock);
                        }

                    }

                    if (match)
                        return;

                    #endregion

                    #region mass emoji filter
                    if (!Program.cfgjson.UnrestrictedEmojiChannels.Contains(channel.Id) && msgContentWithEmbedData.Length >= Program.cfgjson.MassEmojiThreshold)
                    {
                        char[] tempArray = msgContentWithEmbedData.Replace("🏻", "").Replace("🏼", "").Replace("🏽", "").Replace("🏾", "").Replace("🏿", "").ToCharArray();
                        int pos = 0;
                        foreach (char c in tempArray)
                        {

                            if (c == '™' || c == '®' || c == '©')
                            {
                                tempArray[pos] = ' ';
                            }
                            if (c == '\u200d' && pos + 1 < tempArray.Length)
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
                            if (wasAutoModBlock)
                            {
                                Program.discord.Logger.LogDebug("AutoMod-blocked message in {channelId} by user {userId} triggered mass emoji filter", channel.Id, message.Author.Id);
                            }
                            else
                            {
                                Program.discord.Logger.LogDebug("Message {messageId} in {channelId} by user {userId} triggered mass emoji filter", message.Id, channel.Id, message.Author.Id);
                            }

                            string reason = "Mass emoji";

                            if ((await GetPermLevelAsync(member)) == ServerPermLevel.Nothing && !Program.redis.HashExists("emojiPardoned", message.Author.Id.ToString()))
                            {
                                await Program.redis.HashSetAsync("emojiPardoned", member.Id.ToString(), false);
                                string pardonOutput;
                                if (Program.cfgjson.UnrestrictedEmojiChannels.Count > 0)
                                    pardonOutput = $"{Program.cfgjson.Emoji.Information} {message.Author.Mention}, if you want to play around with lots of emoji, please use <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.";
                                else
                                    if (wasAutoModBlock)
                                    pardonOutput = $"{Program.cfgjson.Emoji.Information} {message.Author.Mention} Your message contained too many emoji.";
                                else
                                    pardonOutput = $"{Program.cfgjson.Emoji.Information} {message.Author.Mention} Your message was automatically deleted for mass emoji.";

                                var msgOut = await WarningHelpers.SendPublicWarningMessageAndDeleteInfringingMessageAsync(message, pardonOutput, wasAutoModBlock);
                                await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, DiscordHelpers.MessageLink(msgOut), messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                                return;
                            }

                            string output = $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**";
                            if (Program.cfgjson.UnrestrictedEmojiChannels.Count > 0 && (!Program.redis.HashExists("emojiPardoned", message.Author.Id.ToString()) || Program.redis.HashGet("emojiPardoned", message.Author.Id.ToString()) == false))
                            {
                                output += $"\nIf you want to play around with lots of emoji, please use <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.";
                                await Program.redis.HashSetAsync("emojiPardoned", member.Id.ToString(), true);
                            }

                            DiscordMessage msg = await WarningHelpers.SendPublicWarningMessageAndDeleteInfringingMessageAsync(message, output, wasAutoModBlock);
                            var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextMessage: msg, channel, " automatically ");
                            await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, warning.ContextLink, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                            return;
                        }
                        #endregion

                        #region tech support relaying
                        if (!limitFilters)
                        {
                            if (channel.Id == Program.cfgjson.TechSupportChannel &&
                                message.Content.Contains($"<@&{Program.cfgjson.CommunityTechSupportRoleID}>"))
                            {
                                if (supportRatelimit.ContainsKey(message.Author.Id))
                                {
                                    if (supportRatelimit[message.Author.Id] > DateTime.Now)
                                        return;
                                    else
                                        supportRatelimit.Remove(message.Author.Id);
                                }

                                supportRatelimit.Add(message.Author.Id, DateTime.Now.Add(TimeSpan.FromMinutes(Program.cfgjson.SupportRatelimitMinutes)));

                                var embed = new DiscordEmbedBuilder()
                                    .WithTimestamp(DateTime.Now)
                                    .WithAuthor(DiscordHelpers.UniqueUsername(message.Author), null, $"https://cdn.discordapp.com/avatars/{message.Author.Id}/{message.Author.AvatarHash}.png?size=128");

                                var lastMsgs = await channel.GetMessagesBeforeAsync(message.Id, 50).ToListAsync();
                                var msgMatch = lastMsgs.FirstOrDefault(m => m.Author.Id == message.Author.Id);

                                if (msgMatch is not null)
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
                                    if (embed.ImageUrl is null)
                                        embed.WithImageUrl(message.Attachments[0].Url);
                                    else
                                        embed.ImageUrl = message.Attachments[0].Url;
                                }

                                embed.AddField("Message Link", $"https://discord.com/channels/{channel.Guild.Id}/{channel.Id}/{message.Id}");
                                var logOut = await LogChannelHelper.LogMessageAsync("support", new DiscordMessageBuilder().AddEmbed(embed));
                                _ = logOut.CreateReactionAsync(DiscordEmoji.FromName(client, ":CliptokAcknowledge:", true));
                            }
                        }
                        #endregion
                    }

                    #region phishing API
                    var urlMatches = domain_rx.Matches(msgContentWithEmbedData);
                    if (urlMatches.Count > 0 && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") is not null && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") != "useyourimagination")
                    {
                        var (phishingMatch, httpStatus, responseText, phishingResponse) = await APIs.PhishingAPI.PhishingAPICheckAsync(msgContentWithEmbedData);

                        if (httpStatus == HttpStatusCode.OK)
                        {
                            if (phishingMatch)
                            {
                                if (wasAutoModBlock)
                                {
                                    Program.discord.Logger.LogDebug("AutoMod-blocked message in {channelId} by user {userId} triggered phishing message filter", channel.Id, message.Author.Id);
                                }
                                else
                                {
                                    Program.discord.Logger.LogDebug("Message {messageId} in {channelId} by user {userId} triggered phishing message filter", message.Id, channel.Id, message.Author.Id);
                                }

                                string reason = "Sending phishing URL(s)";
                                DiscordMessage msg = await WarningHelpers.SendPublicWarningMessageAndDeleteInfringingMessageAsync(message, $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**", wasAutoModBlock);
                                var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextMessage: msg, channel, " automatically ");

                                string responseToSend = await StringHelpers.CodeOrHasteBinAsync(responseText, "json", 1000, true);

                                (string name, string value, bool inline) extraField = new("API Response", responseToSend, false);
                                await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, warning.ContextLink, extraField, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                                return;
                            }
                        }
                    }
                    #endregion

                    #region everyone/here ping filter
                    var msgContent = msgContentWithEmbedData;
                    foreach (var letter in Checks.ListChecks.lookalikeAlphabetMap)
                        msgContent = msgContent.Replace(letter.Key, letter.Value);
                    if (Program.cfgjson.EveryoneFilter && !member.Roles.Any(role => Program.cfgjson.EveryoneExcludedRoles.Contains(role.Id)) && !Program.cfgjson.EveryoneExcludedChannels.Contains(channel.Id) && (msgContent.Contains("@everyone") || msgContent.Contains("@here")))
                    {
                        if (wasAutoModBlock)
                        {
                            Program.discord.Logger.LogDebug("AutoMod-blocked message in {channelId} by user {userId} triggered everyone/here mention filter", channel.Id, message.Author.Id);
                        }
                        else
                        {
                            Program.discord.Logger.LogDebug("Message {messageId} in {channelId} by user {userId} triggered everyone/here mention filter", message.Id, channel.Id, message.Author.Id);
                        }

                        string reason = "Attempted to ping everyone/here";
                        DiscordMessage msg = await WarningHelpers.SendPublicWarningMessageAndDeleteInfringingMessageAsync(message, $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**", wasAutoModBlock);
                        var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextMessage: msg, channel, " automatically ");
                        await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, warning.ContextLink, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                        return;
                    }
                    #endregion

                    #region mass mentions warn filter
                    if (((message.MentionedUsers is not null && message.MentionedUsers.Count >= Program.cfgjson.MassMentionThreshold) || (message.MentionedUsersCount >= Program.cfgjson.MassMentionThreshold)) && (await GetPermLevelAsync(member)) < ServerPermLevel.Tier3)
                    {
                        if (wasAutoModBlock)
                        {
                            Program.discord.Logger.LogDebug("AutoMod-blocked message in {channelId} by user {userId} triggered mass mention filter", channel.Id, message.Author.Id);
                        }
                        else
                        {
                            Program.discord.Logger.LogDebug("Message {messageId} in {channelId} by user {userId} triggered mass mention filter", message.Id, channel.Id, message.Author.Id);
                        }

                        string reason = "Mass mentions";
                        try
                        {
                            _ = InvestigationsHelpers.SendInfringingMessaageAsync("mod", message, reason, null, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                        }
                        catch
                        {
                            // still warn anyway
                        }

                        DiscordMessage msg = await WarningHelpers.SendPublicWarningMessageAndDeleteInfringingMessageAsync(message, $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**", wasAutoModBlock);
                        var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextMessage: msg, channel, " automatically ");
                        await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, warning.ContextLink, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                        return;
                    }
                    #endregion

                    #region line limit filter
                    var lineCount = CountNewlines(msgContentWithEmbedData);

                    if (!Program.cfgjson.LineLimitExcludedChannels.Contains(channel.Id)
                        && (channel.ParentId is null || !Program.cfgjson.LineLimitExcludedChannels.Contains((ulong)channel.ParentId))
                        && (lineCount >= Program.cfgjson.IncreasedLineLimit
                        || (lineCount >= Program.cfgjson.LineLimit && (await GetPermLevelAsync(member)) < (ServerPermLevel)Program.cfgjson.LineLimitTier)))
                    {
                        if (wasAutoModBlock)
                        {
                            Program.discord.Logger.LogDebug("AutoMod-blocked message in {channelId} by user {userId} triggered line limit filter", channel.Id, message.Author.Id);
                        }
                        else
                        {
                            Program.discord.Logger.LogDebug("Message {messageId} in {channelId} by user {userId} triggered line limit filter", message.Id, channel.Id, message.Author.Id);
                            await DiscordHelpers.ThreadChannelAwareDeleteMessageAsync(message);
                        }

                        string reason = "Too many lines in a single message";

                        if (!Program.redis.SetContains("linePardoned", message.Author.Id.ToString()))
                        {
                            await Program.redis.SetAddAsync("linePardoned", member.Id.ToString());
                            string output;
                            if (wasAutoModBlock)
                                output = $"{Program.cfgjson.Emoji.Information} {message.Author.Mention}, your message contained too many lines.\n" +
                                         $"Please consider using a Pastebin-style website or <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid further punishment.";
                            else
                                output = $"{Program.cfgjson.Emoji.Information} {message.Author.Mention}, your message was deleted for containing too many lines.\n" +
                                         $"Please consider using a Pastebin-style website or <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid further punishment.";
                            DiscordMessageBuilder messageBuilder = new();
                            messageBuilder.WithContent(output);
                            DiscordMessage msg;
                            if (!wasAutoModBlock)
                            {
                                messageBuilder.AddActionRowComponent(new DiscordButtonComponent(DiscordButtonStyle.Secondary, "line-limit-deleted-message-callback", "View message content", false, null));
                                msg = await channel.SendMessageAsync(messageBuilder);
                                await Program.redis.HashSetAsync("deletedMessageReferences", msg.Id, message.Content);
                            }
                            else
                            {
                                msg = await channel.SendMessageAsync(messageBuilder);
                            }
                            await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, DiscordHelpers.MessageLink(msg), messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);
                            return;
                        }
                        else
                        {
                            string output = $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**\n" +
                                $"Please consider using a Pastebin-style website or <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.";
                            DiscordMessageBuilder messageBuilder = new();
                            messageBuilder.WithContent(output);
                            DiscordMessage msg;
                            if (!wasAutoModBlock)
                            {
                                messageBuilder.AddActionRowComponent(new DiscordButtonComponent(DiscordButtonStyle.Secondary, "line-limit-deleted-message-callback", "View message content", false, null));
                                msg = await channel.SendMessageAsync(messageBuilder);
                                await Program.redis.HashSetAsync("deletedMessageReferences", msg.Id, message.Content);
                            }
                            else
                            {
                                msg = await channel.SendMessageAsync(messageBuilder);
                            }

                            var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextMessage: msg, channel, " automatically ");
                            await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, warning.ContextLink, messageContentOverride: msgContentWithEmbedData, wasAutoModBlock: wasAutoModBlock);

                            return;
                        }

                    }
                    #endregion
                }
                #endregion

                if (!limitFilters)
                {
                    #region feedback hub forum validation
                    if ((await GetPermLevelAsync(member)) < ServerPermLevel.TrialModerator && !isAnEdit && channel.IsThread && channel.ParentId == Program.cfgjson.FeedbackHubForum && !Program.redis.SetContains("processedFeedbackHubThreads", channel.Id))
                    {
                        var thread = (DiscordThreadChannel)channel;
                        Program.redis.SetAdd("processedFeedbackHubThreads", thread.Id);

                        // we need to make sure this is the first message in the channel
                        if ((await thread.GetMessagesBeforeAsync(message.Id).ToListAsync()).Count == 0)
                        {
                            // lock thread if there is no possible feedback hub link
                            if (!message.Content.Contains("aka.ms/") && !message.Content.Contains("feedback-hub:"))
                            {
                                await message.BaseMessage.RespondAsync($"{Program.cfgjson.Emoji.Error} Your {channel.Parent.Mention} submission must include a Feedback Hub link!\nThis post will be automatically deleted shortly.");
                                await thread.ModifyAsync(thread =>
                                {
                                    thread.IsArchived = true;
                                    thread.Locked = true;
                                });
                                await Task.Delay(30000);
                                await LogChannelHelper.LogMessageAsync("messages",
                                    new DiscordMessageBuilder()
                                        .WithContent($"{Program.cfgjson.Emoji.Deleted} Deleted non-feedback post from {message.Author.Mention} in {channel.Parent.Mention}:")
                                        .AddEmbed(new DiscordEmbedBuilder()
                                            .WithAuthor(
                                                $"{DiscordHelpers.UniqueUsername(message.Author)} in #{channel.Parent.Name}",
                                                null, await LykosAvatarMethods.UserOrMemberAvatarURL(message.Author, channel.Guild))
                                            .WithTitle(thread.Name)
                                            .WithDescription(message.Content)
                                            .WithColor(DiscordColor.Red)
                                        )

                                );
                                await thread.DeleteAsync();
                                return;
                            }
                            else
                            {
                                await Task.Delay(2000);
                                await message.BaseMessage.ModifyEmbedSuppressionAsync(true);
                            }
                        }
                    }
                    #endregion

                    #region passive list matching
                    // Check the passive lists AFTER all other checks.
                    if ((await GetPermLevelAsync(member)) >= ServerPermLevel.TrialModerator)
                        return;

                    foreach (var listItem in Program.cfgjson.WordListList)
                    {
                        if (!listItem.Passive)
                        {
                            continue;
                        }
                        else
                        {
                            (bool success, string flaggedWord) = Checks.ListChecks.CheckForNaughtyWords(message.Content.ToLower(), listItem);
                            if (success)
                            {
                                DiscordChannel logChannel = default;
                                if (listItem.ChannelId is not null)
                                {
                                    logChannel = await Program.discord.GetChannelAsync((ulong)listItem.ChannelId);
                                }

                                string content = $"{Program.cfgjson.Emoji.Warning} Detected potentially suspicious message by {message.Author.Mention} in {channel.Mention}:";

                                (string name, string value, bool inline) extraField = new("Match", flaggedWord, true);

                                await InvestigationsHelpers.SendInfringingMessaageAsync(
                                    "investigations",
                                    message,
                                    listItem.Reason,
                                    DiscordHelpers.MessageLink(message),
                                    content: content,
                                    colour: new DiscordColor(0xFEC13D),
                                    channelOverride: logChannel,
                                    extraField: extraField,
                                    wasAutoModBlock: wasAutoModBlock
                                );
                            }
                        }
                    }
                    #endregion
                }
            }
            catch (Exception e)
            {
                client.Logger.LogError(eventId: Program.CliptokEventID, message: "{message}", e.ToString());

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
                    await LogChannelHelper.LogMessageAsync("errors", embed: embed.Build()).ConfigureAwait(false);
                }
            }

            if (wasAutoModBlock)
                Program.discord.Logger.LogDebug("AutoMod-blocked message in {channelId} by user {userId} triggered no filters!", channel.Id, message.Author.Id);
            else
                Program.discord.Logger.LogDebug("Message {messageId} in {channelId} by user {userId} triggered no filters!", message.Id, channel.Id, message.Author.Id);
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

        public static async Task<bool> InviteCheck(DiscordInvite? invite, MockDiscordMessage message, DiscordClient client, string messageContentOverride = null, bool wasAutoModBlock = false)
        {
            if (invite is null || invite.Guild is null)
                return false;

            (bool serverMatch, HttpStatusCode httpStatus, string responseString, ServerApiResponseJson? serverResponse) = await APIs.ServerAPI.ServerAPICheckAsync(invite.Guild.Id);

            if (httpStatus != HttpStatusCode.OK)
            {
                Program.discord.Logger.LogError("Error checking if server {id} posted by {member} is malicious: {code}\n{response}", invite.Guild.Id, message.Author.Id, httpStatus, responseString);
                return false;
            }
            else if (serverMatch)
            {
                if (!wasAutoModBlock)
                    await DiscordHelpers.ThreadChannelAwareDeleteMessageAsync(message);
                string reason = "Sent a malicious Discord invite";

                DiscordMessage msg = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextMessage: msg, message.Channel, " automatically ");

                string responseToSend = $"```json\n{responseString}\n```";

                (string name, string value, bool inline) extraField = new("API Response", responseToSend, false);
                await InvestigationsHelpers.SendInfringingMessaageAsync("investigations", message, reason, warning.ContextLink, extraField, messageContentOverride: messageContentOverride, wasAutoModBlock: wasAutoModBlock);

                var newEntry = JsonConvert.DeserializeObject<ServerApiResponseJson>(responseString);
                newEntry.Invite = invite.Code;
                newEntry.ServerID = invite.Guild.Id.ToString();
                Program.serverApiList.Add(newEntry);
                return true;
            }
            else
            {
                return false;
            }
        }

        private static async Task RelayTrackedMessageAsync(DiscordClient client, DiscordMessage message)
        {
            await RelayTrackedMessageAsync(client, new MockDiscordMessage(message));
        }
        private static async Task RelayTrackedMessageAsync(DiscordClient client, MockDiscordMessage message)
        {
            DiscordThreadChannel relayThread;

            if (trackingThreadCache.ContainsKey(message.Author.Id))
            {
                relayThread = trackingThreadCache[message.Author.Id];
            }
            else
            {
                relayThread = (DiscordThreadChannel)await client.GetChannelAsync(
                    (ulong)await Program.redis.HashGetAsync("trackingThreads", message.Author.Id));
                trackingThreadCache.Add(message.Author.Id, relayThread);
            }

            var _ = await relayThread.SendMessageAsync(
                await DiscordHelpers.GenerateMessageRelay(message.BaseMessage, true, true));
        }

    }
}
