namespace Cliptok.Events
{
    public class DirectMessageEvent
    {
        public static async void DirectMessageEventHandler(DiscordMessage message)
        {
            // Ignore message if user is blocked
            if (await Program.db.SetContainsAsync("dmRelayBlocklist", message.Author.Id)) return;

            // Auto-response to contact modmail if DM follows warn/mute and is within configured time limit

            bool sentAutoresponse = false;

            // Make sure there is a message before the current one, otherwise an exception could be thrown
            var msgsBefore = await message.Channel.GetMessagesBeforeAsync(message.Id, 1).ToListAsync();
            if (msgsBefore.Count() > 0)
            {
                // Get single message before the current one
                var msgBefore = msgsBefore[0];

                // Ignore messages older than time limit (in hours)
                if ((DateTime.UtcNow - msgBefore.CreationTimestamp.DateTime).TotalHours < Program.cfgjson.DmAutoresponseTimeLimit)
                {
                    // Make sure the message before the current one is from the bot and is a warn/mute DM & respond
                    if (msgBefore.Author.Id == Program.discord.CurrentUser.Id &&
                        (msgBefore.Content.Contains("You were warned") ||
                            msgBefore.Content.Contains("You have been muted") ||
                            msgBefore.Content.Contains("You were automatically warned")))
                    {
                        await message.RespondAsync(
                            $"{Program.cfgjson.Emoji.Information} If you wish to discuss moderator actions, **please contact**" +
                            $" <@{Program.cfgjson.ModmailUserId}>");
                        sentAutoresponse = true;
                    }
                }
            }

            // Log DMs to DM log channel, include note about auto-response if applicable
            await LogChannelHelper.LogMessageAsync("dms", await DiscordHelpers.GenerateMessageRelay(message, sentAutoresponse: sentAutoresponse));
        }
    }
}
