namespace Cliptok.Tasks
{
    internal class RaidmodeTasks
    {
        public static async Task<bool> CheckRaidmodeAsync(ulong guildId)
        {
            if (!Program.redis.HashExists("raidmode", guildId))
            {
                return false;
            }
            else
            {
                long unixExpiration = (long)Program.redis.HashGet("raidmode", guildId);
                long currentUnixTime = TimeHelpers.ToUnixTimestamp(DateTime.Now);
                if (currentUnixTime >= unixExpiration)
                {
                    Program.redis.HashDelete("raidmode", guildId);
                    Program.redis.KeyDelete("raidmode-accountage");
                    LogChannelHelper.LogMessageAsync("mod", $"{Program.cfgjson.Emoji.Off} Raidmode was **disabled** automatically.");
                    return true;
                }
                else
                {
                    return false;
                }
            }

        }
    }
}
