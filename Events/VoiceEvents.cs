namespace Cliptok.Events
{
    public class VoiceEvents
    {

        private static List<ulong> PendingOverWrites = new();
        private static List<ulong> PendingPurge = new();

        public static async Task VoiceStateUpdate(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            if (!Program.cfgjson.EnableTextInVoice)
                return;

            if (e.After.Channel is null)
            {
                client.Logger.LogDebug("{user} left {channel}", e.User.Username, e.Before.Channel.Name);

                UserLeft(client, e);
            }
            else if (e.Before is null)
            {
                client.Logger.LogDebug("{user} joined {channel}", e.User.Username, e.After.Channel.Name);

                UserJoined(client, e);
            }
            else if (e.Before.Channel.Id != e.After.Channel.Id)
            {
                client.Logger.LogDebug("{user} moved from {before} to {after}", e.User.Username, e.Before.Channel.Name, e.After.Channel.Name);

                UserLeft(client, e);
                UserJoined(client, e);
            }

            if (e.Before is not null && e.Before.Channel.Users.Count == 0 && !Program.cfgjson.IgnoredVoiceChannels.Contains(e.Before.Channel.Id))
            {
                client.Logger.LogDebug("{channel} is now empty!", e.Before.Channel.Name);


                if (PendingPurge.Contains(e.Before.Channel.Id))
                    return;

                PendingPurge.Add(e.Before.Channel.Id);

                for (int i = 0; i <= 12; i++)
                {
                    if (e.Guild.Channels[e.Before.Channel.Id].Users.Count != 0)
                    {
                        PendingPurge.Remove(e.Before.Channel.Id);
                        return;
                    }
                    else
                    {
                        await Task.Delay(10000);
                    }
                }

                if (e.Guild.Channels[e.Before.Channel.Id].Users.Count == 0)
                {
                    List<DiscordMessage> messages = new();
                    try
                    {
                        var firstMsg = (await e.Before.Channel.GetMessagesAsync(1).ToListAsync()).FirstOrDefault();
                        if (firstMsg == default)
                            return;

                        messages.Add(firstMsg);
                        var lastMsgId = firstMsg.Id;
                        // delete all the messages from the channel
                        while (true)
                        {
                            var newmsgs = await e.Before.Channel.GetMessagesBeforeAsync(lastMsgId, 100).ToListAsync();
                            messages.AddRange(newmsgs);
                            if (newmsgs.Count() < 100)
                                break;
                            else
                                lastMsgId = newmsgs.Last().Id;
                        }
                        messages.RemoveAll(message => message.CreationTimestamp.ToUniversalTime() < DateTime.UtcNow.AddDays(-14));
                        PendingPurge.Remove(e.Before.Channel.Id);

                        await e.Before.Channel.DeleteMessagesAsync(messages);
                    }
                    catch (Exception ex)
                    {
                        PendingPurge.Remove(e.Before.Channel.Id);
                        Program.discord.Logger.LogError(Program.CliptokEventID, ex, "Error ocurred trying to purge messages from {channel}", e.Before.Channel.Name);
                        return;
                    }

                    await LogChannelHelper.LogDeletedMessagesAsync(
                        "messages",
                        $"{Program.cfgjson.Emoji.Deleted} Automatically purged **{messages.Count}** messages from {e.Before.Channel.Mention}.",
                        messages,
                        e.Before.Channel
                    );

                }
            }

        }

        public static async Task UserJoined(DiscordClient _, VoiceStateUpdateEventArgs e)
        {

            if (Program.cfgjson.IgnoredVoiceChannels.Contains(e.After.Channel.Id))
                return;

            while (PendingOverWrites.Contains(e.User.Id))
            {
                await Task.Delay(5);
            }

            PendingOverWrites.Add(e.User.Id);

            DiscordOverwrite[] existingOverwrites = e.After.Channel.PermissionOverwrites.ToArray();

            try
            {
                if (!e.After.Member.Roles.Any(role => role.Id == Program.cfgjson.MutedRole))
                {
                    bool userOverrideSet = false;
                    foreach (DiscordOverwrite overwrite in existingOverwrites)
                    {
                        if (overwrite.Type == OverwriteType.Member && overwrite.Id == e.After.Member.Id)
                        {
                            await e.After.Channel.AddOverwriteAsync(e.After.Member, overwrite.Allowed | Permissions.SendMessages, overwrite.Denied, "User joined voice channel.");
                            userOverrideSet = true;
                            break;
                        }
                    }

                    if (!userOverrideSet)
                    {
                        await e.After.Channel.AddOverwriteAsync(e.After.Member, Permissions.SendMessages, Permissions.None, "User joined voice channel.");
                    }
                }
            }
            catch (Exception ex)
            {
                PendingOverWrites.Remove(e.User.Id);
                Program.discord.Logger.LogError(Program.CliptokEventID, ex, "Error ocurred trying to add voice overwrites for {user} in {channel}", e.User.Username, e.After.Channel.Name);
            }

            PendingOverWrites.Remove(e.User.Id);

            try
            {
                await e.After.Channel.SendMessageAsync($"{e.After.Member.Mention} has joined.");
            }
            catch (Exception ex)
            {
                Program.discord.Logger.LogError(Program.CliptokEventID, ex, "Error ocurred trying to send join message for {user} in {channel}", e.User.Username, e.After.Channel.Name);
            }
        }

        public static async Task UserLeft(DiscordClient _, VoiceStateUpdateEventArgs e)
        {
            if (Program.cfgjson.IgnoredVoiceChannels.Contains(e.Before.Channel.Id))
                return;

            DiscordMember member = e.After.Member;

            while (PendingOverWrites.Contains(e.User.Id))
            {
                await Task.Delay(5);
            }

            PendingOverWrites.Add(e.User.Id);

            DiscordOverwrite[] existingOverwrites = e.Before.Channel.PermissionOverwrites.ToArray();

            try
            {
                foreach (DiscordOverwrite overwrite in existingOverwrites)
                {
                    if (overwrite.Type == OverwriteType.Member && overwrite.Id == member.Id)
                    {
                        if (overwrite.Allowed == Permissions.SendMessages && overwrite.Denied == Permissions.None)
                        {
                            // User only has allow for Send Messages, so we can delete the entire override
                            await overwrite.DeleteAsync("User left voice channel.");
                        }
                        else
                        {
                            // User has other overrides set, so we should only remove the Send Messages override
                            if (overwrite.Allowed.HasPermission(Permissions.SendMessages))
                            {
                                await e.Before.Channel.AddOverwriteAsync(member, (Permissions)(overwrite.Allowed - Permissions.SendMessages), overwrite.Denied, "User left voice channel.");
                            }
                            else
                            {
                                // Check if the overwrite has no permissions set - if so, delete it to keep the list clean.
                                if (overwrite.Allowed == Permissions.None && overwrite.Denied == Permissions.None)
                                {
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
                PendingOverWrites.Remove(e.User.Id);
                Program.discord.Logger.LogError(Program.CliptokEventID, ex, "Error ocurred trying to remove voice overwrites for {user} in {channel}", e.User.Username, e.Before.Channel.Name);
            }

            PendingOverWrites.Remove(e.User.Id);

            DiscordMessageBuilder message = new()
            {
                Content = $"{member.Mention} has left."
            };
            try
            {
                await e.Before.Channel.SendMessageAsync(message.WithAllowedMentions(Mentions.None));
            }
            catch (Exception ex)
            {
                Program.discord.Logger.LogError(Program.CliptokEventID, ex, "Error ocurred trying to send leave message for {user} in {channel}", e.User.Username, e.Before.Channel.Name);
            }
        }
    }
}
