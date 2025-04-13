﻿namespace Cliptok.Helpers
{
    public class InvestigationsHelpers
    {
        public static async Task SendInfringingMessaageAsync(string logChannelKey, DiscordMessage infringingMessage, string reason, string messageURL, (string name, string value, bool inline) extraField = default, string content = default, DiscordColor? colour = null, DiscordChannel channelOverride = default)
        {
            await SendInfringingMessaageAsync(logChannelKey, new MockDiscordMessage(infringingMessage), reason, messageURL, extraField, content, colour, channelOverride);
        }
        public static async Task SendInfringingMessaageAsync(string logChannelKey, MockDiscordMessage infringingMessage, string reason, string messageURL, (string name, string value, bool inline) extraField = default, string content = default, DiscordColor? colour = null, DiscordChannel channelOverride = default, string messageContentOverride = default, bool wasAutoModBlock = false)
        {
            if (colour is null)
                colour = new DiscordColor(0xf03916);
            
            // If logging to #investigations and there is embed/forward data, leave it out & add a note to check #mod-logs instead
            if (logChannelKey == "investigations" && !string.IsNullOrEmpty(messageContentOverride) && messageContentOverride != infringingMessage.Content)
                messageContentOverride = $"{infringingMessage.Content}\n-# [...full content omitted, check <#{LogChannelHelper.GetLogChannelId("mod")}>...]";

            var embed = new DiscordEmbedBuilder()
            .WithDescription(string.IsNullOrWhiteSpace(messageContentOverride) ? infringingMessage.Content : messageContentOverride)
            .WithColor((DiscordColor)colour)
            .WithTimestamp(infringingMessage.Timestamp)
            .WithFooter(
                $"User ID: {infringingMessage.Author.Id}",
                null
            )
            .WithAuthor(
                $"{DiscordHelpers.UniqueUsername(infringingMessage.Author)} in #{infringingMessage.Channel.Name}",
                null,
                await LykosAvatarMethods.UserOrMemberAvatarURL(infringingMessage.Author, infringingMessage.Channel.Guild, "png")
            );

            if (reason is not null && reason != "")
                embed.AddField("Reason", reason, true);

            if (messageURL is not null)
                embed.AddField("Message link", messageURL, true);

            if (extraField != default)
                embed.AddField(extraField.name, extraField.value, extraField.inline);

            if (content == default)
                if (wasAutoModBlock)
                    content = $"{Program.cfgjson.Emoji.Denied} Detected infringing AutoMod message by {infringingMessage.Author.Mention} in {infringingMessage.Channel.Mention}:";
                else
                    content = $"{Program.cfgjson.Emoji.Denied} Deleted infringing message by {infringingMessage.Author.Mention} in {infringingMessage.Channel.Mention}:";

            DiscordMessage logMsg;
            if (channelOverride == default)
                logMsg = await LogChannelHelper.LogMessageAsync(logChannelKey, content, embed);
            else
                logMsg = await channelOverride.SendMessageAsync(new DiscordMessageBuilder().WithContent(content).AddEmbed(embed).WithAllowedMentions(Mentions.None));
            
            // Add reaction to log message to be used to delete
            if (logChannelKey == "investigations")
            {
                var emoji = DiscordEmoji.FromGuildEmote(Program.discord, Convert.ToUInt64(Constants.RegexConstants.id_rx.Match(Program.cfgjson.Emoji.Deleted).ToString()));
                await logMsg.CreateReactionAsync(emoji);
                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(Program.cfgjson.WarningLogReactionTimeMinutes));
                    await logMsg.DeleteOwnReactionAsync(emoji);
                });
            }
        }

    }
}
