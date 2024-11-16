﻿namespace Cliptok.Helpers
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

            await channel.AddOverwriteAsync(channel.Guild.CurrentMember, DiscordPermission.SendMessages, DiscordPermissions.None, "Failsafe 1 for Lockdown");
            await channel.AddOverwriteAsync(await channel.Guild.GetRoleAsync(Program.cfgjson.ModRole), DiscordPermission.SendMessages, DiscordPermissions.None, "Failsafe 2 for Lockdown");

            bool everyoneRoleChanged = false;
            foreach (DiscordOverwrite overwrite in existingOverwrites)
            {
                if (overwrite.Type == DiscordOverwriteType.Role)
                {
                    DiscordRole role = await overwrite.GetRoleAsync();

                    if (role == channel.Guild.EveryoneRole)
                    {
                        if (lockThreads)
                        {
                            if (overwrite.Denied.HasPermission(DiscordPermission.ViewChannel))
                            {
                                await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, DiscordPermissions.None, new([DiscordPermission.SendMessages, DiscordPermission.ViewChannel, DiscordPermission.SendThreadMessages]), $"[Lockdown by {DiscordHelpers.UniqueUsername(user)}]: {reason}");
                            }
                            else
                            {
                                await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, DiscordPermissions.None, new([DiscordPermission.SendMessages, DiscordPermission.SendThreadMessages]), $"[Lockdown by {DiscordHelpers.UniqueUsername(user)}]: {reason}");
                            }

                            if (overwrite.Allowed.HasPermission(DiscordPermission.SendMessages))
                            {
                                await channel.AddOverwriteAsync(await overwrite.GetRoleAsync(), (DiscordPermissions)(overwrite.Allowed - DiscordPermission.SendMessages), DiscordPermission.SendMessages | overwrite.Denied, "Reinstating existing overrides for lockdown.");
                            }
                            else
                            {
                                await channel.AddOverwriteAsync(await overwrite.GetRoleAsync(), overwrite.Allowed, DiscordPermission.SendMessages | overwrite.Denied, "Reinstating existing overrides for lockdown.");
                            }
                        }
                        else
                        {
                            if (overwrite.Denied.HasPermission(DiscordPermission.ViewChannel))
                            {
                                await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, DiscordPermissions.None, new([DiscordPermission.SendMessages, DiscordPermission.ViewChannel]), $"[Lockdown by {DiscordHelpers.UniqueUsername(user)}]: {reason}");
                            }
                            else
                            {
                                await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, DiscordPermissions.None, DiscordPermission.SendMessages, $"[Lockdown by {DiscordHelpers.UniqueUsername(user)}]: {reason}");
                            }

                            if (overwrite.Allowed.HasPermission(DiscordPermission.SendMessages))
                            {
                                await channel.AddOverwriteAsync(await overwrite.GetRoleAsync(), (DiscordPermissions)(overwrite.Allowed - DiscordPermission.SendMessages), DiscordPermission.SendMessages | overwrite.Denied, "Reinstating existing overrides for lockdown.");
                            }
                            else
                            {
                                await channel.AddOverwriteAsync(await overwrite.GetRoleAsync(), overwrite.Allowed, DiscordPermission.SendMessages | overwrite.Denied, "Reinstating existing overrides for lockdown.");
                            }
                        }

                        everyoneRoleChanged = true;
                    }
                    else
                    {
                        if (role == await channel.Guild.GetRoleAsync(Program.cfgjson.ModRole))
                        {
                            await channel.AddOverwriteAsync(await channel.Guild.GetRoleAsync(Program.cfgjson.ModRole), overwrite.Allowed.Add(DiscordPermission.SendMessages), DiscordPermissions.None, "Reinstating existing overrides for lockdown.");
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
                    await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, DiscordPermissions.None, new([DiscordPermission.SendMessages, DiscordPermission.SendThreadMessages]), $"[Lockdown by {DiscordHelpers.UniqueUsername(user)}]: {reason}");
                }
                else
                {
                    await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, DiscordPermissions.None, DiscordPermission.SendMessages, $"[Lockdown by {DiscordHelpers.UniqueUsername(user)}]: {reason}");
                }
            }

            string msg;
            if (reason == "" || reason == "No reason specified.")
                msg = $"{Program.cfgjson.Emoji.Locked} This channel has been locked by a Moderator.";
            else
                msg = $"{Program.cfgjson.Emoji.Locked} This channel has been locked: **{reason}**";

            if (duration is not null)
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
                if (permission.Type == DiscordOverwriteType.Role)
                {
                    DiscordRole role = await permission.GetRoleAsync();

                    DiscordOverwriteBuilder newOverwrite;
                    if (
                        (role == discordChannel.Guild.EveryoneRole
                        && permission.Denied.HasPermission(DiscordPermission.SendMessages))
                        )
                    {
                        if (permission.Denied.HasPermission(DiscordPermission.SendThreadMessages))
                        {
                            newOverwrite = new(discordChannel.Guild.EveryoneRole)
                            {
                                Allowed = permission.Allowed,
                                Denied = permission.Denied - DiscordPermission.SendMessages - DiscordPermission.SendThreadMessages
                            };
                        }
                        else
                        {
                            newOverwrite = new(discordChannel.Guild.EveryoneRole)
                            {
                                Allowed = permission.Allowed,
                                Denied = (DiscordPermissions)(permission.Denied - DiscordPermission.SendMessages)
                            };
                        }

                        success = true;
                        if (discordMember.Id == Program.discord.CurrentUser.Id)
                            await discordChannel.AddOverwriteAsync(discordChannel.Guild.EveryoneRole, newOverwrite.Allowed, newOverwrite.Denied, "Lockdown has naturally expired.");
                        else
                            await discordChannel.AddOverwriteAsync(discordChannel.Guild.EveryoneRole, newOverwrite.Allowed, newOverwrite.Denied, $"[Unlock by {DiscordHelpers.UniqueUsername(discordMember)}]: {reason}");
                    }

                    if (role == await discordChannel.Guild.GetRoleAsync(Program.cfgjson.ModRole)
                        && permission.Allowed == DiscordPermission.SendMessages)
                    {
                        await permission.DeleteAsync();
                    }

                    if (role == await discordChannel.Guild.GetRoleAsync(Program.cfgjson.ModRole)
                        && permission.Allowed == new DiscordPermissions([DiscordPermission.SendMessages, DiscordPermission.ViewChannel]))
                    {
                        await discordChannel.AddOverwriteAsync(await discordChannel.Guild.GetRoleAsync(Program.cfgjson.ModRole), (DiscordPermissions)(permission.Allowed - DiscordPermission.SendMessages), DiscordPermissions.None);
                    }

                }
                else
                {
                    var member = await permission.GetMemberAsync();
                    if ((member == discordMember || member == discordChannel.Guild.CurrentMember) && permission.Allowed == DiscordPermission.SendMessages)
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
