namespace Cliptok.Helpers
{
    public class LockdownHelpers
    {
        public static async Task LockChannelAsync(DiscordChannel channel, TimeSpan? duration = null, string reason = "")
        {
            if (!Program.cfgjson.LockdownEnabledChannels.Contains(channel.Id))
            {
                return;
            }

            DiscordOverwrite[] existingOverwrites = channel.PermissionOverwrites.ToArray();

            await channel.AddOverwriteAsync(channel.Guild.CurrentMember, Permissions.SendMessages, Permissions.None, "Failsafe 1 for Lockdown");
            await channel.AddOverwriteAsync(channel.Guild.GetRole(Program.cfgjson.ModRole), Permissions.SendMessages, Permissions.None, "Failsafe 2 for Lockdown");

            foreach (DiscordOverwrite overwrite in existingOverwrites)
            {
                if (overwrite.Type == OverwriteType.Role)
                {
                    if (await overwrite.GetRoleAsync() == channel.Guild.EveryoneRole)
                    {
                        if (overwrite.Denied.HasPermission(Permissions.AccessChannels))
                        {
                            await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages | Permissions.AccessChannels, "Lockdown command");
                        }
                        else
                        {
                            await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages, "Lockdown command");
                        }

                        if (overwrite.Allowed.HasPermission(Permissions.SendMessages))
                        {
                            await channel.AddOverwriteAsync(await overwrite.GetRoleAsync(), (Permissions)(overwrite.Allowed - Permissions.SendMessages), Permissions.SendMessages | overwrite.Denied);
                        }
                        else
                        {
                            await channel.AddOverwriteAsync(await overwrite.GetRoleAsync(), overwrite.Allowed, Permissions.SendMessages | overwrite.Denied);
                        }
                    }
                    else
                    {
                        await channel.AddOverwriteAsync(await overwrite.GetRoleAsync(), overwrite.Allowed, overwrite.Denied);

                    }
                }
                else
                {
                    await channel.AddOverwriteAsync(await overwrite.GetMemberAsync(), overwrite.Allowed, overwrite.Denied);
                }
            }

            string msg;
            if (reason == "")
                msg = $"{Program.cfgjson.Emoji.Locked} This channel has been locked by a Moderator.";
            else
                msg = $"{Program.cfgjson.Emoji.Locked} This channel has been locked: **{reason}**";

            if (duration != null)
            {
                await Program.db.HashSetAsync("unlocks", channel.Id, TimeHelpers.ToUnixTimestamp(DateTime.Now + duration));
                msg += $"\nChannel unlocks: <t:{TimeHelpers.ToUnixTimestamp(DateTime.Now + duration)}:R>";
            }

            await channel.SendMessageAsync(msg);
        }

        public static async Task<bool> UnlockChannel(DiscordChannel discordChannel, DiscordMember discordMember)
        {
            bool success = false;
            var permissions = discordChannel.PermissionOverwrites.ToArray();
            foreach (var permission in permissions)
            {
                if (permission.Type == OverwriteType.Role)
                {
                    DiscordOverwriteBuilder newOverwrite;
                    if (
                        (await permission.GetRoleAsync() == discordChannel.Guild.EveryoneRole
                        && permission.Denied.HasPermission(Permissions.SendMessages))
                        )
                    {
                        if (permission.Denied.HasPermission(Permissions.SendMessages))
                        {
                            newOverwrite = new(discordChannel.Guild.EveryoneRole)
                            {
                                Allowed = permission.Allowed,
                                Denied = (Permissions)(permission.Denied - Permissions.SendMessages)
                            };
                        }
                        else
                        {
                            newOverwrite = new(discordChannel.Guild.EveryoneRole)
                            {
                                Allowed = permission.Allowed,
                                Denied = permission.Denied,
                            };
                        }

                        success = true;
                        await discordChannel.AddOverwriteAsync(discordChannel.Guild.EveryoneRole, newOverwrite.Allowed, newOverwrite.Denied);
                    }

                    if (await permission.GetRoleAsync() == discordChannel.Guild.GetRole(Program.cfgjson.ModRole)
                        && permission.Allowed == Permissions.SendMessages)
                    {
                        await permission.DeleteAsync();
                    }

                }
                else
                {
                    var member = await permission.GetMemberAsync();
                    if ((member == discordMember || member == discordChannel.Guild.CurrentMember) && permission.Allowed == Permissions.SendMessages)
                    {
                        success = true;
                        await permission.DeleteAsync();
                    }

                }
            }
            if (success)
            {
                await Program.db.HashDeleteAsync("unlocks", discordChannel.Id);
                await discordChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Unlock} This channel has been unlocked!");
            }
            else
            {
                await discordChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} This channel is not locked, or unlock failed.");
            }
            return success;
        }

    }
}
