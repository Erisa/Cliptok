using static Cliptok.Program;

namespace Cliptok.Events
{
    public class ReactionEvent
    {
        public static async Task OnReaction(DiscordClient _, MessageReactionAddedEventArgs e)
        {
            if (e.Emoji.Id != cfgjson.HeartosoftId || e.Channel.IsPrivate || e.Guild.Id != cfgjson.ServerID)
                return;

            DiscordMessage targetMessage = await e.Channel.GetMessageAsync(e.Message.Id);

            await Task.Delay(1000);

            if (targetMessage.Author.Id == e.User.Id)
            {
                await targetMessage.DeleteReactionAsync(e.Emoji, e.User);
            }
        }
    }
}
