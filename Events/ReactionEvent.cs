using static Cliptok.Constants.RegexConstants;
using static Cliptok.Program;

namespace Cliptok.Events
{
    public class ReactionEvent
    {
        public static async Task OnReaction(DiscordClient _, MessageReactionAddedEventArgs e)
        {
            // Ignore DMs and other servers
            if (e.Channel.IsPrivate || e.Guild.Id != cfgjson.ServerID)
                return;

            DiscordMessage targetMessage = await e.Channel.GetMessageAsync(e.Message.Id);

            // Remove reactions from warning/mute/ban messages

            if (targetMessage.Author.Id == discord.CurrentUser.Id &&
                warn_msg_rx.IsMatch(targetMessage.Content) ||
                auto_warn_msg_rx.IsMatch(targetMessage.Content) ||
                mute_msg_rx.IsMatch(targetMessage.Content) ||
                unmute_msg_rx.IsMatch(targetMessage.Content) ||
                ban_msg_rx.IsMatch(targetMessage.Content) ||
                unban_msg_rx.IsMatch(targetMessage.Content))
            {
                await targetMessage.DeleteReactionAsync(e.Emoji, e.User);
                var emoji = e.Emoji.Id != 0 ? $"[{e.Emoji.Name}](<{e.Emoji.Url}>)" : e.Emoji.ToString();
                await LogChannelHelper.LogMessageAsync("reactions", $"<:WindowsRecycleBin:824380487920910348> Removed reaction {emoji} from {targetMessage.JumpLink} by {e.User.Mention}");
                return;
            }

            // Remove self-heartosofts
            if (e.Emoji.Id == cfgjson.HeartosoftId)
            {
                // Avoid starboard race conditions
                await Task.Delay(1000);

                if (targetMessage.Author.Id == e.User.Id)
                {
                    await targetMessage.DeleteReactionAsync(e.Emoji, e.User);
                }
            }

            // Reaction to undo warnings in log channels
            
            if (e.User.Id == discord.CurrentUser.Id)
                return;
            
            if (e.Channel.Id != cfgjson.LogChannels["investigations"].ChannelId && e.Channel.Id != cfgjson.LogChannels["mod"].ChannelId)
                return;
            
            var member = await e.Guild.GetMemberAsync(e.User.Id);
            if (await GetPermLevelAsync(member) < ServerPermLevel.TrialModerator)
                return;
            
            var recycleBinEmoji = DiscordEmoji.FromName(discord, ":CliptokRecycleBin:", true);
            
            // Ignore reactions that are not the CliptokRecycleBin emoji!!
            if (e.Emoji != recycleBinEmoji)
                return;
            
            if (e.Channel.Id == cfgjson.LogChannels["mod"].ChannelId)
            {
                string warningId;
                try
                {
                    warningId = targetMessage.Embeds[0].Fields?.First(x => x.Name == "Warning ID").Value;
                }
                catch
                {
                    // probably reacted to invalid msg, ignore
                    return;
                }
                if (warningId is null) // probably reacted to invalid msg, ignore
                    return;
                
                var targetUserId = Convert.ToUInt64(user_rx.Match(targetMessage.Content).Groups[1].ToString());
                var warning = WarningHelpers.GetWarning(targetUserId, Convert.ToUInt32(warningId));
                
                if ((await GetPermLevelAsync(member)) == ServerPermLevel.TrialModerator && warning.ModUserId != e.User.Id && warning.ModUserId != discord.CurrentUser.Id)
                    return;
                
                if (warning is null)
                {
                    await targetMessage.DeleteReactionsEmojiAsync(recycleBinEmoji);
                    return;
                }

                bool success = await WarningHelpers.DelWarningAsync(warning, targetUserId);
                if (success)
                {
                    await LogChannelHelper.LogMessageAsync("mod",
                        new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Deleted} Warning deleted:" +
                                         $"`{StringHelpers.Pad(warning.WarningId)}` (belonging to <@{targetUserId}>, deleted by {e.User.Mention})")
                            .AddEmbed(await WarningHelpers.FancyWarnEmbedAsync(warning, true, 0xf03916, true, targetUserId))
                            .WithAllowedMentions(Mentions.None)
                    );
                }
                else
                {
                    var errorEmoji = DiscordEmoji.FromName(discord, ":CliptokCritical:", true);
                    await targetMessage.CreateReactionAsync(errorEmoji);
                }
            }
            else
            {
                // Collect data from message
                var userId = user_rx.Match(targetMessage.Content).Groups[1].ToString();
                string reason;
                try
                {
                    reason = targetMessage.Embeds[0].Fields?.First(x => x.Name == "Reason").Value;
                }
                catch
                {
                    // probably reacted to invalid msg, ignore
                    return;
                }
                var userWarnings = (await Program.db.HashGetAllAsync(userId));
                
                // Try to match against user warnings;
                // match warnings that have a reason that exactly matches the reason in the msg,
                // and that are explicitly warnings (WarningType.Warning), not notes
                
                UserWarning warning = null;
                
                var matchingWarnings = userWarnings.Where(x =>
                {
                    var warn = JsonConvert.DeserializeObject<UserWarning>(x.Value);
                    return warn.WarnReason == reason && warn.Type == WarningType.Warning;
                }).Select(x => JsonConvert.DeserializeObject<UserWarning>(x.Value)).ToList();
                
                var errorEmoji = DiscordEmoji.FromName(discord, ":CliptokCritical:", true);
                if (matchingWarnings.Count > 1)
                {
                    bool foundMatch = false;
                    foreach (var match in matchingWarnings)
                    {
                        // timestamps of warning msg & warning are within a minute, this is most likely the correct warning
                        if (targetMessage.Timestamp.ToUniversalTime() - match.WarnTimestamp.ToUniversalTime() < TimeSpan.FromMinutes(1))
                        {
                            warning = match;
                            foundMatch = true;
                            break;   
                        }
                    }
                    if (!foundMatch)
                    {
                        await targetMessage.CreateReactionAsync(errorEmoji);
                        return;
                    }
                }
                else if (matchingWarnings.Count < 1)
                {
                    await targetMessage.CreateReactionAsync(errorEmoji);
                    return;
                }
                else
                {
                    warning = matchingWarnings.First();
                }
                
                if ((await GetPermLevelAsync(member)) == ServerPermLevel.TrialModerator && warning.ModUserId != e.User.Id && warning.ModUserId != discord.CurrentUser.Id)
                {
                    await targetMessage.CreateReactionAsync(errorEmoji);
                }
                else
                {
                    bool success = await WarningHelpers.DelWarningAsync(warning, warning.TargetUserId);
                    if (success)
                    {
                        await LogChannelHelper.LogMessageAsync("mod",
                            new DiscordMessageBuilder()
                                .WithContent($"{Program.cfgjson.Emoji.Deleted} Warning deleted:" +
                                             $"`{StringHelpers.Pad(warning.WarningId)}` (belonging to <@{warning.TargetUserId}>, deleted by {e.User.Mention})")
                                .AddEmbed(await WarningHelpers.FancyWarnEmbedAsync(warning, true, 0xf03916, true, warning.TargetUserId))
                                .WithAllowedMentions(Mentions.None)
                        );
                        
                        var successEmoji = DiscordEmoji.FromName(discord, ":CliptokSuccess:", true);
                        await targetMessage.CreateReactionAsync(successEmoji);
                    }
                    else
                    {
                        await targetMessage.CreateReactionAsync(errorEmoji);
                    }
                }
            }
        }
    }
}
