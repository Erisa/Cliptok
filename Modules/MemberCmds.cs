using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cliptok.Modules
{
    public class MemberCmds : BaseCommandModule
    {
        [Command("ask")]
        public async Task AskCmd(CommandContext ctx, DiscordUser user = default)
        {
            await ctx.Message.DeleteAsync();
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithTitle("**__Need Help Or Have a Problem?__**")
                .WithDescription(
                $"You're probably looking for <#{Program.cfgjson.TechSupportChannel}>.\n" +
                $"{Program.cfgjson.Emoji.Windows11} Need help with **Windows 11**? Go to <#894699119195619379>\n\n" +
                $"Once there, please be sure to provide **plenty of details**, ping the <@&{Program.cfgjson.CommunityTechSupportRoleID}> role, and *be patient!*\n\n" +
                $"Look under the `🔧 Support` category for the appropriate channel for your issue. See <#413274922413195275> for more info."
                )
                .WithColor(13920845);

            if (user != default)
            {
                ctx.Channel.SendMessageAsync(user.Mention, embed);
            }
            else if (ctx.Message.ReferencedMessage != null)
            {
                var messageBuild = new DiscordMessageBuilder()
                    .WithEmbed(embed)
                    .WithReply(ctx.Message.ReferencedMessage.Id, mention: true);

                ctx.Channel.SendMessageAsync(messageBuild);
            }
            else
            {
                ctx.Channel.SendMessageAsync(embed);
            }
        }

        [Command("ping")]
        [Description("Pong? This command lets you know whether I'm working well.")]
        public async Task Ping(CommandContext ctx)
        {
            DiscordMessage return_message = await ctx.Message.RespondAsync("Pinging...");
            ulong ping = (return_message.Id - ctx.Message.Id) >> 22;
            Char[] choices = new Char[] { 'a', 'e', 'o', 'u', 'i', 'y' };
            Char letter = choices[Program.rand.Next(0, choices.Length)];
            await return_message.ModifyAsync($"P{letter}ng! 🏓\n" +
                $"• It took me `{ping}ms` to reply to your message!\n" +
                $"• Last Websocket Heartbeat took `{ctx.Client.Ping}ms`!");
        }

        [Group("timestamp")]
        [Aliases("ts", "time")]
        [Description("Returns various timestamps for a given Discord ID/snowflake")]
        [HomeServer]
        class TimestampCmds : BaseCommandModule
        {
            [GroupCommand]
            [Aliases("u", "unix", "epoch")]
            [Description("Returns the Unix timestamp of a given Discord ID/snowflake")]
            public async Task TimestampUnixCmd(CommandContext ctx, [Description("The ID/snowflake to fetch the Unix timestamp for")] ulong snowflake)
            {
                var msSinceEpoch = snowflake >> 22;
                var msUnix = msSinceEpoch + 1420070400000;
                await ctx.RespondAsync($"{msUnix / 1000}");
            }

            [Command("relative")]
            [Aliases("r")]
            [Description("Returns the amount of time between now and a given Discord ID/snowflake")]
            public async Task TimestampRelativeCmd(CommandContext ctx, [Description("The ID/snowflake to fetch the relative timestamp for")] ulong snowflake)
            {
                var msSinceEpoch = snowflake >> 22;
                var msUnix = msSinceEpoch + 1420070400000;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.ClockTime} <t:{msUnix / 1000}:R>");
            }

            [Command("fulldate")]
            [Aliases("f", "datetime")]
            [Description("Returns the fully-formatted date and time of a given Discord ID/snowflake")]
            public async Task TimestampFullCmd(CommandContext ctx, [Description("The ID/snowflake to fetch the full timestamp for")] ulong snowflake)
            {
                var msSinceEpoch = snowflake >> 22;
                var msUnix = msSinceEpoch + 1420070400000;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.ClockTime} <t:{msUnix / 1000}:F>");
            }
        }

        [Command("no")]
        [Aliases("yes")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Tier5)]
        public async Task No(CommandContext ctx)
        {
            List<string> noResponses = new List<string> {
                "Processing...",
                "Considering it...",
                "Hmmm...",
                "Give me a moment...",
                "Calculating...",
                "Generating response...",
                "Asking the Oracle...",
                "Loading...",
                "Please wait..."
            };

            await ctx.Message.DeleteAsync();
            var msg = await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Loading} Thinking about it...");
            await Task.Delay(2000);

            for (int thinkCount = 1; thinkCount <= 3; thinkCount++)
            {
                int r = Program.rand.Next(noResponses.Count);
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Loading} {noResponses[r]}");
                await Task.Delay(2000);
            }

            if (Program.rand.Next(10) == 3)
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Yes.");
            }
            else
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} No.");
            }
        }

        public class Reminder
        {
            [JsonProperty("userID")]
            public ulong UserID { get; set; }

            [JsonProperty("channelID")]
            public ulong ChannelID { get; set; }

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
        [Aliases("reminder", "rember", "wemember", "remember")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Tier4)]
        public async Task RemindMe(CommandContext ctx, string timetoParse, [RemainingText] string reminder)
        {
            DateTime t = HumanDateParser.HumanDateParser.Parse(timetoParse);
            if (t <= DateTime.Now)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Time can't be in the past!");
                return;
            }
            else if (t < (DateTime.Now + TimeSpan.FromSeconds(59)))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Time must be at least a minute in the future!");
                return;
            }

            var reminderObject = new Reminder()
            {
                UserID = ctx.User.Id,
                ChannelID = ctx.Channel.Id,
                MessageLink = $"https://discord.com/channels/{ctx.Guild.Id}/{ctx.Channel.Id}/{ctx.Message.Id}",
                ReminderText = reminder,
                ReminderTime = t,
                OriginalTime = DateTime.Now
            };

            await Program.db.ListRightPushAsync("reminders", JsonConvert.SerializeObject(reminderObject));
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} I'll try my best to remind you about that on <t:{ModCmds.ToUnixTimestamp(t)}:f> (<t:{ModCmds.ToUnixTimestamp(t)}:R>)"); // (In roughly **{Warnings.TimeToPrettyFormat(t.Subtract(ctx.Message.Timestamp.DateTime), false)}**)");
        }

        public static async Task<bool> CheckRemindersAsync()
        {
            bool success = false;
            foreach (var reminder in Program.db.ListRange("reminders", 0, -1))
            {
                var guild = await Program.discord.GetGuildAsync(Program.cfgjson.ServerID);
                var reminderObject = JsonConvert.DeserializeObject<Reminder>(reminder);
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
                        var member = await guild.GetMemberAsync(reminderObject.UserID);
                        if (Warnings.GetPermLevel(member) >= ServerPermLevel.TrialMod)
                        {
                            channel = await Program.discord.GetChannelAsync(Program.cfgjson.HomeChannel);
                        }
                        else
                        {
                            channel = await Program.discord.GetChannelAsync(240528256292356096);
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
                        $"Reminder from {Warnings.TimeToPrettyFormat(DateTime.Now.Subtract(reminderObject.OriginalTime), true)}",
                        null,
                        user.AvatarUrl
                    )
                    .AddField("Context", $"[`Jump to context`]({reminderObject.MessageLink})", true);

                    await channel.SendMessageAsync($"<@!{reminderObject.UserID}>, you asked to be reminded of something:", embed);
                }

            }
            return success;
        }
    }
}
