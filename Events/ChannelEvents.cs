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

                foreach (var userOverwrites in dbOverwrites)
                {
                    var overwriteDict = JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(userOverwrites.Value);

                    // If the db overwrites are not in the current channel overwrites, remove them from the db.

                    foreach (var overwrite in overwriteDict)
                    {
                        // (if overwrite is for a different channel, skip)
                        if (overwrite.Key != e.ChannelAfter.Id) continue;
                        
                        // (if current overwrite is in the channel, skip)
                        if (currentChannelOverwrites.Any(a => a == overwrite.Value && e.ChannelAfter.Id == overwrite.Key)) continue;
                        
                        // If it looks like the member left, do NOT remove their overrides.

                        // Delay to allow leave to complete first
                        await Task.Delay(500);

                        // Try to fetch member. If it fails, they are not in the guild.
                        try
                        {
                            await e.Guild.GetMemberAsync((ulong)userOverwrites.Name);
                        }
                        catch
                        {
                            // Failed to fetch user. They probably left or were otherwise removed from the server.
                            // Preserve overrides.
                            return;
                        }

                        // User could be fetched, so they are in the server and their override was removed. Remove from db.
                        
                        var overrides = await Program.db.HashGetAsync("overrides", userOverwrites.Name);
                        var dict = JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(overrides);
                        dict.Remove(e.ChannelAfter.Id);
                        if (dict.Count > 0)
                            await Program.db.HashSetAsync("overrides", userOverwrites.Name, JsonConvert.SerializeObject(dict));
                        else
                        {
                            await Program.db.HashDeleteAsync("overrides", userOverwrites.Name);
                        }
                    }
                }

                foreach (var overwrite in currentChannelOverwrites)
                {
                    // Ignore role overrides because we aren't storing those
                    if (overwrite.Type == OverwriteType.Role) continue;

                    // If the current channel overwrites are not in the db, add them to the db.

                    if (dbOverwrites
                        .Select(dbOverwrite => JsonConvert.DeserializeObject<DiscordOverwrite>(dbOverwrite.Value))
                        .All(dbOverwriteObj => dbOverwriteObj != overwrite))
                    {
                        if ((await Program.db.HashKeysAsync("overrides")).Any(a => a == overwrite.Id.ToString()))
                        {
                            // User has an overwrite in the db; add this one to their list of overrides without
                            // touching existing ones

                            var overwrites = await Program.db.HashGetAsync("overrides", overwrite.Id);

                            if (!string.IsNullOrWhiteSpace(overwrites))
                            {
                                var dict = JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(overwrites);

                                if (dict is not null)
                                {                                    
                                    dict.Add(e.ChannelAfter.Id, overwrite);

                                    if (dict.Count > 0)
                                        await Program.db.HashSetAsync("overrides", overwrite.Id,
                                            JsonConvert.SerializeObject(dict));
                                    else
                                        await Program.db.HashDeleteAsync("overrides", overwrite.Id);
                                }
                            }
                        }
                        else
                        {
                            // User doesn't have any overrides in db, so store new dictionary

                            await Program.db.HashSetAsync("overrides",
                                overwrite.Id, JsonConvert.SerializeObject(new Dictionary<ulong, DiscordOverwrite>
                                    { { e.ChannelAfter.Id, overwrite } }));
                        }
                    }
                }
            });
        }
    }
}