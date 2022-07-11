namespace Cliptok.Events
{
    public class VoiceEvents
    {
        public static async Task VoiceStateUpdate(DiscordClient client, VoiceStateUpdateEventArgs e)
        {        
            if (e.After.Channel is null)
            {
                client.Logger.LogDebug($"{e.User.Username} left {e.Before.Channel.Name}");

                // todo: remove the user's "send messages" override from e.Before.Channel, without breaking their other perms
            }
            else if (e.Before is null)
            {
                client.Logger.LogDebug($"{e.User.Username} joined {e.After.Channel.Name}");
                
                // todo: add a user "send message" override to e.After.Channel, without breaking other user override perms
            } 
            else if (e.Before.Channel.Id != e.After.Channel.Id)
            {
                client.Logger.LogDebug($"{e.User.Username} moved from {e.Before.Channel.Name} to {e.After.Channel.Name}");

                // todo: remove their "send message" override from e.Before.Channel and add one to e.After.Channel instead
            }

            if (e.Before is not null && e.Before.Channel.Users.Count == 0)
            {
                client.Logger.LogDebug($"{e.Before.Channel.Name} is now empty!");

                // todo: purge message history, on delay
            }

        }
    }
}
