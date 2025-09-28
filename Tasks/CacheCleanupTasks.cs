using Microsoft.EntityFrameworkCore;

namespace Cliptok.Tasks
{
    internal class CacheCleanupTasks
    {
        public static async Task DeleteOldMessageCacheDataAsync()
        {
            await Program.redis.StringSetAsync("lastCacheDelete", TimeHelpers.ToUnixTimestamp(DateTime.UtcNow));
            using var dbContext = new CliptokDbContext();
            DateTime cutoffDate = DateTime.UtcNow.AddDays(0 - Program.cfgjson.MessageCachePruneDays);
            int rowsAffected = await dbContext.Messages
                .Where(m => m.Timestamp < cutoffDate)
                .ExecuteDeleteAsync();
            Program.discord.Logger.LogDebug("Deleted {rows} old message(s) from database", rowsAffected);
        }

        public static async Task<bool> CheckAndDeleteOldMessageCacheAsync()
        {
            if (!Program.cfgjson.EnablePersistentDb)
                return false;

            var dbResult = await Program.redis.StringGetAsync("lastCacheDelete");
            bool firstRun = false;

            if (dbResult.IsNull)
            {
                await DeleteOldMessageCacheDataAsync();
                firstRun = true;
            }

            var lastCacheDelete = DateTimeOffset.FromUnixTimeSeconds((int)dbResult);
            var timeSinceLastRun = DateTime.UtcNow - lastCacheDelete;

            if (firstRun)
                return true;

            if (timeSinceLastRun > TimeSpan.FromHours(24))
            {
                await DeleteOldMessageCacheDataAsync();
                return true;
            }
            else
            {
                Program.discord.Logger.LogDebug("Cache delete run skipped. Last run was at {time} UTC", lastCacheDelete);
                return false;
            }
        }
    }
}
