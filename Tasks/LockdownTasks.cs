namespace Cliptok.Tasks
{
    internal class LockdownTasks
    {
        public static async Task<bool> CheckUnlocksAsync()
        {
            var channelUnlocks = await Program.db.HashGetAllAsync("unlocks");
            var success = false;

            foreach (var channelUnlock in channelUnlocks)
            {
                long unixExpiration = (long)channelUnlock.Value;
                long currentUnixTime = TimeHelpers.ToUnixTimestamp(DateTime.Now);
                if (currentUnixTime >= unixExpiration)
                {
                    var channel = await Program.discord.GetChannelAsync((ulong)channelUnlock.Name);
                    var currentMember = await channel.Guild.GetMemberAsync(Program.discord.CurrentUser.Id);
                    await LockdownHelpers.UnlockChannel(channel, currentMember);
                    success = true;
                }
            }

            return success;
        }
    }
}
