namespace Cliptok.Events
{
    public class AutoModEvents
    {
        public static async Task AutoModerationRuleExecuted(DiscordClient client, AutoModerationRuleExecutedEventArgs e)
        {
            if (e.Rule.Action.Type == DiscordRuleActionType.BlockMessage)
            {
                // AutoMod blocked a message. Pass it to the message handler to run it through some filters anyway.
                
                var author = await client.GetUserAsync(e.Rule.UserId);
                var channel = await client.GetChannelAsync(e.Rule.ChannelId!.Value);
                
                // Create a "mock" message object to pass to the message handler, since we don't have the actual message object
                var message = new MockDiscordMessage(author: author, channel: channel, channelId: channel.Id, content: e.Rule.Content);
                
                // Pass to the message handler
                await MessageEvent.MessageHandlerAsync(client, message, channel, false, true, true);
            }
        }
    }
}