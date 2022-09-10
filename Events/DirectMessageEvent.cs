namespace Cliptok.Events
{
    public class DirectMessageEvent
    {
        public static async void DirectMessageEventHandler(DiscordMessage message)
        {
            await LogChannelHelper.LogMessageAsync("dms", await DiscordHelpers.GenerateMessageRelay(message));
        }
    }
}
