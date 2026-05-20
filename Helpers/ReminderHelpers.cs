using static Cliptok.Constants.RegexConstants;

namespace Cliptok.Helpers
{
    internal class ReminderHelpers
    {
        internal static (DateTime? parsedTime, string error) ParseReminderTime(string reminderTime)
        {
            DateTime parsedTime;
            try
            {
                parsedTime = TimeHelpers.ParseAnyDateFormat(reminderTime);
            }
            catch
            {
                return (null, $"{Program.cfgjson.Emoji.Error} I couldn't parse the time you entered! Please try again.");
            }

            if (parsedTime <= DateTime.UtcNow)
                return (null, $"{Program.cfgjson.Emoji.Error} Time can't be in the past!");
#if !DEBUG
            else if (parsedTime < (DateTime.UtcNow + TimeSpan.FromSeconds(59)))
                return (null, $"{Program.cfgjson.Emoji.Error} Time must be at least a minute in the future!");
#endif

            return (parsedTime, null);
        }

        internal static async Task<(Reminder reminder, string error)> GetReminderAsync(string reminderId, ulong requestingUserId)
        {
            if (!reminder_id_rx.IsMatch(reminderId))
                return (null, $"{Program.cfgjson.Emoji.Error} The reminder ID you provided isn't correct! Please try again.");

            Reminder reminder;
            try
            {
                reminder = JsonConvert.DeserializeObject<Reminder>(await Program.redis.HashGetAsync("reminders", reminderId));
            }
            catch
            {
                return (null, $"{Program.cfgjson.Emoji.Error} I couldn't find a reminder with that ID! Please try again.");
            }

            if (reminder is null || reminder.UserId != requestingUserId)
                return (null, $"{Program.cfgjson.Emoji.Error} I couldn't find a reminder with that ID! Please try again.");

            return (reminder, null);
        }

        internal static async Task<List<Reminder>> GetUserRemindersAsync(ulong userId)
        {
            return (await Program.redis.HashGetAllAsync("reminders"))
                .Select(x => JsonConvert.DeserializeObject<Reminder>(x.Value)).Where(r => r is not null && r.UserId == userId)
                .OrderBy(x => x.ReminderTime)
                .ToList();
        }

        internal static async Task<int> GenerateUniqueReminderIdAsync()
        {
            Random random = new();
            var reminderId = random.Next(1000, 9999);

            var reminders = await Program.redis.HashGetAllAsync("reminders");
            while (reminders.Any(x => x.Name == reminderId))
                reminderId = random.Next(1000, 9999);

            return reminderId;
        }

        internal static DiscordSelectComponent CreateSelectComponentFromReminders(List<Reminder> reminders, string componentCustomId)
        {
            List<DiscordSelectComponentOption> options = reminders.Select(reminder =>
                new DiscordSelectComponentOption(string.IsNullOrWhiteSpace(reminder.ReminderText)
                        ? "..."
                        : StringHelpers.Truncate(reminder.ReminderText, 100, true),
                    reminder.ReminderId.ToString(),
                    "in about " + TimeHelpers.TimeToPrettyFormat(reminder.ReminderTime.Subtract(DateTime.UtcNow).Add(TimeSpan.FromMinutes(1)), false)))
                .ToList();

            return new DiscordSelectComponent(componentCustomId, null, options);
        }

        internal static async Task<DiscordEmbed> CreateReminderListEmbedAsync(List<Reminder> reminders, DiscordUser user)
        {
            string output = "";
            foreach (var reminder in reminders)
            {
                var setTime = ((DateTimeOffset)reminder.SetTime).ToUnixTimeSeconds();

                long reminderTime = ((DateTimeOffset)reminder.ReminderTime).ToUnixTimeSeconds();

                string guildName;
                if (id_rx.IsMatch(reminder.GuildId))
                {
                    var targetGuild = await Program.discord.GetGuildAsync(Convert.ToUInt64(reminder.GuildId));
                    guildName = targetGuild.Name;
                }
                else
                {
                    guildName = "DMs";
                }

                var reminderLink = $"<https://discord.com/channels/{reminder.GuildId}/{reminder.ChannelId}/{reminder.MessageId}>";

                var reminderText = StringHelpers.Truncate(reminder.ReminderText, 300, true);

                var reminderLocation = $" in {guildName}";
                if (guildName != "DMs")
                    reminderLocation += $" <#{reminder.ChannelId}>";

                output += $"`{reminder.ReminderId}`:\n"
                          + (string.IsNullOrWhiteSpace(reminderText)
                              ? ""
                              : $"> {reminderText}\n")
                          + $"[Set <t:{setTime}:R>]({reminderLink}) to remind you <t:{reminderTime}:R>";

                output += reminderLocation;

                output += "\n\n";
            }

            DiscordEmbedBuilder embed = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor()
                {
                    Name = $"Reminders for {user.Username}",
                    IconUrl = user.AvatarUrl
                },
                Color = new DiscordColor(0xFEC13C)
            };

            if (output.Length > 4096)
            {
                embed.WithColor(DiscordColor.Red);

                var desc = "You have too many reminders to list here! Here are the IDs of each one.\n\n";

                foreach (var reminder in reminders)
                {
                    var setTime = ((DateTimeOffset)reminder.SetTime).ToUnixTimeSeconds();

                    long reminderTime = ((DateTimeOffset)reminder.ReminderTime).ToUnixTimeSeconds();

                    desc += $"`{reminder.ReminderId}` - set <t:{setTime}:R> to remind you <t:{reminderTime}:R>\n";
                }

                embed.WithDescription(desc.Trim());
            }
            else
            {
                embed.WithDescription(output);
            }

            return embed;
        }
    }
}