using Cliptok.Helpers;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using static Cliptok.Modules.MessageEvent;

namespace Cliptok.Modules
{

    public class ModCmds : BaseCommandModule
    {
        public const char dehoistCharacter = '\u17b5';
        public bool ongoingLockdown = false;

        public static async Task<bool> CheckBansAsync()
        {
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);
            Dictionary<string, MemberPunishment> banList = Program.db.HashGetAll("bans").ToDictionary(
                x => x.Name.ToString(),
                x => JsonConvert.DeserializeObject<MemberPunishment>(x.Value)
            );
            if (banList == null | banList.Keys.Count == 0)
                return false;
            else
            {
                // The success value will be changed later if any of the unmutes are successful.
                bool success = false;
                foreach (KeyValuePair<string, MemberPunishment> entry in banList)
                {
                    MemberPunishment banEntry = entry.Value;
                    DiscordGuild targetGuild = await Program.discord.GetGuildAsync(banEntry.ServerId);
                    if (DateTime.Now > banEntry.ExpireTime)
                    {
                        targetGuild = await Program.discord.GetGuildAsync(banEntry.ServerId);
                        var user = await Program.discord.GetUserAsync(banEntry.MemberId);
                        await UnbanUserAsync(targetGuild, user);
                        success = true;

                    }

                }
#if DEBUG
                Program.discord.Logger.LogDebug(Program.CliptokEventID, $"Checked bans at {DateTime.Now} with result: {success}");
#endif
                return success;
            }
        }

        // If invoker is allowed to mod target.
        public static bool AllowedToMod(DiscordMember invoker, DiscordMember target)
        {
            return GetHier(invoker) > GetHier(target);
        }

        public static int GetHier(DiscordMember target)
        {
            return target.IsOwner ? int.MaxValue : (!target.Roles.Any() ? 0 : target.Roles.Max(x => x.Position));
        }

        [Command("kick")]
        [Aliases("yeet", "shoo", "goaway")]
        [Description("Kicks a user, removing them from the server until they rejoin. Generally not very useful.")]
        [RequirePermissions(Permissions.KickMembers), HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task Kick(CommandContext ctx, DiscordUser target, [RemainingText] string reason = "No reason specified.")
        {
            reason = reason.Replace("`", "\\`").Replace("*", "\\*");

            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(target.Id);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be in the server!");
                return;
            }

            if (AllowedToMod(ctx.Member, member))
            {
                if (AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
                {
                    await ctx.Message.DeleteAsync();
                    await KickAndLogAsync(member, reason, ctx.Member);
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Ejected} {target.Mention} has been kicked: **{reason}**");
                    return;
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to kick **{target.Username}#{target.Discriminator}**!");
                    return;
                }
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You aren't allowed to kick **{target.Username}#{target.Discriminator}**!");
                return;
            }
        }

        [Command("dehoist")]
        [Description("Adds an invisible character to someone's nickname that drops them to the bottom of the member list. Accepts multiple members.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task Dehoist(CommandContext ctx, [Description("List of server members to dehoist")] params DiscordMember[] discordMembers)
        {
            if (discordMembers.Length == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You need to tell me who to dehoist!");
                return;
            }
            else if (discordMembers.Length == 1)
            {
                if (discordMembers[0].DisplayName[0] == dehoistCharacter)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {discordMembers[0].Mention} is already dehoisted!");
                    return;
                }
                try
                {
                    await discordMembers[0].ModifyAsync(a =>
                    {
                        a.Nickname = DehoistName(discordMembers[0].DisplayName);
                    });
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully dehoisted {discordMembers[0].Mention}!");
                }
                catch
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to dehoist {discordMembers[0].Mention}!");
                }
                return;
            }

            var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it...");
            int failedCount = 0;

            foreach (DiscordMember discordMember in discordMembers)
            {
                var origName = discordMember.DisplayName;
                if (origName[0] == '\u17b5')
                {
                    failedCount++;
                }
                else
                {
                    try
                    {
                        await discordMember.ModifyAsync(a =>
                        {
                            a.Nickname = DehoistName(origName);
                        });
                    }
                    catch
                    {
                        failedCount++;
                    }
                }

            }
            _ = await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully dehoisted {discordMembers.Length - failedCount} of {discordMembers.Length} member(s)! (Check Audit Log for details)");
        }

        [Command("massdehoist")]
        [Description("Dehoist everyone on the server who has a bad name. WARNING: This is a computationally expensive operation.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task MassDehoist(CommandContext ctx)
        {
            var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it. This will take a while.");
            var discordMembers = await ctx.Guild.GetAllMembersAsync();
            int failedCount = 0;

            foreach (DiscordMember discordMember in discordMembers)
            {
                bool success = await Program.CheckAndDehoistMemberAsync(discordMember);
                if (!success)
                    failedCount++;
            }
            await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully dehoisted {discordMembers.Count - failedCount} of {discordMembers.Count} member(s)! (Check Audit Log for details)");
        }

        [Command("massundehoist")]
        [Description("Remove the dehoist for users attached via a txt file.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task MassUndhoist(CommandContext ctx)
        {
            int failedCount = 0;

            if (ctx.Message.Attachments.Count == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Please upload an attachment as well.");
            }
            else
            {
                string strList;
                using (WebClient client = new())
                {
                    strList = client.DownloadString(ctx.Message.Attachments[0].Url);
                }

                var list = strList.Split(' ');

                var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it. This will take a while.");

                foreach (string strID in list)
                {
                    ulong id = Convert.ToUInt64(strID);
                    DiscordMember member = default;
                    try
                    {
                        member = await ctx.Guild.GetMemberAsync(id);
                    }
                    catch (DSharpPlus.Exceptions.NotFoundException)
                    {
                        failedCount++;
                        continue;
                    }

                    if (member.Nickname != null && member.Nickname[0] == dehoistCharacter)
                    {
                        var newNickname = member.Nickname[1..];
                        await member.ModifyAsync(a =>
                        {
                            a.Nickname = newNickname;
                        }
                        );
                    }
                    else
                    {
                        failedCount++;
                    }
                }

                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully undehoisted {list.Length - failedCount} of {list.Length} member(s)! (Check Audit Log for details)");

            }
        }

        public static string DehoistName(string origName)
        {
            if (origName.Length == 32)
            {
                origName = origName[0..^1];
            }
            return dehoistCharacter + origName;
        }

        public async static Task<bool> UnbanUserAsync(DiscordGuild guild, DiscordUser target)
        {
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);
            await Program.db.HashSetAsync("unbanned", target.Id, true);
            try
            {
                await guild.UnbanMemberAsync(target);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
            await logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Unbanned} Successfully unbanned <@{target.Id}>!");
            await Program.db.HashDeleteAsync("bans", target.Id.ToString());
            return true;
        }

        public async static Task KickAndLogAsync(DiscordMember target, string reason, DiscordMember moderator)
        {
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);
            await target.RemoveAsync(reason);
            await logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Ejected} <@{target.Id}> was kicked by `{moderator.Username}#{moderator.Discriminator}` (`{moderator.Id}`).\nReason: **{reason}**");
        }

        [Command("tellraw")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task TellRaw(CommandContext ctx, DiscordChannel discordChannel, [RemainingText] string output)
        {
            try
            {
                await discordChannel.SendMessageAsync(output);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Your dumb message didn't want to send. Congrats, I'm proud of you.");
                return;
            }
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} I sent your stupid message to {discordChannel.Mention}.");

        }

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
        [Aliases("reminder", "rember", "wemember", "remember", "remind")]
        [RequireHomeserverPerm(ServerPermLevel.Tier4, WorkOutside = true)]
        public async Task RemindMe(CommandContext ctx, string timetoParse, [RemainingText] string reminder)
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
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} I'll try my best to remind you about that on <t:{ToUnixTimestamp(t)}:f> (<t:{ToUnixTimestamp(t)}:R>)"); // (In roughly **{Warnings.TimeToPrettyFormat(t.Subtract(ctx.Message.Timestamp.DateTime), false)}**)");
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
                await ctx.RespondAsync($"{(msUnix / 1000).ToString()}");
            }

            [Command("relative")]
            [Aliases("r")]
            [Description("Returns the amount of time between now and a given Discord ID/snowflake")]
            public async Task TimestampRelativeCmd(CommandContext ctx, [Description("The ID/snowflake to fetch the relative timestamp for")] ulong snowflake)
            {
                var msSinceEpoch = snowflake >> 22;
                var msUnix = msSinceEpoch + 1420070400000;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.ClockTime} <t:{(msUnix / 1000).ToString()}:R>");
            }

            [Command("fulldate")]
            [Aliases("f", "datetime")]
            [Description("Returns the fully-formatted date and time of a given Discord ID/snowflake")]
            public async Task TimestampFullCmd(CommandContext ctx, [Description("The ID/snowflake to fetch the full timestamp for")] ulong snowflake)
            {
                var msSinceEpoch = snowflake >> 22;
                var msUnix = msSinceEpoch + 1420070400000;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.ClockTime} <t:{(msUnix / 1000).ToString()}:F>");
            }

        }

        public static long ToUnixTimestamp(DateTime? dateTime)
        {
            return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
        }

        public static async Task<bool> CheckRemindersAsync()
        {
            bool success = false;
            foreach (var reminder in Program.db.ListRange("reminders", 0, -1))
            {
                bool DmFallback = false;
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
                        var guild = await Program.discord.GetGuildAsync(Program.cfgjson.ServerID);
                        var member = await guild.GetMemberAsync(reminderObject.UserID);

                        if (Warnings.GetPermLevel(member) >= ServerPermLevel.TrialModerator)
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
                        $"Reminder from {Warnings.TimeToPrettyFormat(DateTime.Now.Subtract(reminderObject.OriginalTime), true)}",
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
            Program.discord.Logger.LogDebug(Program.CliptokEventID, $"Checked reminders at {DateTime.Now} with result: {success}");
#endif
            return success;
        }

        [Command("announce")]
        [Description("Announces something in the current channel, pinging an Insider role in the process.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task AnnounceCmd(CommandContext ctx, [Description("'dev','beta','rp' or 'patch")] string roleName, [RemainingText, Description("The announcement message to send.")] string announcementMessage)
        {
            DiscordRole discordRole;

            if (Program.cfgjson.AnnouncementRoles.ContainsKey(roleName))
            {
                discordRole = ctx.Guild.GetRole(Program.cfgjson.AnnouncementRoles[roleName]);
                await discordRole.ModifyAsync(mentionable: true);
                try
                {
                    await ctx.Message.DeleteAsync();
                    await ctx.Channel.SendMessageAsync($"{discordRole.Mention} {announcementMessage}");
                }
                catch
                {
                    // We still need to remember to make it unmentionable even if the msg fails.
                }
                await discordRole.ModifyAsync(mentionable: false);
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That role name isnt recognised!");
                return;
            }

        }

        [Group("debug")]
        [Aliases("troubleshoot", "unbug", "bugn't", "helpsomethinghasgoneverywrong")]
        [Description("Commands and things for fixing the bot in the unlikely event that it breaks a bit.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        class DebugCmds : BaseCommandModule
        {
            [Command("mutes")]
            [Aliases("mute")]
            [Description("Debug the list of mutes.")]
            public async Task MuteDebug(CommandContext ctx, DiscordUser targetUser = default)
            {
                try
                {
                    await ctx.TriggerTypingAsync();
                }
                catch
                {
                    // typing failing is unimportant, move on
                }

                string strOut = "";
                if (targetUser == default)
                {
                    var muteList = Program.db.HashGetAll("mutes").ToDictionary();
                    if (muteList == null | muteList.Keys.Count == 0)
                    {
                        await ctx.RespondAsync("No mutes found in database!");
                        return;
                    }
                    else
                    {
                        foreach (var entry in muteList)
                        {
                            strOut += $"{entry.Value}\n";
                        }
                    }
                    if (strOut.Length > 1930)
                    {
                        HasteBinResult hasteResult = await Program.hasteUploader.Post(strOut);
                        if (hasteResult.IsSuccess)
                        {
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} Output exceeded character limit: {hasteResult.FullUrl}.json");
                        }
                        else
                        {
                            Console.WriteLine(strOut);
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Unknown error ocurred during upload to Hastebin.\nPlease try again or contact the bot owner.");
                        }
                    }
                    else
                    {
                        await ctx.RespondAsync($"```json\n{strOut}\n```");
                    }
                }
                else // if (targetUser != default)
                {
                    var userMute = Program.db.HashGet("mutes", targetUser.Id);
                    if (userMute.IsNull)
                    {
                        await ctx.RespondAsync("That user has no mute registered in the database!");
                    }
                    else
                    {
                        await ctx.RespondAsync($"```json\n{userMute}\n```");
                    }
                }
            }

            [Command("bans")]
            [Aliases("ban")]
            [Description("Debug the list of bans.")]
            public async Task BanDebug(CommandContext ctx, DiscordUser targetUser = default)
            {
                try
                {
                    await ctx.TriggerTypingAsync();
                }
                catch
                {
                    // typing failing is unimportant, move on
                }

                string strOut = "";
                if (targetUser == default)
                {
                    var banList = Program.db.HashGetAll("bans").ToDictionary();
                    if (banList == null | banList.Keys.Count == 0)
                    {
                        await ctx.RespondAsync("No mutes found in database!");
                        return;
                    }
                    else
                    {
                        foreach (var entry in banList)
                        {
                            strOut += $"{entry.Value}\n";
                        }
                    }
                    if (strOut.Length > 1930)
                    {
                        HasteBinResult hasteResult = await Program.hasteUploader.Post(strOut);
                        if (hasteResult.IsSuccess)
                        {
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} Output exceeded character limit: {hasteResult.FullUrl}.json");
                        }
                        else
                        {
                            Console.WriteLine(strOut);
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Unknown error ocurred during upload to Hastebin.\nPlease try again or contact the bot owner.");
                        }
                    }
                    else
                    {
                        await ctx.RespondAsync($"```json\n{strOut}\n```");
                    }
                }
                else // if (targetUser != default)
                {
                    var userMute = Program.db.HashGet("bans", targetUser.Id);
                    if (userMute.IsNull)
                    {
                        await ctx.RespondAsync("That user has no ban registered in the database!");
                    }
                    else
                    {
                        await ctx.RespondAsync($"```json\n{userMute}\n```");
                    }
                }
            }

            [Command("restart")]
            [RequireHomeserverPerm(ServerPermLevel.Admin), Description("Restart the bot. If not under Docker (Cliptok is, dw) this WILL exit instead.")]
            public async Task Restart(CommandContext ctx)
            {
                await ctx.RespondAsync("Now restarting bot.");
                Environment.Exit(1);
            }

            [Command("shutdown")]
            [RequireHomeserverPerm(ServerPermLevel.Admin), Description("Panics and shuts the bot down. Check the arguments for usage.")]
            public async Task Shutdown(CommandContext ctx, [Description("This MUST be set to \"I understand what I am doing\" for the command to work."), RemainingText] string verificationArgument)
            {
                if (verificationArgument == "I understand what I am doing")
                {
                    await ctx.RespondAsync("WARNING: The bot is now shutting down. This action is permanent.");
                    Environment.Exit(0);
                }
                else
                {
                    await ctx.RespondAsync("Invalid argument. Make sure you know what you are doing.");

                };

            }

            [Command("refresh")]
            [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
            [Description("Manually run all the automatic actions.")]
            public async Task Refresh(CommandContext ctx)
            {
                var msg = await ctx.RespondAsync("Checking for pending scheduled tasks...");
                bool bans = await CheckBansAsync();
                bool mutes = await Mutes.CheckMutesAsync();
                bool reminders = await ModCmds.CheckRemindersAsync();
                bool raidmode = await CheckRaidmodeAsync(ctx.Guild.Id);
                await msg.ModifyAsync($"Unban check result: `{bans}`\nUnmute check result: `{mutes}`\nReminders check result: `{reminders}`\nRaidmode check result: `{raidmode}`");
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

        [Command("edit")]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task Edit(CommandContext ctx, ulong messageId, [RemainingText] string content)
        {
            var msg = await ctx.Channel.GetMessageAsync(messageId);

            if (msg == null || msg.Author.Id != ctx.Client.CurrentUser.Id)
                return;

            await ctx.Message.DeleteAsync();

            await msg.ModifyAsync(content);
        }

        [Command("editannounce")]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task EditAnnounce(CommandContext ctx, ulong messageId, string roleName, [RemainingText] string content)
        {
            DiscordRole discordRole;

            if (Program.cfgjson.AnnouncementRoles.ContainsKey(roleName))
            {
                discordRole = ctx.Guild.GetRole(Program.cfgjson.AnnouncementRoles[roleName]);
                await discordRole.ModifyAsync(mentionable: true);
                try
                {
                    await ctx.Message.DeleteAsync();
                    var msg = await ctx.Channel.GetMessageAsync(messageId);
                    await msg.ModifyAsync($"{discordRole.Mention} {content}");
                }
                catch
                {
                    // We still need to remember to make it unmentionable even if the msg fails.
                }
                await discordRole.ModifyAsync(mentionable: false);
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That role name isnt recognised!");
                return;
            }
        }

        [Command("ask")]
        [HomeServer]
        public async Task AskCmd(CommandContext ctx, DiscordUser user = default)
        {
            await ctx.Message.DeleteAsync();
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithTitle("**__Need Help Or Have a Problem?__**")
                .WithDescription(
                $"You're probably looking for <#{Program.cfgjson.TechSupportChannel}>.\n" +
                $"{Program.cfgjson.Emoji.Windows11} Need help with **Windows 11**? Go to <#894699119195619379>.\n\n" +
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

        [Command("grant")]
        [Aliases("clipgrant")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task GrantCommand(CommandContext ctx, DiscordMember member)
        {
            var tierOne = ctx.Guild.GetRole(Program.cfgjson.TierRoles[0]);
            await member.GrantRoleAsync(tierOne);
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} {member.Mention} can now access the server!");
        }

        [Command("scamcheck")]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task ScamCheck(CommandContext ctx, [RemainingText] string content)
        {
            var urlMatches = MessageEvent.url_rx.Matches(content);
            if (urlMatches.Count > 0 && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") != null && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") != "useyourimagination")
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT"));
                request.Headers.Add("User-Agent", "Cliptok (https://github.com/Erisa/Cliptok)");
                MessageEvent.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var bodyObject = new PhishingRequestBody()
                {
                    Message = content
                };

                request.Content = new StringContent(JsonConvert.SerializeObject(bodyObject), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await httpClient.SendAsync(request);
                int httpStatusCode = (int)response.StatusCode;
                var httpStatus = response.StatusCode;
                string responseText = await response.Content.ReadAsStringAsync();

                if (httpStatus == System.Net.HttpStatusCode.OK)
                {
                    var phishingResponse = JsonConvert.DeserializeObject<MessageEvent.PhishingResponseBody>(responseText);

                    if (phishingResponse.Match)
                    {
                        foreach (PhishingMatch phishingMatch in phishingResponse.Matches)
                        {
                            if (phishingMatch.Domain != "discord.net" && phishingMatch.Type == "PHISHING" && phishingMatch.TrustRating == 1)
                            {
                                string responseToSend = $"```json\n{responseText}\n```";
                                if (responseToSend.Length > 1940)
                                {
                                    try
                                    {
                                        HasteBinResult hasteURL = await Program.hasteUploader.Post(responseText);
                                        if (hasteURL.IsSuccess)
                                            responseToSend = hasteURL.FullUrl + ".json";
                                        else
                                            responseToSend = "Response was too big and Hastebin failed, sorry.";
                                    }
                                    catch
                                    {
                                        responseToSend = "Response was too big and Hastebin failed, sorry.";
                                    }
                                }

                                await ctx.RespondAsync(responseToSend);
                                return;
                            }
                        }
                    }
                }
            }
            await ctx.RespondAsync("No matches found.");
        }

        [Group("clipraidmode")]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        class RaidmodeCommands : BaseCommandModule
        {
            [GroupCommand]
            [Aliases("status")]
            public async Task RaidmodeStatus(CommandContext ctx)
            {
                if (Program.db.HashExists("raidmode", ctx.Guild.Id))
                {
                    string output = $"{Program.cfgjson.Emoji.Unbanned} Raidmode is currently **enabled**.";
                    ulong expirationTimeUnix = (ulong)Program.db.HashGet("raidmode", ctx.Guild.Id);
                    output += $"\nRaidmode ends <t:{expirationTimeUnix}>";
                    await ctx.RespondAsync("output");
                } else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Banned} Raidmode is currently **disabled**.");
                }
            }

            [Command("on")]
            public async Task RaidmodeOn(CommandContext ctx, string duration = default)
            {
                if (Program.db.HashExists("raidmode", ctx.Guild.Id))
                {
                    string output = $"{Program.cfgjson.Emoji.Unbanned} Raidmode is already **enabled**.";

                    ulong expirationTimeUnix = (ulong)Program.db.HashGet("raidmode", ctx.Guild.Id);
                    output += $"\nRaidmode ends <t:{expirationTimeUnix}>";
                    await ctx.RespondAsync(output);
                }
                else
                {
                    DateTime parsedExpiration;

                    if (duration == default)
                        parsedExpiration = DateTime.Now.AddHours(3);
                    else
                        parsedExpiration = HumanDateParser.HumanDateParser.Parse(duration);

                    long unixExpiration = ToUnixTimestamp(parsedExpiration);
                    Program.db.HashSet("raidmode", ctx.Guild.Id, unixExpiration);

                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Raidmode is now **enabled** and will end <t:{unixExpiration}:R>.");
                    DiscordMessageBuilder response = new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Unbanned} Raidmode was **enabled** by {ctx.User.Mention} and ends <t:{unixExpiration}:R>.")
                        .WithAllowedMentions(Mentions.None);
                    await Program.logChannel.SendMessageAsync(response);
                }
            }

            [Command("off")]
            public async Task RaidmdodeOff(CommandContext ctx)
            {
                if (Program.db.HashExists("raidmode", ctx.Guild.Id))
                {
                    long expirationTimeUnix = (long)Program.db.HashGet("raidmode", ctx.Guild.Id);
                    Program.db.HashDelete("raidmode", ctx.Guild.Id);
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Raidmode is now **disabled**.\nIt was supposed to end <t:{expirationTimeUnix}:R>.");
                    DiscordMessageBuilder response = new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Banned} Raidmode was **disabled** by {ctx.User.Mention}.\nIt was supposed to end <t:{expirationTimeUnix}:R>.")
                        .WithAllowedMentions(Mentions.None);
                    await Program.logChannel.SendMessageAsync(response);
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Raidmode is already **disabled**.");
                }
            }
        }

        public static async Task<bool> CheckRaidmodeAsync(ulong guildId)
        {
            if (!Program.db.HashExists("raidmode", guildId))
            {
                return false;
            } else
            {
                long unixExpiration = (long)Program.db.HashGet("raidmode", guildId);
                long currentUnixTime = ToUnixTimestamp(DateTime.Now);
                if (currentUnixTime >= unixExpiration)
                {
                    Program.db.HashDelete("raidmode", guildId);
                    await Program.logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Information} Raidmode was **disabled** automatically.");
                    return true;
                }
                else
                {
                    return false;
                }
            }
                
        }

        [Command("listadd")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task ListAdd(CommandContext ctx, string fileName, [RemainingText] string content)
        {
            if (Environment.GetEnvironmentVariable("CLIPTOK_GITHUB_TOKEN") == null)
            {
                ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} `CLIPTOK_GITHUB_TOKEN` was not set, so GitHub API commands cannot be used.");
                return;
            }

            if (!fileName.EndsWith(".txt"))
                fileName = fileName + ".txt";

            if (content.Length < 3)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Your input is too short, please reconsider.");
                return;
            }

            await ctx.Channel.TriggerTypingAsync();

            if (content.Substring(0, 3) == "```")
                content = content.Replace("```", "").Trim();

            string[] lines = content.Split(
                new string[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None
            );

            if (lines.Any(line => line.Length < 4))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} One of your lines is shorter than 4 characters.\nFor safety reasons I can't accept this input over command. Please submit a PR manually.");
                return;
            }

            string nameToSend = $"{ctx.User.Username}#{ctx.User.Discriminator}";

            using HttpClient httpClient = new()
            {
                BaseAddress = new Uri("https://api.github.com/")
            };

            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Cliptok", "1.0"));
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", Environment.GetEnvironmentVariable("CLIPTOK_GITHUB_TOKEN"));

            HttpRequestMessage request = new(HttpMethod.Post, $"/repos/{Program.cfgjson.GitHubWorkFlow.Repo}/actions/workflows/{Program.cfgjson.GitHubWorkFlow.WorkflowId}/dispatches");

            GitHubDispatchInputs inputs = new()
            {
                File = fileName,
                Text = content,
                User = nameToSend
            };

            GitHubDispatchBody body = new()
            {
                Ref = Program.cfgjson.GitHubWorkFlow.Ref,
                Inputs = inputs
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/vnd.github.v3+json");

            HttpResponseMessage response = await httpClient.SendAsync(request);
            string responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} The request was successful.\n" +
                    $"You should see the result in <#{Program.cfgjson.HomeChannel}> soon or can check at <https://github.com/{Program.cfgjson.GitHubWorkFlow.Repo}/actions>");
            else
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} An error with code `{response.StatusCode}` was returned when trying to request the Action run.\n" +
                    $"Body: ```json\n{responseText}```");
        }

        public class GitHubDispatchBody
        {
            [JsonProperty("ref")]
            public string Ref { get; set; }

            [JsonProperty("inputs")]
            public GitHubDispatchInputs Inputs { get; set; }
        }

        public class GitHubDispatchInputs
        {
            [JsonProperty("file")]
            public string File { get; set; }

            [JsonProperty("text")]
            public string Text { get; set; }

            [JsonProperty("user")]
            public string User { get; set; }
        }

    }
}
