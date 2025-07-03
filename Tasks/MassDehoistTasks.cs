namespace Cliptok.Tasks
{
    internal class MassDehoistTasks
    {
        public static async Task<bool> CheckAndMassDehoistTask()
        {
            var dbResult = await Program.redis.StringGetAsync("lastMassDehoistRun");

            if (dbResult.IsNull)
            {
                await MassDehoistAndUpdateTimeAsync();
            }

            var lastMassDehoistRun = DateTimeOffset.FromUnixTimeSeconds((int)dbResult);
            var timeSinceLastRun = DateTime.Now - lastMassDehoistRun;

            if (timeSinceLastRun > TimeSpan.FromHours(24))
            {
                await MassDehoistAndUpdateTimeAsync();
                return true;
            }
            else
            {
                Program.discord.Logger.LogDebug("Mass dehoist task skipped. Last run was at {time} UTC", lastMassDehoistRun);
                return false;
            }
        }

        public static async Task<bool> MassDehoistAndUpdateTimeAsync()
        {
            await Program.redis.StringSetAsync("lastMassDehoistRun", TimeHelpers.ToUnixTimestamp(DateTime.Now));
            var (totalMembers, failedMembers) = await DehoistHelpers.MassDehoistAsync(Program.homeGuild);
            if (totalMembers != failedMembers)
            {
                Program.discord.Logger.LogInformation("Successfully mass dehoisted {members} of {total} member(s)! Check Audit Log for details.", totalMembers - failedMembers, totalMembers);
                return true;
            }
            else
            {
                Program.discord.Logger.LogDebug("Mass dehoist task completed with no members to dehoist.");
                return false;
            }
        }
    }
}
