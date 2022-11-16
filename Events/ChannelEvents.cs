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
    
                // Check if overwrite exists in db for current channel being updated
                foreach (var overwriteHash in dbOverwrites)
                {
                    var overwriteDict =
                        JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(overwriteHash.Value);
    
                    if (overwriteDict is null) continue;
    
                    foreach (var overwrite in overwriteDict.Where(overwrite => overwrite.Key == e.ChannelAfter.Id))
                        if (currentChannelOverwrites.Any(x => x.Id == overwrite.Value.Id))
                        {
                            if (overwrite.Value.Type == OverwriteType.Role) continue;
    
                            // Overwrite exists in both db and channel, check if they are the same
                            var currentOverwrite = currentChannelOverwrites.First(x => x.Id == overwrite.Value.Id);
    
                            if (currentOverwrite.Allowed == overwrite.Value.Allowed &&
                                currentOverwrite.Denied == overwrite.Value.Denied) continue;
    
                            // Overwrite is different, update db
    
                            var userDbOverwrites =
                                await Program.db.HashGetAsync("overrides", currentOverwrite.Id.ToString());
    
                            Dictionary<ulong, DiscordOverwrite> userDbOverwritesDict;
                            if (string.IsNullOrWhiteSpace(userDbOverwrites))
                                userDbOverwritesDict = new Dictionary<ulong, DiscordOverwrite>();
                            else
                                try
                                {
                                    userDbOverwritesDict =
                                        JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(
                                            userDbOverwrites);
                                }
                                catch
                                {
                                    userDbOverwritesDict = new Dictionary<ulong, DiscordOverwrite>();
                                }
    
                            if (userDbOverwritesDict is null)
                            {
                                await Program.db.HashDeleteAsync("overrides", currentOverwrite.Id);
                                continue;
                            }
    
                            userDbOverwritesDict.Remove(e.ChannelAfter.Id);
                            userDbOverwritesDict.Add(e.ChannelAfter.Id, currentOverwrite);
                            await Program.db.HashSetAsync("overrides", currentOverwrite.Id,
                                JsonConvert.SerializeObject(userDbOverwritesDict));
    
                            return;
                        }
                        else
                        {
                            // Overwrite exists in db but not in channel, remove from db
    
                            // Check if member left - if it looks like they left, dont delete overrides
    
                            // Delay to allow leave to complete first
                            await Task.Delay(500);
    
                            try
                            {
                                await e.Guild.GetMemberAsync((ulong)overwriteHash.Name);
                            }
                            catch
                            {
                                // Failed to fetch user. They probably left or were otherwise removed from the server.
                                // Preserve overrides.
                                return;
                            }
    
                            var userDbOverwrites = await Program.db.HashGetAsync("overrides", overwrite.Value.Id);
                            Dictionary<ulong, DiscordOverwrite> userDbOverwritesDict;
                            if (string.IsNullOrWhiteSpace(userDbOverwrites))
                                userDbOverwritesDict = new Dictionary<ulong, DiscordOverwrite>();
                            else
                                try
                                {
                                    userDbOverwritesDict =
                                        JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(
                                            userDbOverwrites);
                                }
                                catch
                                {
                                    userDbOverwritesDict = new Dictionary<ulong, DiscordOverwrite>();
                                }
    
                            if (userDbOverwritesDict is null)
                            {
                                await Program.db.HashDeleteAsync("overrides", overwrite.Value.Id);
                                continue;
                            }
    
                            userDbOverwritesDict.Remove(e.ChannelAfter.Id);
    
                            if (userDbOverwritesDict.Count > 0)
                                await Program.db.HashSetAsync("overrides", overwrite.Value.Id,
                                    JsonConvert.SerializeObject(userDbOverwritesDict));
                            else
                                await Program.db.HashDeleteAsync("overrides", overwrite.Value.Id);
    
                            return;
                        }
                }
    
                // Overwrite is not in db; add
                foreach (var overwrite in currentChannelOverwrites)
                    if (overwrite.Type != OverwriteType.Role)
                    {
                        var allUserOverwrites = new Dictionary<ulong, Dictionary<ulong, DiscordOverwrite>>();
                        // <user ID, <channel ID, overwrite>>

                        foreach (var dbOverwriteHash in dbOverwrites)
                        {
                            var dbOverwrite =
                                JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(
                                    dbOverwriteHash.Value);

                            if (dbOverwrite is null) continue;

                            foreach (var item in dbOverwrite.Where(item => item.Value.Type == OverwriteType.Member))
                            {
                                if (!allUserOverwrites.ContainsKey(item.Value.Id) &&
                                    item.Value.Type == OverwriteType.Member)
                                {
                                    allUserOverwrites.Add(item.Value.Id,
                                        new Dictionary<ulong, DiscordOverwrite> { { item.Key, item.Value } });
                                }
                            }
                        }
                        
                        foreach (var item in e.ChannelAfter.PermissionOverwrites)
                        {
                            if (item.Type != OverwriteType.Member) continue;
                            
                            var dict = new Dictionary<ulong, DiscordOverwrite> { { e.ChannelAfter.Id, item } };

                            if (allUserOverwrites.ContainsKey(item.Id))
                                allUserOverwrites[item.Id].Add(e.ChannelAfter.Id, item);
                            else
                                allUserOverwrites.Add(item.Id, dict);
                        }

                        foreach (var userOverwrites in allUserOverwrites)
                        {
                            await Program.db.HashSetAsync("overrides", userOverwrites.Value.First().Value.Id,
                                JsonConvert.SerializeObject(userOverwrites.Value));
                        }
                    }
            });
        }
    }
}