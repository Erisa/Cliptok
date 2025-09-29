namespace Cliptok.Tasks
{
    public class EventTasks
    {
        public static Dictionary<DateTime, ChannelCreatedEventArgs> PendingChannelCreateEvents = new();
        public static Dictionary<DateTime, ChannelUpdatedEventArgs> PendingChannelUpdateEvents = new();
        public static Dictionary<DateTime, ChannelDeletedEventArgs> PendingChannelDeleteEvents = new();

        // populated in Channel Create & Update handlers to save API calls in IsMemberInServer method
        private static List<MemberPunishment> CurrentBans = new();

        // set to true if the last attempt to populate CurrentBans failed, to suppress warnings in case of repeated failures
        private static bool LastBanListPopulationFailed = false;

        // todo(milkshake): combine create & update handlers to reduce duplicate code
        public static async Task<bool> HandlePendingChannelCreateEventsAsync()
        {
            bool success = false;

            // populate CurrentBans list
            try
            {
                Dictionary<string, MemberPunishment> bans = (await Program.redis.HashGetAllAsync("bans")).ToDictionary(
                    x => x.Name.ToString(),
                    x => JsonConvert.DeserializeObject<MemberPunishment>(x.Value)
                );

                CurrentBans = bans.Values.ToList();

                LastBanListPopulationFailed = false;
            }
            catch (Exception ex)
            {
                if (!LastBanListPopulationFailed)
                    Program.discord.Logger.LogWarning(ex, "Failed to populate list of current bans during override persistence checks! This warning will be suppressed until the next success!");

                // Since this is likely caused by corrupt or otherwise unreadable data in the db, set a flag so that this warning is not spammed
                // The flag will be reset on the next successful attempt to populate the CurrentBans list
                LastBanListPopulationFailed = true;
            }

            try
            {
                foreach (var pendingEvent in PendingChannelCreateEvents)
                {
                    // This is the timestamp on this event, used to identify it / keep events in order in the list
                    var timestamp = pendingEvent.Key;

                    // This is a set of ChannelCreatedEventArgs for the event we are processing
                    var e = pendingEvent.Value;

                    try
                    {
                        // Sync channel overwrites with db so that they can be restored when a user leaves & rejoins.

                        // Get the current channel overwrites
                        var currentChannelOverwrites = e.Channel.PermissionOverwrites;

                        // Get the db overwrites
                        var dbOverwrites = await Program.redis.HashGetAllAsync("overrides");

                        // Compare the two and sync them, prioritizing overwrites on channel over stored overwrites

                        foreach (var userOverwrites in dbOverwrites)
                        {
                            var overwriteDict =
                                JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(userOverwrites
                                    .Value);

                            // If the db overwrites are not in the current channel overwrites, remove them from the db.

                            foreach (var overwrite in overwriteDict)
                            {
                                // (if overwrite is for a different channel, skip)
                                if (overwrite.Key != e.Channel.Id) continue;

                                // (if current overwrite is in the channel, skip)
                                // checking individual properties here because sometimes they are the same but the one from Discord has
                                // other properties like Discord (DiscordClient) that I don't care about and will wrongly mark the overwrite as different
                                if (currentChannelOverwrites.Any(a => CompareOverwrites(a, overwrite.Value)))
                                    continue;

                                // If it looks like the member left, do NOT remove their overrides.

                                // Try to fetch member. If it fails, they are not in the guild. If this is a voice channel, remove the override.
                                // (if they are not in the guild & this is not a voice channel, skip; otherwise, code below handles removal)
                                bool isMemberInServer = await IsMemberInServer((ulong)userOverwrites.Name, e.Guild);
                                if (!isMemberInServer && e.Channel.Type != DiscordChannelType.Voice)
                                    continue;

                                // User could be fetched, so they are in the server and their override was removed. Remove from db.
                                // (or user could not be fetched & this is a voice channel; remove)

                                var overrides = await Program.redis.HashGetAsync("overrides", userOverwrites.Name);
                                var dict = JsonConvert
                                    .DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(overrides);
                                dict.Remove(e.Channel.Id);
                                if (dict.Count > 0)
                                    await Program.redis.HashSetAsync("overrides", userOverwrites.Name,
                                        JsonConvert.SerializeObject(dict));
                                else
                                {
                                    await Program.redis.HashDeleteAsync("overrides", userOverwrites.Name);
                                }
                            }
                        }

                        foreach (var overwrite in currentChannelOverwrites)
                        {
                            // Ignore role overrides because we aren't storing those
                            if (overwrite.Type == DiscordOverwriteType.Role) continue;

                            // If the current channel overwrites are not in the db, add them to the db.

                            // Pull out db overwrites into list

                            var dbOverwriteRaw = await Program.redis.HashGetAllAsync("overrides");
                            var dbOverwriteList = new List<Dictionary<ulong, DiscordOverwrite>>();

                            foreach (var dbOverwrite in dbOverwriteRaw)
                            {
                                var dict = JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(dbOverwrite.Value);
                                dbOverwriteList.Add(dict);
                            }

                            // If the overwrite is already in the db for this channel, skip
                            if (dbOverwriteList.Any(dbOverwriteSet => dbOverwriteSet.ContainsKey(e.Channel.Id) && CompareOverwrites(dbOverwriteSet[e.Channel.Id], overwrite)))
                                continue;

                            if ((await Program.redis.HashKeysAsync("overrides")).Any(a => a == overwrite.Id.ToString()))
                            {
                                // User has an overwrite in the db; add this one to their list of overrides without
                                // touching existing ones

                                var overwrites = await Program.redis.HashGetAsync("overrides", overwrite.Id);

                                if (!string.IsNullOrWhiteSpace(overwrites))
                                {
                                    var dict =
                                        JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(overwrites);

                                    if (dict is not null)
                                    {
                                        dict.Add(e.Channel.Id, overwrite);

                                        if (dict.Count > 0)
                                            await Program.redis.HashSetAsync("overrides", overwrite.Id,
                                                JsonConvert.SerializeObject(dict));
                                        else
                                            await Program.redis.HashDeleteAsync("overrides", overwrite.Id);
                                    }
                                }
                            }
                            else
                            {
                                // User doesn't have any overrides in db, so store new dictionary

                                await Program.redis.HashSetAsync("overrides",
                                    overwrite.Id, JsonConvert.SerializeObject(new Dictionary<ulong, DiscordOverwrite>
                                        { { e.Channel.Id, overwrite } }));
                            }
                        }

                        PendingChannelCreateEvents.Remove(timestamp);
                        success = true;
                    }
                    catch (InvalidOperationException ex)
                    {
                        Program.discord.Logger.LogDebug(ex, "Failed to enumerate channel overwrites for channel {channel}; this usually means the permissions were changed while processing a channel event. Will try again on next task run.", e.Channel.Id);
                    }
                    catch (Exception ex)
                    {
                        // Log the exception
                        Program.discord.Logger.LogWarning(ex,
                            "Failed to process pending channel create event for channel {channel}", e.Channel.Id);

                        // Always remove the event from the pending list, even if we failed to process it
                        PendingChannelCreateEvents.Remove(timestamp);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                Program.discord.Logger.LogDebug(ex, "Failed to enumerate pending channel create events; this usually means a Channel Create event was just added to the list, or one was processed and removed from the list. Will try again on next task run.");
            }

            Program.discord.Logger.LogDebug(Program.CliptokEventID, "Checked pending channel create events at {time} with result: {success}", DateTime.UtcNow, success);
            return success;
        }

        public static async Task<bool> HandlePendingChannelUpdateEventsAsync()
        {
            bool success = false;

            // populate CurrentBans list
            try
            {
                Dictionary<string, MemberPunishment> bans = (await Program.redis.HashGetAllAsync("bans")).ToDictionary(
                    x => x.Name.ToString(),
                    x => JsonConvert.DeserializeObject<MemberPunishment>(x.Value)
                );

                CurrentBans = bans.Values.ToList();

                LastBanListPopulationFailed = false;
            }
            catch (Exception ex)
            {
                if (!LastBanListPopulationFailed)
                    Program.discord.Logger.LogWarning(ex, "Failed to populate list of current bans during override persistence checks! This warning will be suppressed until the next success!");

                // Since this is likely caused by corrupt or otherwise unreadable data in the db, set a flag so that this warning is not spammed
                // The flag will be reset on the next successful attempt to populate the CurrentBans list
                LastBanListPopulationFailed = true;
            }

            try
            {
                foreach (var pendingEvent in PendingChannelUpdateEvents)
                {
                    // This is the timestamp on this event, used to identify it / keep events in order in the list
                    var timestamp = pendingEvent.Key;

                    // This is a set of ChannelUpdatedEventArgs for the event we are processing
                    var e = pendingEvent.Value;

                    try
                    {
                        // Sync channel overwrites with db so that they can be restored when a user leaves & rejoins.

                        // Get the current channel overwrites
                        var currentChannelOverwrites = e.ChannelAfter.PermissionOverwrites;

                        // Get the db overwrites
                        var dbOverwrites = await Program.redis.HashGetAllAsync("overrides");

                        // Compare the two and sync them, prioritizing overwrites on channel over stored overwrites

                        foreach (var userOverwrites in dbOverwrites)
                        {
                            var overwriteDict =
                                JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(userOverwrites
                                    .Value);

                            // If the db overwrites are not in the current channel overwrites, remove them from the db.

                            foreach (var overwrite in overwriteDict)
                            {
                                // (if overwrite is for a different channel, skip)
                                if (overwrite.Key != e.ChannelAfter.Id) continue;

                                // (if current overwrite is in the channel, skip)
                                // checking individual properties here because sometimes they are the same but the one from Discord has
                                // other properties like Discord (DiscordClient) that I don't care about and will wrongly mark the overwrite as different
                                if (currentChannelOverwrites.Any(a => CompareOverwrites(a, overwrite.Value)))
                                    continue;

                                // If it looks like the member left, do NOT remove their overrides.

                                // Try to fetch member. If it fails, they are not in the guild. If this is a voice channel, remove the override.
                                // (if they are not in the guild & this is not a voice channel, skip; otherwise, code below handles removal)
                                bool isMemberInServer = await IsMemberInServer((ulong)userOverwrites.Name, e.Guild);
                                if (!isMemberInServer && e.ChannelAfter.Type != DiscordChannelType.Voice)
                                    continue;

                                // User could be fetched, so they are in the server and their override was removed. Remove from db.
                                // (or user could not be fetched & this is a voice channel; remove)

                                var overrides = await Program.redis.HashGetAsync("overrides", userOverwrites.Name);
                                var dict = JsonConvert
                                    .DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(overrides);
                                dict.Remove(e.ChannelAfter.Id);
                                if (dict.Count > 0)
                                    await Program.redis.HashSetAsync("overrides", userOverwrites.Name,
                                        JsonConvert.SerializeObject(dict));
                                else
                                {
                                    await Program.redis.HashDeleteAsync("overrides", userOverwrites.Name);
                                }
                            }
                        }

                        foreach (var overwrite in currentChannelOverwrites)
                        {
                            // Ignore role overrides because we aren't storing those
                            if (overwrite.Type == DiscordOverwriteType.Role) continue;

                            // If the current channel overwrites are not in the db, add them to the db.

                            // Pull out db overwrites into list

                            var dbOverwriteRaw = await Program.redis.HashGetAllAsync("overrides");
                            var dbOverwriteList = new List<Dictionary<ulong, DiscordOverwrite>>();

                            foreach (var dbOverwrite in dbOverwriteRaw)
                            {
                                var dict = JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(dbOverwrite.Value);
                                dbOverwriteList.Add(dict);
                            }

                            // If the overwrite is already in the db for this channel, skip
                            if (dbOverwriteList.Any(dbOverwriteSet => dbOverwriteSet.ContainsKey(e.ChannelAfter.Id) && CompareOverwrites(dbOverwriteSet[e.ChannelAfter.Id], overwrite)))
                                continue;

                            if ((await Program.redis.HashKeysAsync("overrides")).Any(a => a == overwrite.Id.ToString()))
                            {
                                // User has an overwrite in the db; add this one to their list of overrides without
                                // touching existing ones

                                var overwrites = await Program.redis.HashGetAsync("overrides", overwrite.Id);

                                if (!string.IsNullOrWhiteSpace(overwrites))
                                {
                                    var dict =
                                        JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(overwrites);

                                    if (dict is not null)
                                    {
                                        dict.Add(e.ChannelAfter.Id, overwrite);

                                        if (dict.Count > 0)
                                            await Program.redis.HashSetAsync("overrides", overwrite.Id,
                                                JsonConvert.SerializeObject(dict));
                                        else
                                            await Program.redis.HashDeleteAsync("overrides", overwrite.Id);
                                    }
                                }
                            }
                            else
                            {
                                // User doesn't have any overrides in db, so store new dictionary

                                await Program.redis.HashSetAsync("overrides",
                                    overwrite.Id, JsonConvert.SerializeObject(new Dictionary<ulong, DiscordOverwrite>
                                        { { e.ChannelAfter.Id, overwrite } }));
                            }
                        }

                        PendingChannelUpdateEvents.Remove(timestamp);
                        success = true;
                    }
                    catch (InvalidOperationException ex)
                    {
                        Program.discord.Logger.LogDebug(ex, "Failed to enumerate channel overwrites for channel {channel}; this usually means the permissions were changed while processing a channel event. Will try again on next task run.", e.ChannelAfter.Id);
                    }
                    catch (Exception ex)
                    {
                        // Log the exception
                        Program.discord.Logger.LogWarning(ex,
                            "Failed to process pending channel update event for channel {channel}", e.ChannelAfter.Id);

                        // Always remove the event from the pending list, even if we failed to process it
                        PendingChannelUpdateEvents.Remove(timestamp);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                Program.discord.Logger.LogDebug(ex, "Failed to enumerate pending channel update events; this usually means a Channel Update event was just added to the list, or one was processed and removed from the list. Will try again on next task run.");
            }

            Program.discord.Logger.LogDebug(Program.CliptokEventID, "Checked pending channel update events at {time} with result: {success}", DateTime.UtcNow, success);
            return success;
        }

        public static async Task<bool> HandlePendingChannelDeleteEventsAsync()
        {
            bool success = false;

            try
            {
                foreach (var pendingEvent in PendingChannelDeleteEvents)
                {
                    // This is the timestamp on this event, used to identify it / keep events in order in the list
                    var timestamp = pendingEvent.Key;

                    // This is a set of ChannelDeletedEventArgs for the event we are processing
                    var e = pendingEvent.Value;

                    try
                    {
                        // Purge all overwrites from db for this channel

                        // Get all overwrites
                        var dbOverwrites = await Program.redis.HashGetAllAsync("overrides");

                        // Overwrites are stored by user ID, then as a dict with channel ID as key & overwrite as value, so we can't just delete by channel ID.
                        // We need to loop through all overwrites and delete the ones that match the channel ID, then put everything back together.

                        foreach (var userOverwrites in dbOverwrites)
                        {
                            var overwriteDict = JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(userOverwrites.Value);

                            // Now overwriteDict is a dict of this user's overwrites, with channel ID as key & overwrite as value

                            // Loop through these; for any with a matching channel ID to the channel that was deleted, remove them
                            foreach (var overwrite in overwriteDict)
                            {
                                if (overwrite.Key == e.Channel.Id)
                                {
                                    overwriteDict.Remove(overwrite.Key);
                                }
                            }

                            // Now we have a modified overwriteDict (ulong, DiscordOverwrite)
                            // Now we put everything back together

                            // If the user now has no overrides, remove them from the db entirely
                            if (overwriteDict.Count == 0)
                            {
                                await Program.redis.HashDeleteAsync("overrides", userOverwrites.Name);
                            }
                            else
                            {
                                // Otherwise, update the user's overrides in the db
                                await Program.redis.HashSetAsync("overrides", userOverwrites.Name, JsonConvert.SerializeObject(overwriteDict));
                            }
                        }

                        PendingChannelDeleteEvents.Remove(timestamp);
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        // Log the exception
                        Program.discord.Logger.LogWarning(ex,
                            "Failed to process pending channel delete event for channel {channel}", e.Channel.Id);

                        // Always remove the event from the pending list, even if we failed to process it
                        PendingChannelDeleteEvents.Remove(timestamp);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                Program.discord.Logger.LogDebug(ex, "Failed to enumerate pending channel delete events; this usually means a Channel Delete event was just added to the list, or one was processed and removed from the list. Will try again on next task run.");
            }

            Program.discord.Logger.LogDebug(Program.CliptokEventID, "Checked pending channel delete events at {time} with result: {success}", DateTime.UtcNow, success);
            return success;
        }

        private static bool CompareOverwrites(DiscordOverwrite a, DiscordOverwrite b)
        {
            // Compares two overwrites. ONLY CHECKS PERMISSIONS, ID, TYPE AND CREATION TIME. Ignores other properties!

            return a.Allowed == b.Allowed && a.Denied == b.Denied && a.Id == b.Id && a.Type == b.Type && a.CreationTimestamp == b.CreationTimestamp;
        }

        private static async Task<bool> IsMemberInServer(ulong userId, DiscordGuild guild)
        {
            bool isMemberInServer = false;

            // Check cache first
            if (guild.Members.ContainsKey(userId))
                return true;

            // If the user isn't cached, maybe they are banned? Check before making any API calls.
            if (CurrentBans.Any(b => b.MemberId == userId))
                return false;

            // If the user isn't cached or banned, try fetching them to confirm
            try
            {
                await guild.GetMemberAsync(userId);
                isMemberInServer = true;
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // Member is not in the server
                // isMemberInServer is already false

                Program.discord.Logger.LogInformation(Program.CliptokEventID, "Failed to fetch member {userId} during override persistence checks. This and the accompanying 404 are expected if the user is not in the server.", userId);
            }

            return isMemberInServer;
        }
    }
}