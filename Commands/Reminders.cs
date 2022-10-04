namespace Cliptok.Commands
{
    public class Reminders : BaseCommandModule
    {
        public class Reminder
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

        [Command("remindme")]
        [Description("Set a reminder for yourself. Example: !reminder 1h do the thing")]
        [Aliases("reminder", "rember", "wemember", "remember", "remind")]
        [RequireHomeserverPerm(ServerPermLevel.Tier4, WorkOutside = true)]
        public async Task RemindMe(
            CommandContext ctx,
            [Description("The amount of time to wait before reminding you. For example: 2s, 5m, 1h, 1d")] string timetoParse,
            [RemainingText, Description("The text to send when the reminder triggers.")] string reminder
        )
        {
            DateTime t = HumanDateParser.HumanDateParser.Parse(timetoParse);
            if (t <= DateTime.Now)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Time can't be in the past!");
                return;
            }
#if !DEBUG
            else if (t < (DateTime.Now + TimeSpan.FromSeconds(59)))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Time must be at least a minute in the future!");
                return;
            }
#endif
            string guildId;

            if (ctx.Channel.IsPrivate)
                guildId = "@me";
            else
                guildId = ctx.Guild.Id.ToString();

            var reminderObject = new Reminder()
            {
                UserID = ctx.User.Id,
                ChannelID = ctx.Channel.Id,
                MessageID = ctx.Message.Id,
                MessageLink = $"https://discord.com/channels/{guildId}/{ctx.Channel.Id}/{ctx.Message.Id}",
                ReminderText = reminder,
                ReminderTime = t,
                OriginalTime = DateTime.Now
            };

            await Program.db.ListRightPushAsync("reminders", JsonConvert.SerializeObject(reminderObject));
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} I'll try my best to remind you about that on <t:{TimeHelpers.ToUnixTimestamp(t)}:f> (<t:{TimeHelpers.ToUnixTimestamp(t)}:R>)"); // (In roughly **{TimeHelpers.TimeToPrettyFormat(t.Subtract(ctx.Message.Timestamp.DateTime), false)}**)");
        }

    }
}
