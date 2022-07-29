namespace Cliptok.Events
{
    public class VoiceEvents
    {
        public static async Task VoiceStateUpdate(DiscordClient client, VoiceStateUpdateEventArgs e)
        {        
            if (e.After.Channel is null)
            {
                client.Logger.LogDebug($"{e.User.Username} left {e.Before.Channel.Name}");

                await UserLeft(client, e);
            }
            else if (e.Before is null)
            {
                client.Logger.LogDebug($"{e.User.Username} joined {e.After.Channel.Name}");

                await UserJoined(client, e);
            } 
            else if (e.Before.Channel.Id != e.After.Channel.Id)
            {
                client.Logger.LogDebug($"{e.User.Username} moved from {e.Before.Channel.Name} to {e.After.Channel.Name}");

                await UserLeft(client, e);
                await UserJoined(client, e);
            }

            if (e.Before is not null && e.Before.Channel.Users.Count == 0)
            {
                client.Logger.LogDebug($"{e.Before.Channel.Name} is now empty!");

                // todo: purge message history, on delay
            }

        }

        public static async Task UserJoined(DiscordClient _, VoiceStateUpdateEventArgs e)
        {
            Task.Run(async () =>
            {
                DiscordOverwrite[] existingOverwrites = e.After.Channel.PermissionOverwrites.ToArray();
                DiscordMember member = await e.Guild.GetMemberAsync(e.User.Id);

                if (!member.Roles.Any(role => role.Id == Program.cfgjson.MutedRole))
                {
                    bool userOverrideSet = false;
                    foreach (DiscordOverwrite overwrite in existingOverwrites)
                    {
                        if (overwrite.Type == OverwriteType.Member && overwrite.Id == member.Id)
                        {
                            await e.After.Channel.AddOverwriteAsync(member, overwrite.Allowed | Permissions.SendMessages, overwrite.Denied, "User joined voice channel.");
                            userOverrideSet = true;
                            break;
                        }
                    }

                    if (!userOverrideSet)
                    {
                        await e.After.Channel.AddOverwriteAsync(member, Permissions.SendMessages, Permissions.None, "User joined voice channel.");
                    }
                }

                DiscordMessageBuilder message = new()
                {
                    Content = $"{member.Mention} has joined."
                };
                await e.After.Channel.SendMessageAsync(message.WithAllowedMentions(Mentions.None));
            });
        }

        public static async Task UserLeft(DiscordClient _, VoiceStateUpdateEventArgs e)
        {
            Task.Run(async () =>
            {
                DiscordOverwrite[] existingOverwrites = e.Before.Channel.PermissionOverwrites.ToArray();
                DiscordMember member = await e.Guild.GetMemberAsync(e.User.Id);

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

                DiscordMessageBuilder message = new()
                {
                    Content = $"{member.Mention} has left."
                };
                await e.Before.Channel.SendMessageAsync(message.WithAllowedMentions(Mentions.None));
            });
        }
    }
}
