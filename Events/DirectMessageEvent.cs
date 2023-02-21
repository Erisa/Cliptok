namespace Cliptok.Events
{
    public class DirectMessageEvent
    {
        public static async void DirectMessageEventHandler(DiscordMessage message)
        {
            // Auto-response to contact modmail if DM follows warn/mute and is within configured time limit

            bool sentAutoresponse = false;
            
            // Ignore messages older than time limit (in hours)
            if ((DateTime.UtcNow - message.CreationTimestamp.DateTime).TotalHours < Program.cfgjson.DmAutoresponseTimeLimit)
            {
                // Make sure there is a message before the current one, otherwise an exception could be thrown
                var msgBefore = await message.Channel.GetMessagesBeforeAsync(message.Id, 1);
                if (msgBefore.Count > 0)
                {
                    // Make sure the message before the current one is from the bot and is a warn/mute DM & respond
                    if (msgBefore[0].Author.Id == Program.discord.CurrentUser.Id &&
                        (msgBefore[0].Content.Contains("You were warned") ||
                         msgBefore[0].Content.Contains("You have been muted")))
                    {
                        await message.RespondAsync(
                            "You can discuss warnings and punishments with the moderators by messaging modmail:" +
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
