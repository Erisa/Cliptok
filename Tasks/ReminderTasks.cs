namespace Cliptok.Tasks
{
    internal class ReminderTasks
    {
        public static async Task<bool> CheckRemindersAsync()
        {
            bool success = false;
            foreach (var reminder in Program.db.ListRange("reminders", 0, -1))
            {
                bool DmFallback = false;
                var reminderObject = JsonConvert.DeserializeObject<Commands.Reminders.Reminder>(reminder);
                if (reminderObject.ReminderTime <= DateTime.Now)
                {
                    var user = await Program.discord.GetUserAsync(reminderObject.UserID);
                    DiscordChannel channel = null;
                    try
                    {
                        channel = await Program.discord.GetChannelAsync(reminderObject.ChannelID);
                    }
                    catch
                    {
                        // channel likely doesnt exist
                    }
                    if (channel == null)
                    {
                        var guild = Program.homeGuild;
                        var member = await guild.GetMemberAsync(reminderObject.UserID);

                        if (GetPermLevel(member) >= ServerPermLevel.TrialModerator)
                        {
                            channel = await Program.discord.GetChannelAsync(Program.cfgjson.HomeChannel);
                        }
                        else
                        {
                            channel = await member.CreateDmChannelAsync();
                            DmFallback = true;
                        }
                    }

                    await Program.db.ListRemoveAsync("reminders", reminder);
                    success = true;

                    var embed = new DiscordEmbedBuilder()
                    .WithDescription(reminderObject.ReminderText)
                    .WithColor(new DiscordColor(0xD084))
                    .WithFooter(
                        "Reminder was set",
                        null
                    )
                    .WithTimestamp(reminderObject.OriginalTime)
                    .WithAuthor(
                        $"Reminder from {TimeHelpers.TimeToPrettyFormat(DateTime.Now.Subtract(reminderObject.OriginalTime), true)}",
                        null,
                        user.AvatarUrl
                    )
                    .AddField("Context", $"[`Jump to context`]({reminderObject.MessageLink})", true);

                    var msg = new DiscordMessageBuilder()
                        .WithEmbed(embed)
                        .WithContent($"<@!{reminderObject.UserID}>, you asked to be reminded of something:");

                    if (DmFallback)
                    {
                        msg.WithContent("You asked to be reminded of something:");
                        await channel.SendMessageAsync(msg);
                    }
                    else if (reminderObject.MessageID != default)
                    {
                        try
                        {
                            msg.WithReply(reminderObject.MessageID, mention: true, failOnInvalidReply: true)
                                .WithContent("You asked to be reminded of something:");
                            await channel.SendMessageAsync(msg);
                        }
                        catch (DSharpPlus.Exceptions.BadRequestException)
                        {
                            msg.WithContent($"<@!{reminderObject.UserID}>, you asked to be reminded of something:");
                            msg.WithReply(null, false, false);
                            await channel.SendMessageAsync(msg);
                        }
                    }
                    else
                    {
                        await channel.SendMessageAsync(msg);
                    }
                }

            }
#if DEBUG
            Program.discord.Logger.LogDebug(Program.CliptokEventID, "Checked reminders at {time} with result: {success}", DateTime.Now, success);
#endif
            return success;
        }

    }
}
