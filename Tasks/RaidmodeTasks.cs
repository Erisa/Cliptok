namespace Cliptok.Tasks
{
    internal class RaidmodeTasks
    {
        public static async Task<bool> CheckRaidmodeAsync(ulong guildId)
        {
            if (!Program.db.HashExists("raidmode", guildId))
            {
                return false;
            }
            else
            {
                long unixExpiration = (long)Program.db.HashGet("raidmode", guildId);
                long currentUnixTime = TimeHelpers.ToUnixTimestamp(DateTime.Now);
                if (currentUnixTime >= unixExpiration)
                {
                    Program.db.HashDelete("raidmode", guildId);
                    Program.db.KeyDelete("raidmode-accountage");
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
