using static Cliptok.Program;

namespace Cliptok.Events
{
    public class ReactionEvent
    {
        public static async Task OnReaction(DiscordClient client, MessageReactionAddEventArgs e)
        {
            Task.Run(async () =>
            {
                if (e.Emoji.Id != cfgjson.HeartosoftId || e.Channel.IsPrivate || e.Guild.Id != cfgjson.ServerID)
                    return;

                bool handled = false;

                DiscordMessage targetMessage = await e.Channel.GetMessageAsync(e.Message.Id);

                DiscordEmoji noHeartosoft = await e.Guild.GetEmojiAsync(cfgjson.NoHeartosoftId);

                await Task.Delay(1000);

                if (targetMessage.Author.Id == e.User.Id)
                {
                    await targetMessage.DeleteReactionAsync(e.Emoji, e.User);
                    handled = true;
                }

                foreach (string word in cfgjson.RestrictedHeartosoftPhrases)
                {
                    if (targetMessage.Content.ToLower().Contains(word))
                    {
                        if (!handled)
                            await targetMessage.DeleteReactionAsync(e.Emoji, e.User);

                        await targetMessage.CreateReactionAsync(noHeartosoft);
                        return;
                    }
                }
            });
        }
    }
}
