using static Cliptok.Helpers.ReminderHelpers;

namespace Cliptok.Commands
{
    public class ReminderCmds
    {
        // Used to pass context to modal handling
        // <user ID, message from context>
        public static Dictionary<ulong, DiscordMessage> ReminderInteractionCache = new();

        [Command("Remind Me About This")]
        [AllowedProcessors(typeof(MessageCommandProcessor))]
        [SlashCommandTypes(DiscordApplicationCommandType.MessageContextMenu)]

        public static async Task ContextReminder(MessageCommandContext ctx, DiscordMessage targetMessage)
        {
            await ctx.RespondWithModalAsync(new DiscordModalBuilder()
                .WithTitle("Remind Me About This")
                .WithCustomId("remind-me-about-this-modal-callback")
                .AddTextInput(new DiscordTextInputComponent("remind-me-about-this-time-input"), "When do you want to be reminded?")
            );

            ReminderInteractionCache[ctx.User.Id] = targetMessage;
        }

        [Command("reminder")]
        [Description("Set, modify and delete reminders.")]
        [TextAlias("remindme", "rember", "wemember", "remember", "remind")]
        [RequireHomeserverPerm(ServerPermLevel.Tier4, WorkOutside = true)]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        public class ReminderCommand
        {
            [Command("set")]
            [Description("Set a reminder.")]
            [DefaultGroupCommand]
            public static async Task SetReminder(CommandContext ctx,
            [Parameter("time"), Description("When do you want to be reminded?")]
        string time,
            [Parameter("text"), Description("What should the reminder say?")] [MinMaxLength(maxLength: 1000)] [RemainingText]
        string text = "")
            {
                if (ctx is SlashCommandContext)
                    await ctx.As<SlashCommandContext>().DeferResponseAsync();

                var (parsedTime, error) = ParseReminderTime(time);
                if (parsedTime is null)
                {
                    await ctx.RespondAsync(error, ephemeral: true);
                    return;
                }

                Reminder reminder = new()
                {
                    UserId = ctx.User.Id,
                    ChannelId = ctx.Channel.Id,
                    GuildId = ctx.Channel.IsPrivate ? "@me" : ctx.Guild.Id.ToString(),
                    ReminderId = await GenerateUniqueReminderIdAsync(),
                    ReminderText = text,
                    ReminderTime = parsedTime.Value,
                    SetTime = DateTime.UtcNow
                };

                var unixTime = ((DateTimeOffset)parsedTime).ToUnixTimeSeconds();

                DiscordMessage message;
                if (ctx is SlashCommandContext)
                {
                    message = await ctx.As<SlashCommandContext>().FollowupAsync(new DiscordFollowupMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Success} I'll try my best to remind you about that on <t:{unixTime}:f> (<t:{unixTime}:R>)"));
                    reminder.MessageId = message.Id;
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} I'll try my best to remind you about that on <t:{unixTime}:f> (<t:{unixTime}:R>)");
                    reminder.MessageId = ctx.As<TextCommandContext>().Message.Id;
                }

                await Program.redis.HashSetAsync("reminders", reminder.ReminderId, JsonConvert.SerializeObject(reminder));
            }

            [Command("list")]
            [Description("List your reminders.")]
            [AllowedProcessors(typeof(SlashCommandProcessor))]
            public static async Task ListReminders(SlashCommandContext ctx)
            {
                await ctx.DeferResponseAsync(true);

                var userReminders = await GetUserRemindersAsync(ctx.User.Id);

                if (userReminders.Count == 0)
                {
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Error} You don't have any reminders!")
                        .AsEphemeral());
                    return;
                }

                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(await CreateReminderListEmbedAsync(userReminders, ctx.User))
                    .AsEphemeral());
            }

            [Command("delete")]
            [Description("Delete a reminder.")]
            [AllowedProcessors(typeof(SlashCommandProcessor))]
            public static async Task DeleteReminder(SlashCommandContext ctx)
            {
                // We can't defer this!! Want to respond with a modal if the user has >25 reminders.

                var userReminders = await GetUserRemindersAsync(ctx.User.Id);
                if (userReminders.Count == 0)
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Error} You don't have any reminders!")
                        .AsEphemeral());
                    return;
                }
                else if (userReminders.Count <= 25)
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Please choose a reminder to delete.")
                        .AddActionRowComponent(CreateSelectComponentFromReminders(userReminders, "reminder-delete-dropdown-callback"))
                        .AsEphemeral());
                }
                else
                {
                    // User has more than 25 reminders. Show a modal where they are prompted to enter the ID for the reminder they want to delete.
                    // I wanted to paginate a select menu instead, but Discord and D#+ limitations make that really difficult for now. (cba writing my own pagination)

                    var modalText = "You have a lot of reminders! Please enter the ID of the reminder you wish to delete.";

                    await ctx.RespondWithModalAsync(new DiscordModalBuilder().WithCustomId("reminder-delete-modal-callback").WithTitle("Delete a Reminder")
                        .AddTextDisplay(modalText)
                        .AddTextInput(new DiscordTextInputComponent("reminder-delete-id-input"), "Reminder ID"));
                }
            }

            [Command("modify")]
            [Description("Modify a reminder.")]
            [AllowedProcessors(typeof(SlashCommandProcessor))]
            public static async Task ModifyReminder(SlashCommandContext ctx)
            {
                // We can't defer this!! Want to respond with a modal if the user has >25 reminders.

                var userReminders = await GetUserRemindersAsync(ctx.User.Id);
                if (userReminders.Count == 0)
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Error} You don't have any reminders!")
                        .AsEphemeral());
                    return;
                }
                else if (userReminders.Count <= 25)
                {
                    await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder().WithContent("Please choose a reminder to modify.")
                        .AddActionRowComponent(CreateSelectComponentFromReminders(userReminders, "reminder-modify-dropdown-callback"))
                        .AsEphemeral());
                }
                else
                {
                    // User has more than 25 reminders. Show a modal where they are prompted to enter the ID for the reminder they want to modify.
                    // I wanted to paginate a select menu instead, but Discord and D#+ limitations make that really difficult for now. (cba writing my own pagination)

                    var modalText = "You have a lot of reminders! Please enter the ID of the reminder you wish to modify.";

                    await ctx.RespondWithModalAsync(new DiscordModalBuilder().WithCustomId("reminder-modify-modal-callback").WithTitle("Modify a Reminder")
                        .AddTextDisplay(modalText)
                        .AddTextInput(new DiscordTextInputComponent("reminder-modify-id-input"), "Reminder ID")
                        .AddTextInput(new DiscordTextInputComponent("reminder-modify-time-input", required: false), "(Optional) Enter the new reminder time:")
                        .AddTextInput(new DiscordTextInputComponent("reminder-modify-text-input", required: false), "(Optional) Enter the new reminder text:"));
                }
            }

            [Command("show")]
            [Description("Show the details for a reminder.")]
            [AllowedProcessors(typeof(SlashCommandProcessor))]
            public static async Task ReminderShow(SlashCommandContext ctx,
            [Parameter("id"), Description("The ID of the reminder to show.")] string id)
            {
                await ctx.DeferResponseAsync(true);

                var (reminder, error) = await GetReminderAsync(id, ctx.User.Id);
                if (reminder is null)
                {
                    await ctx.RespondAsync(error, ephemeral: true);
                    return;
                }

                DiscordEmbedBuilder embed = new()
                {
                    Title = $"Reminder `{id}`",
                    Description = reminder.ReminderText,
                    Color = new DiscordColor(0xFEC13D)
                };

                if (reminder.GuildId != "@me")
                {
                    embed.AddField("Server",
                        $"{(await Program.discord.GetGuildAsync(Convert.ToUInt64(reminder.GuildId))).Name}");
                    embed.AddField("Channel", $"<#{reminder.ChannelId}>");
                }

                embed.AddField("Context", $"https://discord.com/channels/{reminder.GuildId}/{reminder.ChannelId}/{reminder.MessageId}");

                var setTime = ((DateTimeOffset)reminder.SetTime).ToUnixTimeSeconds();

                long reminderTime = ((DateTimeOffset)reminder.ReminderTime).ToUnixTimeSeconds();

                embed.AddField("Set At", $"<t:{setTime}:F> (<t:{setTime}:R>)");

                embed.AddField("Set For", $"<t:{reminderTime}:F> (<t:{reminderTime}:R>)");

                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed).AsEphemeral());
            }
        }
    }
}
