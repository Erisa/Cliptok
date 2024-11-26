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
                targetMessage.Content.Contains("was warned") ||
                targetMessage.Content.Contains("has been muted") ||
                targetMessage.Content.Contains("has been banned"))
            {
                await targetMessage.DeleteReactionAsync(e.Emoji, e.User);
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
