namespace Cliptok.Events
{
    public class DirectMessageEvent
    {
        public static async void DirectMessageEventHandler(DiscordMessage message)
        {
            var dmLog = await Program.discord.GetChannelAsync(Program.cfgjson.DmLogChannelId);
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithAuthor($"{message.Author.Username}#{message.Author.Discriminator}", null, message.Author.AvatarUrl)
                .WithDescription(message.Content)
                .WithFooter($"Channel ID: {message.Channel.Id} | User ID: {message.Author.Id}");

            if (message.Stickers.Count > 0)
            {
                foreach (var sticker in message.Stickers)
                {
                    var url = sticker.StickerUrl;
                    // d#+ is dumb
                    if (sticker.FormatType is StickerFormat.APNG)
                    {
                        url = url.Replace(".apng", ".png");
                    }

                    string fieldValue = $"[{sticker.Name}]({url})";
                    if (sticker.FormatType is StickerFormat.APNG or StickerFormat.LOTTIE)
                    {
                        fieldValue += " (Animated)";
                    }

                    embed.AddField($"Sticker", fieldValue);

                    if (message.Attachments.Count == 0 && message.Stickers.Count == 1)
                    {
                        embed.WithImageUrl(url);
                    }
                }
            }

            if (message.Attachments.Count > 0)
                embed.WithImageUrl(message.Attachments[0].Url)
                    .AddField($"Attachment", $"[{message.Attachments[0].FileName}]({message.Attachments[0].Url})");

            List<DiscordEmbed> embeds = new()
            {
                        embed
                    };

            if (message.Attachments.Count > 1)
            {
                foreach (var attachment in message.Attachments.Skip(1))
                {
                    embeds.Add(new DiscordEmbedBuilder()
                        .WithAuthor($"{message.Author.Username}#{message.Author.Discriminator}", null, message.Author.AvatarUrl)
                        .AddField("Additional attachment", $"[{attachment.FileName}]({attachment.Url})")
                        .WithImageUrl(attachment.Url));
                }
            }

            await dmLog.SendMessageAsync(new DiscordMessageBuilder().AddEmbeds(embeds.AsEnumerable()));

        }
    }
}
