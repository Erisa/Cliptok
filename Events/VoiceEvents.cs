namespace Cliptok.Events
{
    public class VoiceEvents
    {

        private static List<ulong> PendingOverWrites = new();
        private static List<ulong> PendingPurge = new();

        public static async Task VoiceStateUpdate(DiscordClient client, VoiceStateUpdatedEventArgs e)
        {
            if (!Program.cfgjson.EnableTextInVoice)
                return;

            client.Logger.LogDebug("Got a voice state update event");

            var channelBefore = e.Before is null ? null : await e.Before.GetChannelAsync();
            var channelAfter = e.After is null ? null : await e.After.GetChannelAsync();
            var user = await e.GetUserAsync();
            var guild = await e.GetGuildAsync();

            if (channelAfter is null)
            {
                client.Logger.LogDebug("{user} left {channel}", user.Username, channelBefore.Name);

                UserLeft(client, e);
            }
            else if (e.Before is null)
            {
                client.Logger.LogDebug("{user} joined {channel}", user.Username, channelAfter.Name);

                UserJoined(client, e);
            }
            else if (channelBefore.Id != channelAfter.Id)
            {
                client.Logger.LogDebug("{user} moved from {before} to {after}", user.Username, channelBefore.Name, channelAfter.Name);

                UserLeft(client, e);
                UserJoined(client, e);
            }

            if (e.Before is not null && channelBefore.Users.Count == 0 && !Program.cfgjson.IgnoredVoiceChannels.Contains(channelBefore.Id))
            {
                client.Logger.LogDebug("{channel} is now empty!", channelBefore.Name);


                if (PendingPurge.Contains(channelBefore.Id))
                    return;

                if (Program.cfgjson.VoiceChannelPurge)
                    PendingPurge.Add(channelBefore.Id);

                for (int i = 0; i <= 12; i++)
                {
                    if (guild.Channels[channelBefore.Id].Users.Count != 0)
                    {
                        PendingPurge.Remove(channelBefore.Id);
                        return;
                    }
                    else
                    {
                        await Task.Delay(10000);
                    }
                }

                if (guild.Channels[channelBefore.Id].Users.Count == 0 && Program.cfgjson.VoiceChannelPurge)
                {
                    List<DiscordMessage> messages = new();
                    try
                    {
                        var firstMsg = (await channelBefore.GetMessagesAsync(1).ToListAsync()).FirstOrDefault();
                        if (firstMsg == default)
                            return;

                        messages.Add(firstMsg);
                        var lastMsgId = firstMsg.Id;
                        // delete all the messages from the channel
                        while (true)
                        {
                            var newmsgs = await channelBefore.GetMessagesBeforeAsync(lastMsgId, 100).ToListAsync();
                            messages.AddRange(newmsgs);
                            if (newmsgs.Count() < 100)
                                break;
                            else
                                lastMsgId = newmsgs.Last().Id;
                        }
                        messages.RemoveAll(message => message.CreationTimestamp.ToUniversalTime() < DateTime.UtcNow.AddDays(-14));
                        PendingPurge.Remove(channelBefore.Id);

                        await channelBefore.DeleteMessagesAsync(messages);
                    }
                    catch (Exception ex)
                    {
                        PendingPurge.Remove(channelBefore.Id);
                        Program.discord.Logger.LogError(Program.CliptokEventID, ex, "Error occurred trying to purge messages from {channel}", channelBefore.Name);
                    }

                    // logging is now handled in the bulk delete event
                    if (!Program.cfgjson.EnablePersistentDb)
                    {
                        if (messages.Count == 0)
                            return;

                        messages.Reverse();

                        await LogChannelHelper.LogDeletedMessagesAsync(
                            "messages",
                            $"{Program.cfgjson.Emoji.Deleted} Automatically purged **{messages.Count}** messages from {channelBefore.Mention}.",
                            messages,
                            channelBefore
                        );
                    }
                }
            }
        }

        public static async Task UserJoined(DiscordClient client, VoiceStateUpdatedEventArgs e)
        {
            var channelAfter = e.After is null ? null : await e.After.GetChannelAsync();
            var user = await e.GetUserAsync();
            var guild = await e.GetGuildAsync();
            var member = await guild.GetMemberAsync(user.Id);

            if (Program.cfgjson.IgnoredVoiceChannels.Contains(channelAfter.Id))
                return;

            client.Logger.LogDebug("Processing user join voice event for {user} in {channel}", user.Username, channelAfter.Name);

            while (PendingOverWrites.Contains(user.Id))
            {
                await Task.Delay(5);
            }

            PendingOverWrites.Add(user.Id);

            DiscordOverwrite[] existingOverwrites = channelAfter.PermissionOverwrites.ToArray();

            try
            {
                if (!member.Roles.Any(role => role.Id == Program.cfgjson.MutedRole))
                {
                    bool userOverrideSet = false;
                    foreach (DiscordOverwrite overwrite in existingOverwrites)
                    {
                        if (overwrite.Type == DiscordOverwriteType.Member && overwrite.Id == member.Id)
                        {
                            client.Logger.LogDebug("{user} already has overwrite in {channel}, adding Send Messages permission", user.Username, channelAfter.Name);
                            await channelAfter.AddOverwriteAsync(member, overwrite.Allowed + DiscordPermission.SendMessages, overwrite.Denied, "User joined voice channel.");
                            userOverrideSet = true;
                            break;
                        }
                    }

                    if (!userOverrideSet)
                    {
                        client.Logger.LogDebug("Creating overwrite for {user} in {channel}", user.Username, channelAfter.Name);
                        await channelAfter.AddOverwriteAsync(member, DiscordPermission.SendMessages, DiscordPermissions.None, "User joined voice channel.");
                    }
                }
            }
            catch (Exception ex)
            {
                PendingOverWrites.Remove(user.Id);
                Program.discord.Logger.LogError(Program.CliptokEventID, ex, "Error ocurred trying to add voice overwrites for {user} in {channel}", user.Username, channelAfter.Name);
            }

            PendingOverWrites.Remove(user.Id);

            try
            {
                await channelAfter.SendMessageAsync($"{member.Mention} has joined.");
            }
            catch (Exception ex)
            {
                Program.discord.Logger.LogError(Program.CliptokEventID, ex, "Error ocurred trying to send join message for {user} in {channel}", user.Username, channelAfter.Name);
            }

            client.Logger.LogDebug("Done processing user join voice event for {user} in {channel}", user.Username, channelAfter.Name);
        }

        public static async Task UserLeft(DiscordClient client, VoiceStateUpdatedEventArgs e)
        {
            var channelBefore = e.Before is null ? null : await e.Before.GetChannelAsync();
            var user = await e.GetUserAsync();
            var guild = await e.GetGuildAsync();
            var member = await guild.GetMemberAsync(user.Id);

            if (Program.cfgjson.IgnoredVoiceChannels.Contains(channelBefore.Id))
                return;

            client.Logger.LogDebug("Processing user leave voice event for {user} in {channel}", user.Username, channelBefore.Name);

            while (PendingOverWrites.Contains(user.Id))
            {
                Program.discord.Logger.LogDebug("spinning");
                await Task.Delay(5);
            }

            PendingOverWrites.Add(user.Id);

            DiscordOverwrite[] existingOverwrites = channelBefore.PermissionOverwrites.ToArray();

            try
            {
                foreach (DiscordOverwrite overwrite in existingOverwrites)
                {
                    if (overwrite.Type == DiscordOverwriteType.Member && overwrite.Id == member.Id)
                    {
                        if (overwrite.Allowed == DiscordPermission.SendMessages && overwrite.Denied == DiscordPermissions.None)
                        {
                            // User only has allow for Send Messages, so we can delete the entire override
                            client.Logger.LogDebug("{user} has overwrite in {channel}, deleting", user.Username, channelBefore.Name);
                            await overwrite.DeleteAsync("User left voice channel.");
                        }
                        else
                        {
                            // User has other overrides set, so we should only remove the Send Messages override
                            if (overwrite.Allowed.HasPermission(DiscordPermission.SendMessages))
                            {
                                client.Logger.LogDebug("{user} has overwrite in {channel} with other permissions, removing Send Messages permission", user.Username, channelBefore.Name);
                                await channelBefore.AddOverwriteAsync(member, overwrite.Allowed - DiscordPermission.SendMessages, overwrite.Denied, "User left voice channel.");
                            }
                            else
                            {
                                // Check if the overwrite has no permissions set - if so, delete it to keep the list clean.
                                if (overwrite.Allowed == DiscordPermissions.None && overwrite.Denied == DiscordPermissions.None)
                                {
                                    client.Logger.LogDebug("{user} has overwrite in {channel} with no permissions set, deleting", user.Username, channelBefore.Name);
                                    await overwrite.DeleteAsync("User left voice channel.");
                                }
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                PendingOverWrites.Remove(user.Id);
                Program.discord.Logger.LogError(Program.CliptokEventID, ex, "Error occurred trying to remove voice overwrites for {user} in {channel}", user.Username, channelBefore.Name);
            }

            PendingOverWrites.Remove(user.Id);

            DiscordMessageBuilder message = new()
            {
                Content = $"{member.Mention} has left."
            };
            try
            {
                await channelBefore.SendMessageAsync(message.WithAllowedMentions(Mentions.None));
            }
            catch (Exception ex)
            {
                Program.discord.Logger.LogError(Program.CliptokEventID, ex, "Error ocurred trying to send leave message for {user} in {channel}", user.Username, channelBefore.Name);
            }

            client.Logger.LogDebug("Done processing user leave voice event for {user} in {channel}", user.Username, channelBefore.Name);
        }
    }
}
