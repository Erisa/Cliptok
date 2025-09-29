namespace Cliptok.Events
{
    public class AutoModEvents
    {
        public static async Task AutoModerationRuleExecuted(DiscordClient client, AutoModerationRuleExecutedEventArgs e)
        {
            Program.discord.Logger.LogDebug("Got an AutoMod Rule Executed event with action type {actionType} in channel {channelId} by user {userId}", e.Rule.Action.Type, e.Rule.ChannelId, e.Rule.UserId);


            if (e.Rule.Action.Type is DiscordRuleActionType.BlockMessage)
            {
                var author = await client.GetUserAsync(e.Rule.UserId);
                var channel = await client.GetChannelAsync(e.Rule.ChannelId!.Value);

                Program.discord.Logger.LogDebug("Got an AutoMod Message Block event in channel {channelId} by user {userId}", e.Rule.ChannelId, e.Rule.UserId);

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

                if (Program.cfgjson.AutoModRules.Any(r => r.RuleId == e.Rule.RuleId))
                {
                    var ruleConfig = Program.cfgjson.AutoModRules.First(r => r.RuleId == e.Rule.RuleId);
                    string reason = ruleConfig.Reason;
                    if (reason is null || reason == "")
                        reason = "Automod rule violation";

                    var user = await client.GetUserAsync(e.Rule.UserId);

                    if (user is null)
                    {
                        Program.discord.Logger.LogError("AutoMod rule executed for user {userId} but user could not be found.", e.Rule.UserId);
                        return;
                    }

                    switch (ruleConfig.Action)
                    {
                        case "mute":
                            await MuteHelpers.MuteUserAsync(user, reason, Program.discord.CurrentUser.Id, Program.homeGuild);
                            return;
                        case "warn":
                            DiscordMessage msg = await WarningHelpers.SendPublicWarningMessageAndDeleteInfringingMessageAsync(message, $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**", true);
                            var warning = await WarningHelpers.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextMessage: msg, channel, " automatically ");
                            return;
                        default:
                            throw new NotImplementedException($"Unhandled AutoMod action type: {ruleConfig.Action}");
                    }
                }

                // Pass to the message handler
                await MessageEvent.MessageHandlerAsync(client, message, channel, false, true, true);
            }

        }
    }
}