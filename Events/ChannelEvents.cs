namespace Cliptok.Events
{
    public class ChannelEvents
    {
        public static async Task ChannelCreated(DiscordClient _, ChannelCreatedEventArgs e)
        {
            // see comment on ChannelUpdated

            var timestamp = DateTime.UtcNow;
            Tasks.EventTasks.PendingChannelCreateEvents.Add(timestamp, e);
        }

        public static async Task ChannelUpdated(DiscordClient _, ChannelUpdatedEventArgs e)
        {
            // Add this event to the pending events list. These are handled in a task later, see Tasks/EventTasks/HandlePendingChannelUpdateEventsAsync
            // using DateTime might seem weird, but it's something that is unique for each event
            var timestamp = DateTime.UtcNow;
            Tasks.EventTasks.PendingChannelUpdateEvents.Add(timestamp, e);
        }

        public static async Task ChannelDeleted(DiscordClient client, ChannelDeletedEventArgs e)
        {
            // see comment on ChannelUpdated

            var timestamp = DateTime.UtcNow;
            Tasks.EventTasks.PendingChannelDeleteEvents.Add(timestamp, e);

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
