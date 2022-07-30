namespace Cliptok.Helpers
{
    public class DiscordHelpers
    {
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
            return $"https://discord.com/channels/{msg.Channel.Guild.Id}/{msg.Channel.Id}/{msg.Id}";
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
                return await channel.GetMessageAsync(messageReference.MessageId);
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
                output.AppendLine($"{message.Author.Username}#{message.Author.Discriminator} [{message.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss zzz")}] (User: {message.Author.Id}) (Message: {message.Id})");

                if (message.ReferencedMessage is not null)
                {
                    output.AppendLine($"[Replying to {message.ReferencedMessage.Author.Username}#{message.ReferencedMessage.Author.Discriminator} (User: {message.ReferencedMessage.Author.Id}) (Message: {message.ReferencedMessage.Id})]");
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

    }
}
