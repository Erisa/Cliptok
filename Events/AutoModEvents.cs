namespace Cliptok.Events
{
    public class AutoModEvents
    {
        public static async Task AutoModerationRuleExecuted(DiscordClient client, AutoModerationRuleExecutedEventArgs e)
        {
            Program.discord.Logger.LogDebug("Got an AutoMod Rule Executed event with action type {actionType} in channel {channelId} by user {userId}", e.Rule.Action.Type, e.Rule.ChannelId, e.Rule.UserId);

            if (e.Rule.Action.Type == DiscordRuleActionType.BlockMessage)
            {
                // AutoMod blocked a message. Pass it to the message handler to run it through some filters anyway.

                Program.discord.Logger.LogDebug("Got an AutoMod Message Block event in channel {channelId} by user {userId}", e.Rule.ChannelId, e.Rule.UserId);

                var author = await client.GetUserAsync(e.Rule.UserId);
                var channel = await client.GetChannelAsync(e.Rule.ChannelId!.Value);

                // Create a "mock" message object to pass to the message handler, since we don't have the actual message object
                var mentionedUsers = new List<ulong>();
                if (e.Rule.Content is not null)
                {
                    foreach (var match in Constants.RegexConstants.user_rx.Matches(e.Rule.Content))
                    {
                        var id = Convert.ToUInt64(((Match)match).Groups[1].ToString());
                        if (!mentionedUsers.Contains(id))
                            mentionedUsers.Add(id);
                    }
                }
                var message = new MockDiscordMessage(author: author, channel: channel, channelId: channel.Id, content: e.Rule.Content, mentionedUsersCount: mentionedUsers.Count);

                // Pass to the message handler
                await MessageEvent.MessageHandlerAsync(client, message, channel, false, true, true);
            }
        }
    }
}