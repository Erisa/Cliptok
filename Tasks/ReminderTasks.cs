namespace Cliptok.Tasks
{
    internal class ReminderTasks
    {
        public static async Task<bool> CheckRemindersAsync()
        {
            bool success = false;

            var reminders = await Program.redis.HashGetAllAsync("reminders");
            foreach (var reminder in reminders.Select(x => JsonConvert.DeserializeObject<Reminder>(x.Value)))
            {
                bool DmFallback = false;

                if (reminder.ReminderTime > DateTime.UtcNow)
                    continue;

                var user = await Program.discord.GetUserAsync(reminder.UserId);
                DiscordChannel channel = null;
                try
                {
                    channel = await Program.discord.GetChannelAsync(reminder.ChannelId);
                }
                catch
                {
                    // channel likely doesnt exist
                }
                if (channel is null)
                {
                    var guild = Program.homeGuild;
                    var member = await guild.GetMemberAsync(reminder.UserId);

                    if ((await GetPermLevelAsync(member)) >= ServerPermLevel.TrialModerator)
                    {
                        channel = await Program.discord.GetChannelAsync(Program.cfgjson.HomeChannel);
                    }
                    else
                    {
                        channel = await member.CreateDmChannelAsync();
                        DmFallback = true;
                    }
                }

                await Program.redis.HashDeleteAsync("reminders", reminder.ReminderId);
                success = true;

                var embed = new DiscordEmbedBuilder()
                .WithDescription(reminder.ReminderText)
                .WithColor(new DiscordColor(0xD084))
                .WithFooter(
                    "Reminder was set",
                    null
                )
                .WithTimestamp(reminder.SetTime)
                .WithAuthor(
                    $"Reminder from {TimeHelpers.TimeToPrettyFormat(DateTime.UtcNow.Subtract(reminder.SetTime), true)}",
                    null,
                    user.AvatarUrl
                )
                .AddField("Context", $"https://discord.com/channels/{reminder.GuildId}/{reminder.ChannelId}/{reminder.MessageId}", true);

                var msg = new DiscordMessageBuilder()
                    .AddEmbed(embed)
                    .WithContent($"<@{reminder.UserId}>, you asked to be reminded of something:");

                if (DmFallback)
                {
                    msg.WithContent("You asked to be reminded of something:");
                    await channel.SendMessageAsync(msg);
                }
                else if (reminder.MessageId != default)
                {
                    DiscordMessage originalMessage = default;
                    try
                    {
                        originalMessage = await channel.GetMessageAsync(reminder.MessageId);
                    }
                    catch
                    {
                        // message was probably deleted

                        msg.WithContent($"<@{reminder.UserId}>, you asked to be reminded of something:");
                        msg.WithAllowedMention(new UserMention(reminder.UserId));
                    }

                    if (originalMessage is not null)
                    {
                        if (originalMessage.Author.Id == Program.discord.CurrentUser.Id)
                        {
                            msg.WithReply(reminder.MessageId, mention: true)
                                .WithContent($"<@{reminder.UserId}>, you asked to be reminded of something:")
                                .WithAllowedMention(new UserMention(reminder.UserId));
                        }
                        else
                        {
                            msg.WithReply(reminder.MessageId, mention: true)
                                .WithContent("You asked to be reminded of something:");
                        }
                    }
                }

                await channel.SendMessageAsync(msg);
            }

            Program.discord.Logger.LogDebug(Program.CliptokEventID, "Checked reminders at {time} with result: {success}", DateTime.UtcNow, success);
            return success;
        }

    }
}
