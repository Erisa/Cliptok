namespace Cliptok.Events
{
    public class ChannelEvents
    {
        public static async Task ChannelUpdated(DiscordClient _, ChannelUpdatedEventArgs e)
        {
            // Add this event to the pending events list. These are handled in a task later, see Tasks/EventTasks/HandlePendingChannelUpdateEventsAsync
            // using DateTime might seem weird, but it's something that is unique for each event
            var timestamp = DateTime.Now;
            Tasks.EventTasks.PendingChannelUpdateEvents.Add(timestamp, e);
        }

        public static async Task ChannelDeleted(DiscordClient client, ChannelDeletedEventArgs e)
        {
            // see above

            var timestamp = DateTime.Now;
            Tasks.EventTasks.PendingChannelDeleteEvents.Add(timestamp, e);
        }
    }
}