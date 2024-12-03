using static Cliptok.Program;
using static Cliptok.Constants.RegexConstants;

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
                await LogChannelHelper.LogMessageAsync("reactions", $"<:WindowsRecycleBin:824380487920910348> Removed reaction {emoji} from {e.Message.JumpLink} by {e.User.Mention}");
                return;
            }

            // Remove self-heartosofts

            if (e.Emoji.Id != cfgjson.HeartosoftId)
                return;

            // Avoid starboard race conditions
            await Task.Delay(1000);

            if (targetMessage.Author.Id == e.User.Id)
            {
                await targetMessage.DeleteReactionAsync(e.Emoji, e.User);
            }
        }
    }
}
