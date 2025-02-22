namespace Cliptok.Migrations
{
    class LinePardonMigrations
    {
        public static async Task MigrateLinePardonToSetAsync()
        {
           if (!Program.db.KeyExists("linePardoned") || Program.db.KeyType("linePardoned") == RedisType.Set)
                return;

            // archive old data
            await Program.db.KeyRenameAsync("linePardoned", "linePardonedOld");

            // migrate to set
            var linePardonList = await Program.db.HashGetAllAsync("linePardonedOld");
            foreach (var line in linePardonList)
            {
                await Program.db.SetAddAsync("linePardoned", line.Name);
            }

            Program.discord.Logger.LogInformation(Program.CliptokEventID, "Successfully migrated {count} line pardons to set.", linePardonList.Length);
        }
    }
}
