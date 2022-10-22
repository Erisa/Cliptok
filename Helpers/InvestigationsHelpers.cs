namespace Cliptok.Helpers
{
    public class InvestigationsHelpers
    {
        public static async Task SendInfringingMessaageAsync(string logChannelKey, DiscordMessage infringingMessage, string reason, string messageURL, (string name, string value, bool inline) extraField = default, string content = default, DiscordColor? colour = null, string jumpText = "Jump to warning", DiscordChannel channelOverride = default)
        {
            if (colour is null)
                colour = new DiscordColor(0xf03916);

            var embed = new DiscordEmbedBuilder()
            .WithDescription(infringingMessage.Content)
            .WithColor((DiscordColor)colour)
            .WithTimestamp(infringingMessage.Timestamp)
            .WithFooter(
                $"User ID: {infringingMessage.Author.Id}",
                null
            )
            .WithAuthor(
                $"{infringingMessage.Author.Username}#{infringingMessage.Author.Discriminator} in #{infringingMessage.Channel.Name}",
                null,
                await LykosAvatarMethods.UserOrMemberAvatarURL(infringingMessage.Author, infringingMessage.Channel.Guild, "png")
            );

            if (reason != null && reason != "")
                embed.AddField("Reason", reason, true);

            if (messageURL != null)
                embed.AddField("Message link", $"[`{jumpText}`]({messageURL})", true);

            if (extraField != default)
                embed.AddField(extraField.name, extraField.value, extraField.inline);

            if (content == default)
                content = $"{Program.cfgjson.Emoji.Denied} Deleted infringing message by {infringingMessage.Author.Mention} in {infringingMessage.Channel.Mention}:";

            if (channelOverride == default)
                await LogChannelHelper.LogMessageAsync(logChannelKey, content, embed);
            else
                await channelOverride.SendMessageAsync(new DiscordMessageBuilder().WithContent(content).WithEmbed(embed).WithAllowedMentions(Mentions.None));
        }

    }
}
