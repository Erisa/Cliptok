namespace Cliptok.Helpers
{
    public class DiscordHelpers
    {
        public static string UniqueUsername(DiscordUser user)
        {
            if (user.Discriminator == "0")
                return user.Username;
            else
                return $"{user.Username}#{user.Discriminator}";
        }

        public static async Task<bool> SafeTyping(DiscordChannel channel)
        {
            try
            {
                await channel.TriggerTypingAsync();
                return true;
            }
            catch (Exception ex)
            {
                Program.discord.Logger.LogError(eventId: Program.CliptokEventID, exception: ex, message: "Error occurred trying to type in {channel}", args: channel.Id);
                return false;
            }
        }

        public static string MessageLink(DiscordMessage msg)
        {
            return MessageLink(new MockDiscordMessage(msg));
        }
        public static string MessageLink(MockDiscordMessage msg)
        {
            return $"https://discord.com/channels/{(msg.Channel.IsPrivate ? "@me" : msg.Channel.Guild.Id)}/{msg.Channel.Id}/{msg.Id}";
        }

        // If invoker is allowed to mod target.
        public static bool AllowedToMod(DiscordMember invoker, DiscordMember target)
        {
            return GetHier(invoker) > GetHier(target);
        }

        public static int GetHier(DiscordMember target)
        {
            return target.IsOwner ? int.MaxValue : (!target.Roles.Any() ? 0 : target.Roles.Max(x => x.Position));
        }

        public static async Task<DiscordMessage?> GetMessageFromReferenceAsync(MessageReference messageReference)
        {
            if (messageReference is null || messageReference.ChannelId == 0 || messageReference.MessageId == 0)
                return null;

            try
            {
                var channel = await Program.discord.GetChannelAsync(messageReference.ChannelId);
                DiscordMessage message;
                try
                {
                    message = await channel.GetMessageAsync(messageReference.MessageId);
                    return message;
                }
                catch
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                Program.discord.Logger.LogWarning(eventId: Program.CliptokEventID, exception: ex, message: "Failed to fetch message {message}-{channel}", messageReference.ChannelId, messageReference.MessageId);
                return null;
            }
        }

        public static async Task<string> CompileMessagesAsync(List<DiscordMessage> messages, DiscordChannel channel)
        {
            var output = new StringBuilder().Append($"-- Messages in #{channel.Name} ({channel.Id}) -- {channel.Guild.Name} ({channel.Guild.Id}) --\n");

            foreach (DiscordMessage message in messages)
            {
                output.AppendLine();
                output.AppendLine($"{DiscordHelpers.UniqueUsername(message.Author)} [{message.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss zzz")}] (User: {message.Author.Id}) (Message: {message.Id})");

                if (message.ReferencedMessage is not null)
                {
                    output.AppendLine($"[Replying to {DiscordHelpers.UniqueUsername(message.ReferencedMessage.Author)} (User: {message.ReferencedMessage.Author.Id}) (Message: {message.ReferencedMessage.Id})]");
                }

                if (message.Content is not null && message.Content != "")
                {
                    output.AppendLine($"{message.Content}");
                }

                if (message.Attachments.Count != 0)
                {
                    foreach (DiscordAttachment attachment in message.Attachments)
                    {
                        output.AppendLine($"{attachment.Url}");
                    }
                }

                if (message.Stickers.Count != 0)
                {
                    foreach (var sticker in message.Stickers)
                    {
                        output.AppendLine($"[Sticker: {sticker.Name}] ({sticker.StickerUrl})");
                    }
                }
            }

            return output.ToString();
        }

        public static async Task<DiscordEmbed> GenerateUserEmbed(DiscordUser user, DiscordGuild? guild)
        {
            DiscordMember member = default;
            DiscordEmbedBuilder embed = default;

            bool guildNull = false;

            if (guild is null)
            {
                guild = Program.homeGuild;
                guildNull = true;
            }

            string avatarUrl = await LykosAvatarMethods.UserOrMemberAvatarURL(user, guild, "default", 256);

            try
            {
                member = await guild.GetMemberAsync(user.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                embed = new DiscordEmbedBuilder()
                    .WithThumbnail(avatarUrl)
                    .WithTitle($"User information for {DiscordHelpers.UniqueUsername(user)}")
                    .AddField("User", user.Mention, true)
                    .AddField("User ID", user.Id.ToString(), true)
                    .AddField($"{Program.discord.CurrentUser.Username} permission level", "N/A (not in server)", true);

                if (!guildNull)
                    embed.AddField("Roles", "N/A (not in server)", false);

                embed.AddField("Last joined server", "N/A (not in server)", true)
                   .AddField("Account created", $"<t:{TimeHelpers.ToUnixTimestamp(user.CreationTimestamp.DateTime)}:F>", true);
                return embed.Build();
            }

            string rolesStr = "None";

            if (member.Roles.Any())
            {
                rolesStr = "";

                string truncatedComment = "\n(Truncated. User has an obscene amount of roles.)";

                foreach (DiscordRole role in member.Roles.OrderBy(x => x.Position).Reverse())
                {
                    if (rolesStr.Length + (role.Mention.Length + 1) > (1024 - truncatedComment.Length))
                    {
                        rolesStr += truncatedComment;
                        break;
                    }
                    rolesStr += role.Mention + " ";
                }
            }

            embed = new DiscordEmbedBuilder()
                .WithThumbnail(avatarUrl)
                .WithTitle($"User information for {DiscordHelpers.UniqueUsername(user)}")
                .AddField("User", member.Mention, true)
                .AddField("User ID", member.Id.ToString(), true)
                .AddField($"{Program.discord.CurrentUser.Username} permission level", (await GetPermLevelAsync(member)).ToString(), false);

            if (!guildNull)
                embed.AddField("Roles", rolesStr, false);

            embed.AddField("Last joined server", $"<t:{TimeHelpers.ToUnixTimestamp(member.JoinedAt.DateTime)}:F>", true)
                .AddField("Account created", $"<t:{TimeHelpers.ToUnixTimestamp(member.CreationTimestamp.DateTime)}:F>", true);
            return embed.Build();
        }

        public static async Task<DiscordMessageBuilder> GenerateMessageRelay(DiscordMessage message, bool jumplink = false, bool channelRef = false, bool showChannelId = true, bool sentAutoresponse = false)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithAuthor($"{DiscordHelpers.UniqueUsername(message.Author)}{(channelRef ? $" in #{message.Channel.Name}" : "")}", null, message.Author.AvatarUrl)
                .WithDescription(message.Content)
                .WithFooter($"{(showChannelId ? $"Channel ID: {message.Channel.Id} | " : "")}User ID: {message.Author.Id}");

            if (message.Stickers.Count > 0)
            {
                foreach (var sticker in message.Stickers)
                {
                    string fieldValue = $"[{sticker.Name}]({sticker.StickerUrl})";
                    if (sticker.FormatType is DiscordStickerFormat.APNG or DiscordStickerFormat.LOTTIE)
                    {
                        fieldValue += " (Animated)";
                    }

                    embed.AddField($"Sticker", fieldValue);

                    if (message.Attachments.Count == 0 && message.Stickers.Count == 1 && sticker.FormatType is not DiscordStickerFormat.LOTTIE)
                    {
                        embed.WithImageUrl(sticker.StickerUrl.Replace("cdn.discordapp.com", "media.discordapp.net") + "?size=160");
                    }
                }
            }

            if (message.Attachments.Count > 0)
                embed.WithImageUrl(message.Attachments[0].Url)
                    .AddField($"Attachment", $"[{message.Attachments[0].FileName}]({message.Attachments[0].Url})");

            if (jumplink)
                embed.AddField("Message Link", $"{MessageLink(message)}");


            if (message.ReferencedMessage is not null)
            {
                embed.WithTitle($"Replying to {message.ReferencedMessage.Author.Username}")
                    .WithUrl(MessageLink(message.ReferencedMessage));
            }

            if (sentAutoresponse)
            {
                embed.Footer.Text += "\nThis DM triggered an autoresponse.";
            }

            List<DiscordEmbed> embeds = new()
            {
                embed
            };

            if (message.Attachments.Count > 1)
            {
                foreach (var attachment in message.Attachments.Skip(1))
                {
                    embeds.Add(new DiscordEmbedBuilder()
                        .WithAuthor($"{DiscordHelpers.UniqueUsername(message.Author)}", null, message.Author.AvatarUrl)
                        .AddField("Additional attachment", $"[{attachment.FileName}]({attachment.Url})")
                        .WithImageUrl(attachment.Url));
                }
            }

            return new DiscordMessageBuilder().AddEmbeds(embeds.AsEnumerable());
        }
        
        public static async Task<bool> DoEmptyThreadCleanupAsync(DiscordChannel channel, DiscordMessage message, int minMessages = 0)
        {
            return await DoEmptyThreadCleanupAsync(channel, new MockDiscordMessage(message), minMessages);
        }
        
        public static async Task<bool> DoEmptyThreadCleanupAsync(DiscordChannel channel, MockDiscordMessage message, int minMessages = 0)
        {
            // Delete thread if all messages are deleted.
            // Otherwise, do nothing.
            // Returns whether the thread was deleted.
            
            if (Program.cfgjson.AutoDeleteEmptyThreads && channel is DiscordThreadChannel)
            {
                try
                {
                    var member = await channel.Guild.GetMemberAsync(message.Author.Id);
                    if ((await GetPermLevelAsync(member)) >= ServerPermLevel.TrialModerator)
                        return false;
                }
                catch
                {
                    // User is not in the server. Assume they are not a moderator,
                    // so do nothing here.
                }

                IReadOnlyList<DiscordMessage> messages;
                try
                {
                    messages = await channel.GetMessagesAsync(minMessages + 1).ToListAsync();
                }
                catch (DSharpPlus.Exceptions.NotFoundException ex)
                {
                    Program.discord.Logger.LogDebug(ex, "Delete event failed to fetch messages from channel {channel}", channel.Id);
                    return false;
                }

                // If this is coming after an automatic warning, 1 message in the thread is okay;
                // this is the message that triggered the warning, and we can just delete the thread.
                if (messages.Count == minMessages)
                {
                    await channel.DeleteAsync("All messages in thread were deleted.");
                    return true;
                }
            }
            
            return false;
        }
        
        public static async Task ThreadChannelAwareDeleteMessageAsync(DiscordMessage message, int minMessages = 0)
        {
            await ThreadChannelAwareDeleteMessageAsync(new MockDiscordMessage(message), minMessages);
        }
        
        public static async Task<bool> ThreadChannelAwareDeleteMessageAsync(MockDiscordMessage message, int minMessages = 0)
        {
            // Deletes a message in a thread channel, or if it is the last message, deletes the thread instead.
            // If this is not a thread channel, just deletes the message.
            
            bool wasThreadDeleted = false;
            
            if (message.Channel.Type == DiscordChannelType.GuildForum || message.Channel.Parent.Type == DiscordChannelType.GuildForum)
            {
                wasThreadDeleted = await DoEmptyThreadCleanupAsync(message.Channel, message, minMessages);
                if (!wasThreadDeleted)
                    await message.DeleteAsync();
            }
            else
                await message.DeleteAsync();
            
            return wasThreadDeleted;
        }

    }
}
