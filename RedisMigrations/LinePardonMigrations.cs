namespace Cliptok.Migrations
{
    class LinePardonMigrations
    {
        public static async Task MigrateLinePardonToSetAsync()
        {
            if (!Program.redis.KeyExists("linePardoned") || Program.redis.KeyType("linePardoned") == RedisType.Set)
                return;

            // archive old data
            await Program.redis.KeyRenameAsync("linePardoned", "linePardonedOld");

            // migrate to set
            var linePardonList = await Program.redis.HashGetAllAsync("linePardonedOld");
            foreach (var line in linePardonList)
            {
                await Program.redis.SetAddAsync("linePardoned", line.Name);
            }

            Program.discord.Logger.LogInformation(Program.CliptokEventID, "Successfully migrated {count} line pardons to set.", linePardonList.Length);
        }
    }
}
