namespace Cliptok.Helpers
{
    public class LockdownHelpers
    {
        public static async Task<bool> LockChannelAsync(DiscordUser user, DiscordChannel channel, TimeSpan? duration = null, string reason = "No reason specified.", bool lockThreads = false)
        {
            if (!Program.cfgjson.LockdownEnabledChannels.Contains(channel.Id))
            {
                return false;
            }

            DiscordOverwrite[] existingOverwrites = channel.PermissionOverwrites.ToArray();

            await channel.AddOverwriteAsync(channel.Guild.CurrentMember, Permissions.SendMessages, Permissions.None, "Failsafe 1 for Lockdown");
            await channel.AddOverwriteAsync(channel.Guild.GetRole(Program.cfgjson.ModRole), Permissions.SendMessages, Permissions.None, "Failsafe 2 for Lockdown");

            bool everyoneRoleChanged = false;
            foreach (DiscordOverwrite overwrite in existingOverwrites)
            {
                if (overwrite.Type == OverwriteType.Role)
                {
                    DiscordRole role = await overwrite.GetRoleAsync();

                    if (role == channel.Guild.EveryoneRole)
                    {
                        if (lockThreads)
                        {
                            if (overwrite.Denied.HasPermission(Permissions.AccessChannels))
                            {
                                await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages | Permissions.AccessChannels | Permissions.SendMessagesInThreads, $"[Lockdown by {user.Username}#{user.Discriminator}]: {reason}");
                            }
                            else
                            {
                                await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages | Permissions.SendMessagesInThreads, $"[Lockdown by {user.Username}#{user.Discriminator}]: {reason}");
                            }

                            if (overwrite.Allowed.HasPermission(Permissions.SendMessages))
                            {
                                await channel.AddOverwriteAsync(await overwrite.GetRoleAsync(), (Permissions)(overwrite.Allowed - Permissions.SendMessages), Permissions.SendMessages | overwrite.Denied, "Reinstating existing overrides for lockdown.");
                            }
                            else
                            {
                                await channel.AddOverwriteAsync(await overwrite.GetRoleAsync(), overwrite.Allowed, Permissions.SendMessages | overwrite.Denied, "Reinstating existing overrides for lockdown.");
                            }
                        }
                        else
                        {
                            if (overwrite.Denied.HasPermission(Permissions.AccessChannels))
                            {
                                await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages | Permissions.AccessChannels, $"[Lockdown by {user.Username}#{user.Discriminator}]: {reason}");
                            }
                            else
                            {
                                await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages, $"[Lockdown by {user.Username}#{user.Discriminator}]: {reason}");
                            }

                            if (overwrite.Allowed.HasPermission(Permissions.SendMessages))
                            {
                                await channel.AddOverwriteAsync(await overwrite.GetRoleAsync(), (Permissions)(overwrite.Allowed - Permissions.SendMessages), Permissions.SendMessages | overwrite.Denied, "Reinstating existing overrides for lockdown.");
                            }
                            else
                            {
                                await channel.AddOverwriteAsync(await overwrite.GetRoleAsync(), overwrite.Allowed, Permissions.SendMessages | overwrite.Denied, "Reinstating existing overrides for lockdown.");
                            }
                        }

                        everyoneRoleChanged = true;
                    }
                    else
                    {
                        if (role == channel.Guild.GetRole(Program.cfgjson.ModRole))
                        {
                            await channel.AddOverwriteAsync(channel.Guild.GetRole(Program.cfgjson.ModRole), overwrite.Allowed | Permissions.SendMessages, Permissions.None, "Reinstating existing overrides for lockdown.");
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
                else
                {
                    await channel.AddOverwriteAsync(await overwrite.GetMemberAsync(), overwrite.Allowed, overwrite.Denied);
                }
            }

            if (!everyoneRoleChanged)
            {
                if (lockThreads)
                {
                    await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages | Permissions.SendMessagesInThreads, $"[Lockdown by {user.Username}#{user.Discriminator}]: {reason}");
                }
                else
                {
                    await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages, $"[Lockdown by {user.Username}#{user.Discriminator}]: {reason}");
                }
            }

            string msg;
            if (reason == "" || reason == "No reason specified.")
                msg = $"{Program.cfgjson.Emoji.Locked} This channel has been locked by a Moderator.";
            else
                msg = $"{Program.cfgjson.Emoji.Locked} This channel has been locked: **{reason}**";

            if (duration != null)
            {
                await Program.db.HashSetAsync("unlocks", channel.Id, TimeHelpers.ToUnixTimestamp(DateTime.Now + duration));
                msg += $"\nChannel unlocks: <t:{TimeHelpers.ToUnixTimestamp(DateTime.Now + duration)}:R>";
            }

            await channel.SendMessageAsync(msg);
            return true;
        }

        public static async Task<bool> UnlockChannel(DiscordChannel discordChannel, DiscordMember discordMember, string reason = "No reason specified.", bool isMassUnlock = false)
        {
            bool success = false;
            var permissions = discordChannel.PermissionOverwrites.ToArray();
            foreach (var permission in permissions)
            {
                if (permission.Type == OverwriteType.Role)
                {
                    DiscordRole role = await permission.GetRoleAsync();

                    DiscordOverwriteBuilder newOverwrite;
                    if (
                        (role == discordChannel.Guild.EveryoneRole
                        && permission.Denied.HasPermission(Permissions.SendMessages))
                        )
                    {
                        if (permission.Denied.HasPermission(Permissions.SendMessagesInThreads))
                        {
                            newOverwrite = new(discordChannel.Guild.EveryoneRole)
                            {
                                Allowed = permission.Allowed,
                                Denied = permission.Denied - Permissions.SendMessages - Permissions.SendMessagesInThreads
                            };
                        }
                        else
                        {
                            newOverwrite = new(discordChannel.Guild.EveryoneRole)
                            {
                                Allowed = permission.Allowed,
                                Denied = (Permissions)(permission.Denied - Permissions.SendMessages)
                            };
                        }

                        success = true;
                        if (discordMember.Id == Program.discord.CurrentUser.Id)
                            await discordChannel.AddOverwriteAsync(discordChannel.Guild.EveryoneRole, newOverwrite.Allowed, newOverwrite.Denied, "Lockdown has naturally expired.");
                        else
                            await discordChannel.AddOverwriteAsync(discordChannel.Guild.EveryoneRole, newOverwrite.Allowed, newOverwrite.Denied, $"[Unlock by {discordMember.Username}#{discordMember.Discriminator}]: {reason}");
                    }

                    if (role == discordChannel.Guild.GetRole(Program.cfgjson.ModRole)
                        && permission.Allowed == Permissions.SendMessages)
                    {
                        await permission.DeleteAsync();
                    }

                    if (role == discordChannel.Guild.GetRole(Program.cfgjson.ModRole)
                        && permission.Allowed == (Permissions.SendMessages | Permissions.AccessChannels))
                    {
                        await discordChannel.AddOverwriteAsync(discordChannel.Guild.GetRole(Program.cfgjson.ModRole), (Permissions)(permission.Allowed - Permissions.SendMessages), Permissions.None);
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
                if (!isMassUnlock)
                {
                    // this is just going to loop forever if we don't remove the entry 
                    await Program.db.HashDeleteAsync("unlocks", discordChannel.Id);
                    await discordChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} This channel is not locked, or unlock failed.");
                }
            }
            return success;
        }

    }
}
