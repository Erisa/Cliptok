namespace Cliptok.RedisMigrations
{
    internal class ReminderMigrations
    {
        internal static async Task MigrateRemindersToHashAsync()
        {
            if (!await Program.redis.KeyExistsAsync("reminders") || await Program.redis.KeyTypeAsync("reminders") == RedisType.Hash)
                return;

            var numRemindersToMigrate = await Program.redis.ListLengthAsync("reminders");
            var numRemindersMigrated = 0;

            // archive old data
            await Program.redis.KeyRenameAsync("reminders", "remindersOld");

            // migrate to hash
            var remindersList = await Program.redis.ListRangeAsync("remindersOld");
            foreach (var reminder in remindersList)
            {
                var oldReminderObject = JsonConvert.DeserializeObject<OldReminder>(reminder);

                ulong guildId;
                try
                {
                    guildId = (await Program.discord.GetChannelAsync(oldReminderObject.ChannelID)).Guild.Id;
                }
                catch
                {
                    guildId = Program.homeGuild.Id;
                }

                var newReminderObject = new Reminder()
                {
                    UserId = oldReminderObject.UserID,
                    ChannelId = oldReminderObject.ChannelID,
                    GuildId = guildId.ToString(),
                    MessageId = oldReminderObject.MessageID,
                    ReminderId = await ReminderHelpers.GenerateUniqueReminderIdAsync(),
                    ReminderText = oldReminderObject.ReminderText,
                    ReminderTime = oldReminderObject.ReminderTime,
                    SetTime = oldReminderObject.OriginalTime
                };

                await Program.redis.HashSetAsync("reminders", newReminderObject.ReminderId, JsonConvert.SerializeObject(newReminderObject));

                numRemindersMigrated++;
            }

            if (numRemindersMigrated > 0)
                Program.discord.Logger.LogInformation("Successfully migrated {count}/{total} reminders to hash!", numRemindersMigrated, numRemindersToMigrate);

            if (numRemindersToMigrate != 0 && numRemindersMigrated != numRemindersToMigrate)
                Program.discord.Logger.LogError("Failed to migrate {count}/{total} reminders to hash!", numRemindersToMigrate - numRemindersMigrated, numRemindersToMigrate);
        }
    }

    internal class OldReminder
    {
        [JsonProperty("userID")]
        public ulong UserID { get; set; }

        [JsonProperty("channelID")]
        public ulong ChannelID { get; set; }

        [JsonProperty("messageID")]
        public ulong MessageID { get; set; }

        [JsonProperty("messageLink")]
        public string MessageLink { get; set; }

        [JsonProperty("reminderText")]
        public string ReminderText { get; set; }

        [JsonProperty("reminderTime")]
        public DateTime ReminderTime { get; set; }

        [JsonProperty("originalTime")]
        public DateTime OriginalTime { get; set; }
    }
}
