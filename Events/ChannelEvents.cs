namespace Cliptok.Events
{
    public class ChannelEvents
    {
        public static async Task ChannelCreated(DiscordClient _, ChannelCreatedEventArgs e)
        {
            Program.discord.Logger.LogDebug("Got a channel created event for {channel}", e.Channel.Id);

            // see comment on ChannelUpdated

            var timestamp = DateTime.UtcNow;
            Tasks.EventTasks.PendingChannelCreateEvents.Add(timestamp, e);
            Program.discord.Logger.LogDebug("There are now {count} pending channel create events", Tasks.EventTasks.PendingChannelCreateEvents.Count);
        }

        public static async Task ChannelUpdated(DiscordClient _, ChannelUpdatedEventArgs e)
        {
            Program.discord.Logger.LogDebug("Got a channel updated event for {channel}", e.ChannelAfter.Id);

            // Add this event to the pending events list. These are handled in a task later, see Tasks/EventTasks/HandlePendingChannelUpdateEventsAsync
            // using DateTime might seem weird, but it's something that is unique for each event
            var timestamp = DateTime.UtcNow;
            Tasks.EventTasks.PendingChannelUpdateEvents.Add(timestamp, e);
            Program.discord.Logger.LogDebug("There are now {count} pending channel update events", Tasks.EventTasks.PendingChannelUpdateEvents.Count);
        }

        public static async Task ChannelDeleted(DiscordClient client, ChannelDeletedEventArgs e)
        {
            Program.discord.Logger.LogDebug("Got a channel deleted event for {channel}", e.Channel.Id);

            // see comment on ChannelUpdated

            var timestamp = DateTime.UtcNow;
            Tasks.EventTasks.PendingChannelDeleteEvents.Add(timestamp, e);
            Program.discord.Logger.LogDebug("There are now {count} pending channel delete events", Tasks.EventTasks.PendingChannelDeleteEvents.Count);

            if (
                e.Channel.ParentId != Program.cfgjson.ModmailCategory &&
                e.Guild.Id == Program.cfgjson.ServerID &&
                Program.cfgjson.EnablePersistentDb)
            {
                try
                {
                    await DiscordHelpers.DumpCachedMessagesForChannelAsync(displayName: $"Channel **{e.Channel.Name ?? "Unknown"}** ({e.Channel.Id})", e.Channel);
                }
                catch (Exception ex)
                {
                    Program.discord.Logger.LogError(ex, "Failed to dump cached messages for deleted channel {channelId}", e.Channel.Id);
                }
            }
        }
    }
}
