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

            await channel.AddOverwriteAsync(channel.Guild.CurrentMember, Permissions.SendMessages, Permissions.None, "Failsafe 1 for Lockdown");
            await channel.AddOverwriteAsync(channel.Guild.GetRole(Program.cfgjson.ModRole), Permissions.SendMessages, Permissions.None, "Failsafe 2 for Lockdown");
            await channel.AddOverwriteAsync(channel.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages, "Lockdown command");

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
                    var role = await permission.GetRoleAsync();
                    if (
                        (role == discordChannel.Guild.EveryoneRole
                        && permission.Denied == Permissions.SendMessages)
                        ||
                        (role == discordChannel.Guild.GetRole(Program.cfgjson.ModRole)
                        && permission.Allowed == Permissions.SendMessages
                        )
                        )
                    {
                        success = true;
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
