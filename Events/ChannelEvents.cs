namespace Cliptok.Events
{
    public class ChannelEvents
    {
        public static async Task ChannelUpdated(DiscordClient _, ChannelUpdateEventArgs e)
        {
            Task.Run(async () =>
            {
                // Sync channel overwrites with db so that they can be restored when a user leaves & rejoins.

                // Get the current channel overwrites
                var currentChannelOverwrites = e.ChannelAfter.PermissionOverwrites;

                // Get the db overwrites
                var dbOverwrites = await Program.db.HashGetAllAsync("overrides");

                // Compare the two and sync them, prioritizing overwrites on channel over stored overwrites

                foreach (var overwrite in dbOverwrites)
                {
                    var overwriteObj = JsonConvert.DeserializeObject<DiscordOverwrite>(overwrite.Value);

                    // If the db overwrites are not in the current channel overwrites, remove them from the db.
                    if (currentChannelOverwrites.All(a => a != overwriteObj))
                    {
                        // If it looks like the member left, do NOT remove their overrides.

                        // Delay to allow leave to complete first
                        await Task.Delay(500);

                        try
                        {
                            await e.Guild.GetMemberAsync((ulong)overwrite.Name);
                        }
                        catch
                        {
                            // Failed to fetch user. They probably left or were otherwise removed from the server. Preserve overrides.
                            return;
                        }

                        // User could be fetched, so they are in the server and their override was removed. Remove from db.
                        await Program.db.HashDeleteAsync("overrides", overwrite.Name);
                    }
                }

                foreach (var overwrite in currentChannelOverwrites)
                {
                    // Ignore role overrides because we aren't storing those
                    if (overwrite.Type == OverwriteType.Role) continue;

                    // If the current channel overwrites are not in the db, add them to the db.

                    bool overwriteInDb = false;
                    foreach (var dbOverwrite in dbOverwrites)
                    {
                        var dbOverwriteObj = JsonConvert.DeserializeObject<DiscordOverwrite>(dbOverwrite.Value);
                        if (dbOverwriteObj == overwrite)
                        {
                            overwriteInDb = true;
                            break;
                        }
                    }

                    if (!overwriteInDb)
                    {
                        await Program.db.HashSetAsync("overrides",
                            (await overwrite.GetMemberAsync()).Id,
                            JsonConvert.SerializeObject(new Dictionary<ulong, DiscordOverwrite>
                                { { e.ChannelAfter.Id, overwrite } }));
                    }
                }
            });
        }
    }
}
