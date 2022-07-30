namespace Cliptok.Events
{
    public class VoiceEvents
    {

        private static List<ulong> PendingOverWrites = new();
        private static List<ulong> PendingPurge = new();

        public static async Task VoiceStateUpdate(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            Task.Run(async () =>
            {
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

                if (e.Before is not null && e.Before.Channel.Users.Count == 0)
                {
                    client.Logger.LogDebug("{channel} is now empty!", e.Before.Channel.Name);

                    Task.Run(async () =>
                    {

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
                            var firstMsg = (await e.Before.Channel.GetMessagesAsync(1)).First();
                            messages.Add(firstMsg);
                            var lastMsgId = firstMsg.Id;
                            // delete all the messages from the channel
                            while (true)
                            {
                                var newmsgs = (await e.Before.Channel.GetMessagesBeforeAsync(lastMsgId, 100)).ToList();
                                messages.AddRange(newmsgs);
                                lastMsgId = newmsgs.Last().Id;
                                if (newmsgs.Count < 100)
                                {
                                    break;
                                }
                            }
                            messages.RemoveAll(message => message.CreationTimestamp.ToUniversalTime() < DateTime.UtcNow.AddDays(-14));
                            PendingPurge.Remove(e.Before.Channel.Id);

                            string messageLog = await DiscordHelpers.CompileMessagesAsync(messages.AsEnumerable().Reverse().ToList(), e.Before.Channel);

                            var stream = new MemoryStream(Encoding.UTF8.GetBytes(messageLog));
                            var msg = new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Deleted} Automatically purged **{messages.Count}** messages from {e.Before.Channel.Mention}.").WithFile("messages.txt", stream);

                            var hasteResult = await Program.hasteUploader.Post(messageLog);

                            if (hasteResult.IsSuccess)
                            {
                                msg.WithEmbed(new DiscordEmbedBuilder().WithDescription($"[`📄 View online`]({Program.cfgjson.HastebinEndpoint}/raw/{hasteResult.Key})"));
                            }

                            LogChannelHelper.LogMessageAsync("messages", msg);

                            await e.Before.Channel.DeleteMessagesAsync(messages);
                        }
                    });
                }
            });
        }

        public static async Task UserJoined(DiscordClient _, VoiceStateUpdateEventArgs e)
        {

            while (PendingOverWrites.Contains(e.User.Id))
            {
                Console.WriteLine("spinning");
                await Task.Delay(5);
            }

            PendingOverWrites.Add(e.User.Id);

            DiscordOverwrite[] existingOverwrites = e.After.Channel.PermissionOverwrites.ToArray();

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

            PendingOverWrites.Remove(e.User.Id);

            await e.After.Channel.SendMessageAsync($"{e.After.Member.Mention} has joined.");
        }

        public static async Task UserLeft(DiscordClient _, VoiceStateUpdateEventArgs e)
        {
            DiscordMember member = e.After.Member;

            while (PendingOverWrites.Contains(e.User.Id)) ;
            {
                Console.WriteLine("spinning");
                await Task.Delay(5);
            }

            PendingOverWrites.Add(e.User.Id);

            DiscordOverwrite[] existingOverwrites = e.Before.Channel.PermissionOverwrites.ToArray();

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

            PendingOverWrites.Remove(e.User.Id);

            DiscordMessageBuilder message = new()
            {
                Content = $"{member.Mention} has left."
            };
            await e.Before.Channel.SendMessageAsync(message.WithAllowedMentions(Mentions.None));
        }
    }
}
