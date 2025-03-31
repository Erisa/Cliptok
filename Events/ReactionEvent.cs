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
            
            if (e.Channel.Id != cfgjson.InvestigationsChannelId && e.Channel.Id != cfgjson.LogChannels["mod"].ChannelId)
                return;
            
            var member = await e.Guild.GetMemberAsync(e.User.Id);
            if (await GetPermLevelAsync(member) < ServerPermLevel.TrialModerator)
                return;
            
            var recycleBinEmoji = DiscordEmoji.FromGuildEmote(discord, Convert.ToUInt64(id_rx.Match(cfgjson.Emoji.Deleted).ToString()));
            
            if (e.Channel.Id == cfgjson.LogChannels["mod"].ChannelId)
            {
                var warningId = targetMessage.Embeds[0].Fields?.First(x => x.Name == "Warning ID").Value;
                if (warningId is null) // probably reacted to non-warning msg, ignore
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
                    
                    await targetMessage.DeleteReactionsEmojiAsync(recycleBinEmoji);
                }
                else
                {
                    await targetMessage.CreateReactionAsync(DiscordEmoji.FromName(discord, ":x:"));
                }
            }
            else
            {
                // TODO #investigations
            }
        }
    }
}
