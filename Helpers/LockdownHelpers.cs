namespace Cliptok.Helpers
{
    public class LockdownHelpers
    {
        public static async Task LockChannelAsync(DiscordUser user, DiscordChannel channel, TimeSpan? duration = null, string reason = "No reason specified.", bool lockThreads = false)
        {
            if (!Program.cfgjson.LockdownEnabledChannels.Contains(channel.Id))
            {
                throw new ArgumentException($"Channel {channel.Id} is not in the lockdown whitelist.");
            }

            // Get the permissions that are already on the channel, so that we can make sure they are kept when we adjust overwrites for lockdown
            DiscordOverwrite[] existingOverwrites = channel.PermissionOverwrites.ToArray();

            // Get Cliptok's permission set from before the lockdown
            var cliptokOverwritesBeforeLockdown = existingOverwrites.Where(x => x.Id == Program.discord.CurrentUser.Id).FirstOrDefault();

            // Get Cliptok's allowed permission set
            var cliptokAllowedPermissionsBeforeLockdown = DiscordPermissions.None;
            if (cliptokOverwritesBeforeLockdown is not null)
                cliptokAllowedPermissionsBeforeLockdown = cliptokOverwritesBeforeLockdown.Allowed;

            // Get Cliptok's denied permission set
            var cliptokDeniedPermissionsBeforeLockdown = DiscordPermissions.None;
            if (cliptokOverwritesBeforeLockdown is not null)
                cliptokDeniedPermissionsBeforeLockdown = cliptokOverwritesBeforeLockdown.Denied;

            // Get the Moderator role's permission set from before the lockdown
            var moderatorOverwritesBeforeLockdown = existingOverwrites.Where(x => x.Id == Program.cfgjson.ModRole).FirstOrDefault();

            // Get the Moderator role's allowed permission set
            var moderatorAllowedPermissionsBeforeLockdown = DiscordPermissions.None;
            if (moderatorOverwritesBeforeLockdown is not null)
                moderatorAllowedPermissionsBeforeLockdown = moderatorOverwritesBeforeLockdown.Allowed;

            // Get the Moderator role's denied permission set
            var moderatorDeniedPermissionsBeforeLockdown = DiscordPermissions.None;
            if (moderatorOverwritesBeforeLockdown is not null)
                moderatorDeniedPermissionsBeforeLockdown = moderatorOverwritesBeforeLockdown.Denied;

            // Construct failsafe permission sets
            // Grant Send Messages to Cliptok and Moderator in addition to any permissions they might already have,
            // and Send Messages in Threads if 'lockThreads' is set
            var cliptokAllowedPermissions = cliptokAllowedPermissionsBeforeLockdown.Add(DiscordPermission.SendMessages);
            if (lockThreads)
                cliptokAllowedPermissions = cliptokAllowedPermissions.Add(DiscordPermission.SendThreadMessages);
            var moderatorAllowedPermissions = moderatorAllowedPermissionsBeforeLockdown.Add(DiscordPermission.SendMessages);
            if (lockThreads)
                moderatorAllowedPermissions = moderatorAllowedPermissions.Add(DiscordPermission.SendThreadMessages);

            // Apply failsafes for lockdown
            await channel.AddOverwriteAsync(channel.Guild.CurrentMember, cliptokAllowedPermissions, cliptokDeniedPermissionsBeforeLockdown, "Failsafe 1 for Lockdown");
            await channel.AddOverwriteAsync(await channel.Guild.GetRoleAsync(Program.cfgjson.ModRole), moderatorAllowedPermissions, moderatorDeniedPermissionsBeforeLockdown, "Failsafe 2 for Lockdown");

            // Get the @everyone role's permission set from before the lockdown
            var everyoneOverwritesBeforeLockdown = existingOverwrites.Where(x => x.Id == channel.Guild.EveryoneRole.Id).FirstOrDefault();

            // Get the @everyone role's allowed permission set
            var everyoneAllowedPermissionsBeforeLockdown = DiscordPermissions.None;
            if (everyoneOverwritesBeforeLockdown is not null)
                everyoneAllowedPermissionsBeforeLockdown = everyoneOverwritesBeforeLockdown.Allowed;

            // Get the @everyone role's denied permission set
            var everyoneDeniedPermissionsBeforeLockdown = DiscordPermissions.None;
            if (everyoneOverwritesBeforeLockdown is not null)
                everyoneDeniedPermissionsBeforeLockdown = everyoneOverwritesBeforeLockdown.Denied;

            // Construct new @everyone permission set
            var everyoneDeniedPermissions = everyoneDeniedPermissionsBeforeLockdown.Add(DiscordPermission.SendMessages);
            if (lockThreads)
                everyoneDeniedPermissions = everyoneDeniedPermissions.Add(DiscordPermission.SendThreadMessages);

            // Lock the channel
            await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, everyoneAllowedPermissionsBeforeLockdown, everyoneDeniedPermissions, $"[Lockdown by {DiscordHelpers.UniqueUsername(user)}]: {reason}");

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
        }

        public static async Task UnlockChannel(DiscordChannel discordChannel, DiscordMember discordMember, string reason = "No reason specified.", bool isMassUnlock = false)
        {
            if (!Program.cfgjson.LockdownEnabledChannels.Contains(discordChannel.Id))
            {
                throw new ArgumentException($"Channel {discordChannel.Id} is not in the lockdown whitelist.");
            }

            // Get the permissions that are already on the channel, so that we can make sure they are kept when we adjust overwrites for the unlock
            var permissions = discordChannel.PermissionOverwrites.ToArray();

            // Get Cliptok's permission set from before the unlock
            var cliptokOverwritesBeforeUnlock = permissions.Where(x => x.Id == Program.discord.CurrentUser.Id).FirstOrDefault();

            // Get Cliptok's allowed permission set
            var cliptokAllowedPermissionsBeforeUnlock = DiscordPermissions.None;
            if (cliptokOverwritesBeforeUnlock is not null)
                cliptokAllowedPermissionsBeforeUnlock = cliptokOverwritesBeforeUnlock.Allowed;

            // Get Cliptok's denied permission set
            var cliptokDeniedPermissionsBeforeUnlock = DiscordPermissions.None;
            if (cliptokOverwritesBeforeUnlock is not null)
                cliptokDeniedPermissionsBeforeUnlock = cliptokOverwritesBeforeUnlock.Denied;

            // Get the Moderator role's permission set from before the unlock
            var moderatorOverwritesBeforeUnlock = permissions.Where(x => x.Id == Program.cfgjson.ModRole).FirstOrDefault();

            // Get the Moderator role's allowed permission set
            var moderatorAllowedPermissionsBeforeUnlock = DiscordPermissions.None;
            if (moderatorOverwritesBeforeUnlock is not null)
                moderatorAllowedPermissionsBeforeUnlock = moderatorOverwritesBeforeUnlock.Allowed;

            // Get the Moderator role's denied permission set
            var moderatorDeniedPermissionsBeforeUnlock = DiscordPermissions.None;
            if (moderatorOverwritesBeforeUnlock is not null)
                moderatorDeniedPermissionsBeforeUnlock = moderatorOverwritesBeforeUnlock.Denied;

            // Construct new permission sets for Cliptok and Moderator
            // Resets Send Messages and Send Messages in Threads for Cliptok and Moderator, while preserving other permissions
            var cliptokAllowedPermissions = cliptokAllowedPermissionsBeforeUnlock.Remove(DiscordPermission.SendMessages).Remove(DiscordPermission.SendThreadMessages);
            var moderatorAllowedPermissions = moderatorAllowedPermissionsBeforeUnlock.Remove(DiscordPermission.SendMessages).Remove(DiscordPermission.SendThreadMessages);

            // Get the @everyone role's permission set from before the unlock
            var everyoneOverwritesBeforeUnlock = permissions.Where(x => x.Id == discordChannel.Guild.EveryoneRole.Id).FirstOrDefault();

            // Get the @everyone role's allowed permission set
            var everyoneAllowedPermissionsBeforeUnlock = DiscordPermissions.None;
            if (everyoneOverwritesBeforeUnlock is not null)
                everyoneAllowedPermissionsBeforeUnlock = everyoneOverwritesBeforeUnlock.Allowed;

            // Get the @everyone role's denied permission set
            var everyoneDeniedPermissionsBeforeUnlock = DiscordPermissions.None;
            if (everyoneOverwritesBeforeUnlock is not null)
                everyoneDeniedPermissionsBeforeUnlock = everyoneOverwritesBeforeUnlock.Denied;

            // Construct new permission set for @everyone
            // Resets Send Messages and Send Messages in Threads while preserving other permissions
            var everyoneDeniedPermissions = everyoneDeniedPermissionsBeforeUnlock.Remove(DiscordPermission.SendMessages).Remove(DiscordPermission.SendThreadMessages);

            // Unlock the channel
            await discordChannel.AddOverwriteAsync(discordChannel.Guild.EveryoneRole, everyoneAllowedPermissionsBeforeUnlock, everyoneDeniedPermissions, $"[Unlock by {DiscordHelpers.UniqueUsername(discordMember)}]: {reason}");

            // Remove failsafes
            // For any failsafes where the after-unlock permission set is completely empty, delete the override entirely

            if (cliptokAllowedPermissions == DiscordPermissions.None && cliptokDeniedPermissionsBeforeUnlock == DiscordPermissions.None)
                await discordChannel.DeleteOverwriteAsync(discordChannel.Guild.CurrentMember, "Resetting Lockdown failsafe 1 for unlock");
            else
                await discordChannel.AddOverwriteAsync(discordChannel.Guild.CurrentMember, cliptokAllowedPermissions, cliptokDeniedPermissionsBeforeUnlock, "Resetting Lockdown failsafe 1 for unlock");

            if (moderatorAllowedPermissions == DiscordPermissions.None && moderatorDeniedPermissionsBeforeUnlock == DiscordPermissions.None)
                await discordChannel.DeleteOverwriteAsync(await discordChannel.Guild.GetRoleAsync(Program.cfgjson.ModRole), "Resetting Lockdown failsafe 2 for unlock");
            else
                await discordChannel.AddOverwriteAsync(await discordChannel.Guild.GetRoleAsync(Program.cfgjson.ModRole), moderatorAllowedPermissions, moderatorDeniedPermissionsBeforeUnlock, "Resetting Lockdown failsafe 2 for unlock");

            await Program.db.HashDeleteAsync("unlocks", discordChannel.Id);
            await discordChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Unlock} This channel has been unlocked!");
        }

    }
}
