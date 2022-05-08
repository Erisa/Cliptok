using static Cliptok.Program;

namespace Cliptok.Events
{
    public class ThreadEvents
    {
        public static Task Discord_ThreadCreated(DiscordClient client, ThreadCreateEventArgs e)
        {
            // in case we end up in random guilds
            if (e.Guild.Id != cfgjson.ServerID)
                return Task.CompletedTask;

            e.Thread.JoinThreadAsync();
            client.Logger.LogDebug(eventId: CliptokEventID, "Thread created in {servername}. Thread Name: {threadname}", e.Guild.Name, e.Thread.Name);
            return Task.CompletedTask;
        }

        public static async Task<Task> Discord_ThreadUpdated(DiscordClient client, ThreadUpdateEventArgs e)
        {
            client.Logger.LogDebug(eventId: CliptokEventID, "Thread created in {servername}. Thread Name: {threadname}", e.Guild.Name, e.ThreadAfter.Name);

            // in case we end up in random guilds
            if (e.Guild.Id != cfgjson.ServerID)
                return Task.CompletedTask;

            if (
                db.SetContains("openthreads", e.ThreadAfter.Id)
                && e.ThreadAfter.ThreadMetadata.IsArchived
                && e.ThreadAfter.ThreadMetadata.IsLocked != true
            )
            {
                await e.ThreadAfter.ModifyAsync(a =>
                {
                    a.IsArchived = false;
                    a.AuditLogReason = "Auto unarchiving thread due to enabled keepalive state.";
                });
            }

            return Task.CompletedTask;
        }

        public static Task Discord_ThreadDeleted(DiscordClient client, ThreadDeleteEventArgs e)
        {
            client.Logger.LogDebug(eventId: CliptokEventID, "Thread deleted in {servername}. Thread Name: {threadname}", e.Guild.Name, e.Thread.Name ?? "Unknown");
            return Task.CompletedTask;
        }

        public static Task Discord_ThreadListSynced(DiscordClient client, ThreadListSyncEventArgs e)
        {
            client.Logger.LogDebug(eventId: CliptokEventID, "Threads synced in {guild}.", e.Guild.Name);
            return Task.CompletedTask;
        }

        public static Task Discord_ThreadMemberUpdated(DiscordClient client, ThreadMemberUpdateEventArgs e)
        {
            client.Logger.LogDebug(eventId: CliptokEventID, "Thread member updated.");
            client.Logger.LogDebug(CliptokEventID, "Discord_ThreadMemberUpdated fired for thread {thread}. User ID {user}.", e.ThreadMember.ThreadId, e.ThreadMember.Id);
            return Task.CompletedTask;
        }

        public static Task Discord_ThreadMembersUpdated(DiscordClient client, ThreadMembersUpdateEventArgs e)
        {
            client.Logger.LogDebug(eventId: CliptokEventID, "Thread members updated in {servername}", e.Guild.Name);
            return Task.CompletedTask;
        }

    }
}
