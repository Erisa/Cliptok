using Cliptok.Modules;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cliptok
{
    class Program : BaseCommandModule
    {
        public static DiscordClient discord;
        static CommandsNextExtension commands;
        public static Random rnd = new Random();
        public static ConfigJson cfgjson;
        public static ConnectionMultiplexer redis;
        public static IDatabase db;
        public static DiscordChannel logChannel;
        public static DiscordChannel badMsgLog;
        public static List<ulong> processedMessages = new List<ulong>();
        public static Dictionary<string, string[]> wordLists = new Dictionary<string, string[]>();
        public static string[] badUsernames;
        readonly static Regex emoji_rx = new Regex("((\u203c|\u2049|\u2139|[\u2194-\u2199]|[\u21a9-\u21aa]|[\u231a-\u231b]|\u23cf|[\u23e9-\u23f3]|[\u23f8-\u23fa]|\u24c2|[\u25aa–\u25ab]|\u25b6|\u25c0|[\u25fb–\u25fe]|[\u2600–\u2604]|\u260E|\u2611|[\u2614–\u2615]|\u2618|\u261D|\u2620|[\u2622–\u2623]|\u2626|\u262A|[\u262E–\u262F]|[\u2638–\u263A]|\u2640|\u2642|[\u2648–\u2653]|[\u265F–\u2660]|\u2663|[\u2665–\u2666]|\u2668|\u267B|[\u267E–\u267F]|[\u2692–\u2697]|\u2699|[\u269B–\u269C]|[\u26A0–\u26A1]|\u26A7|[\u26AA–\u26AB]|[\u26B0–\u26B1]|[\u26BD–\u26BE]|[\u26C4–\u26C5]|\u26C8|[\u26CE–\u26CF]|\u26D1|[\u26D3–\u26D4]|[\u26E9–\u26EA]|[\u26F0–\u26F5]|[\u26F7–\u26FA]|\u26FD|\u2702|\u2705|[\u2708–\u270D]|\u270F|\u2712|\u2714|\u2716|\u271D|\u2721|\u2728|[\u2733–\u2734]|\u2744|\u2747|\u274C|\u274E|[\u2753–\u2755]|\u2757|[\u2763–\u2764]|[\u2795–\u2797]|\u27A1|\u27B0|\u27BF|[\u2934–\u2935]|[\u2B05–\u2B07]|[\u2B1B–\u2B1C]|\u2B50|\u2B55|\u3030|\u303D|\u3297|\u3299|\ud83c[\ud000-\udfff]|\ud83d[\ud000-\udfff]|\ud83e[\ud000-\udfff]))|(<a{0,1}:[a-zA-Z0-9_.]{2,32}:[0-9]+>)");
        readonly static Regex animoji_rx = new Regex("(<a:.{2,32}:[0-9]{1,32}>)");
        readonly static Regex id_rx = new Regex("([0-9]{1,32}>)");
        readonly static Regex modmaiL_rx = new Regex("User ID: ([0-9]+)");
        public static Dictionary<ulong, DateTime> supportRatelimit = new Dictionary<ulong, DateTime>();
        public static List<ulong> autoBannedUsersCache = new List<ulong>();

        static bool CheckForNaughtyWords(string input, WordListJson naughtyWordList)
        {
            string[] naughtyWords = naughtyWordList.Words;
            if (naughtyWordList.WholeWord)
            {
                input = input.Replace("\'", " ")
                    .Replace("-", " ")
                    .Replace("_", " ")
                    .Replace(".", " ")
                    .Replace(":", " ")
                    .Replace("/", " ")
                    .Replace(",", " ");

                char[] tempArray = input.ToCharArray();

                tempArray = Array.FindAll(tempArray, c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c));
                input = new string(tempArray);

                string[] arrayOfWords = input.Split(' ');

                for (int i = 0; i < arrayOfWords.Length; i++)
                {
                    bool isNaughty = false;
                    foreach (string naughty in naughtyWords)
                    {
                        string distinctString = new string(arrayOfWords[i].Replace(naughty, "#").Distinct().ToArray());
                        if (distinctString.Length <= 3 && arrayOfWords[i].Contains(naughty))
                        {
                            if (distinctString.Length == 1)
                            {
                                isNaughty = true;

                            }
                            else if (distinctString.Length == 2 && (naughty.EndsWith(distinctString[1].ToString()) || naughty.StartsWith(distinctString[0].ToString())))
                            {
                                isNaughty = true;
                            }
                            else if (distinctString.Length == 3 && naughty.EndsWith(distinctString[1].ToString()) && naughty.StartsWith(distinctString[0].ToString()))
                            {
                                isNaughty = true;
                            }
                        }
                        if (arrayOfWords[i] == "")
                        {
                            isNaughty = false;
                        }
                    }
                    if (isNaughty)
                    {
                        return true;
                    }
                }
                return false;
            }
            else
            {
                foreach (string word in naughtyWords)
                {
                    if (input.Contains(word))
                    {
                        return true;
                    }
                }
                return false;
            }

        }


        public static async Task<bool> CheckAndDehoistMemberAsync(DiscordMember targetMember)
        {
            
            if (
                !(
                    targetMember.DisplayName[0] != ModCmds.dehoistCharacter
                    && (
                        cfgjson.AutoDehoistCharacters.Contains(targetMember.DisplayName[0]) 
                        || (targetMember.Nickname != null && cfgjson.SecondaryAutoDehoistCharacters.Contains(targetMember.Nickname[0]))
                        )
                ))
            {
                return false;
            }

            try
            {
                await targetMember.ModifyAsync(a =>
                {
                    a.Nickname = ModCmds.DehoistName(targetMember.DisplayName);
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void Main(string[] args)
        {
            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] _)
        {
            string token;
            var json = "";

            string configFile = "config.json";
#if DEBUG
            configFile = "config.dev.json";
#endif

            using (var fs = File.OpenRead(configFile))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);

            var keys = cfgjson.WordListList.Keys;
            foreach (string key in keys)
            {
                var listOutput = File.ReadAllLines($"Lists/{key}");
                cfgjson.WordListList[key].Words = listOutput;
            }

            badUsernames = File.ReadAllLines($"Lists/usernames.txt");

            if (Environment.GetEnvironmentVariable("CLIPTOK_TOKEN") != null)
                token = Environment.GetEnvironmentVariable("CLIPTOK_TOKEN");
            else
                token = cfgjson.Core.Token;

            string redisHost;
            if (Environment.GetEnvironmentVariable("REDIS_DOCKER_OVERRIDE") != null)
                redisHost = "redis";
            else
                redisHost = cfgjson.Redis.Host;
            redis = ConnectionMultiplexer.Connect($"{redisHost}:{cfgjson.Redis.Port}");
            db = redis.GetDatabase();
            db.KeyDelete("messages");

            discord = new DiscordClient(new DiscordConfiguration
            {
                Token = token,
                TokenType = TokenType.Bot,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug,
                Intents = DiscordIntents.All
            });

            var slash = discord.UseSlashCommands();
            slash.RegisterCommands<SlashCommands>(cfgjson.ServerID);

            async Task OnReaction(DiscordClient client, MessageReactionAddEventArgs e)
            {
                if (e.Emoji.Id != cfgjson.HeartosoftId || e.Channel.IsPrivate || e.Guild.Id != cfgjson.ServerID)
                    return;

                bool handled = false;

                DiscordMessage targetMessage = await e.Channel.GetMessageAsync(e.Message.Id);

                DiscordEmoji noHeartosoft = await e.Guild.GetEmojiAsync(cfgjson.NoHeartosoftId);

                if (targetMessage.Author.Id == e.User.Id)
                {
                    await targetMessage.DeleteReactionAsync(e.Emoji, e.User);
                    handled = true;
                }

                foreach (string word in cfgjson.RestrictedHeartosoftPhrases)
                {
                    if (targetMessage.Content.ToLower().Contains(word))
                    {
                        if (!handled)
                            await targetMessage.DeleteReactionAsync(e.Emoji, e.User);

                        await targetMessage.CreateReactionAsync(noHeartosoft);
                        return;
                    }
                }
            }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            async Task OnReady(DiscordClient client, ReadyEventArgs e)
            {
                Console.WriteLine($"Logged in as {client.CurrentUser.Username}#{client.CurrentUser.Discriminator}");
                logChannel = await discord.GetChannelAsync(cfgjson.LogChannel);
                badMsgLog = await discord.GetChannelAsync(cfgjson.InvestigationsChannelId);
                Mutes.CheckMutesAsync();
                ModCmds.CheckBansAsync();
                ModCmds.CheckRemindersAsync();

                string commitHash;
                string commitMessage;
                string commitTime;

                if (File.Exists("CommitHash.txt"))
                {
                    using var sr = new StreamReader("CommitHash.txt");
                    commitHash = sr.ReadToEnd();
                } else
                {
                    commitHash = "dev";
                }

                if (File.Exists("CommitMessage.txt"))
                {
                    using var sr = new StreamReader("CommitMessage.txt");
                    commitMessage = sr.ReadToEnd();
                } else
                {
                    commitMessage = "N/A (Bot was built for Windows)";
                }

                if (File.Exists("CommitTime.txt"))
                {
                    using var sr = new StreamReader("CommitTime.txt");
                    commitTime = sr.ReadToEnd();
                } else
                {
                    commitTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
                }

                var cliptokChannel = await client.GetChannelAsync(cfgjson.HomeChannel);
                cliptokChannel.SendMessageAsync($"{cfgjson.Emoji.Connected} {discord.CurrentUser.Username} connected successfully!\n\n" +
                    $"**Version**: `{commitHash}`\n" +
                    $"**Version timestamp**: `{commitTime}`\n**Framework**: `{RuntimeInformation.FrameworkDescription}`\n**Platform**: `{RuntimeInformation.OSDescription}`\n\n" +
                    $"Most recent commit message:\n" +
                    $"```\n" +
                    $"{commitMessage}\n" +
                    $"```");

            }

            async Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
            { 

                if (e.Author.Id == cfgjson.ModmailUserId && e.Message.Content == "@here" && e.Message.Embeds[0].Footer.Text.Contains("User ID:")) 
                {
                    var idString = modmaiL_rx.Match(e.Channel.Topic).Groups[1].Captures[0].Value;
                    DiscordMember modmailMember = default;
                    try {
                        modmailMember = await e.Guild.GetMemberAsync(Convert.ToUInt64(idString));
                    } catch (DSharpPlus.Exceptions.NotFoundException)
                    {
                        return;
                    }

                    DiscordRole muted = e.Guild.GetRole(cfgjson.MutedRole);
                    if (modmailMember.Roles.Contains(muted))
                    {
                        await e.Channel.SendMessageAsync(null, Warnings.GenerateWarningsEmbed(modmailMember));
                    }
                    
                }

                if (e.Channel.IsPrivate || e.Guild.Id != cfgjson.ServerID || e.Author.IsBot)
                    return;

                if (processedMessages.Contains(e.Message.Id))
                {
                    return;
                }
                else
                {
                    processedMessages.Add(e.Message.Id);
                }

                DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
                if (Warnings.GetPermLevel(member) >= ServerPermLevel.TrialMod)
                {
                    return;
                }

                if (e.Message.MentionedUsers.Count > cfgjson.MassMentionBanThreshold)
                {
                    var _ = e.Message.DeleteAsync();
                    await e.Guild.BanMemberAsync(e.Author.Id, 7, $"Mentioned more thann {cfgjson.MassMentionBanThreshold} users in one message.");
                }

                bool match = false;

                // Matching word list
                var wordListKeys = cfgjson.WordListList.Keys;
                foreach (string key in wordListKeys)
                {
                    if (CheckForNaughtyWords(e.Message.Content.ToLower(), cfgjson.WordListList[key]))
                    {
                        string reason = cfgjson.WordListList[key].Reason;
                        try
                        {
                            e.Message.DeleteAsync();
                            await SendInfringingMessaageAsync(logChannel, e.Message, reason, null);
                        }
                        catch
                        {
                            // still warn anyway
                        }

                        if (key == "autoban.txt" && Warnings.GetPermLevel(member) < ServerPermLevel.Tier4)
                        {
                            var _ = e.Message.DeleteAsync();
                            await ModCmds.BanFromServerAsync(e.Author.Id, reason, discord.CurrentUser.Id, e.Guild, 0, e.Channel, default, true);
                            return;
                        }

                        match = true;
                        
                        DiscordMessage msg = await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Denied} {e.Message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                        var warning = await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                        await SendInfringingMessaageAsync(badMsgLog, e.Message, reason, warning.ContextLink);
                        return;
                    }
                    if (match)
                        return;
                }

                if (match)
                    return;

                // Mass mentions
                if (e.Message.MentionedUsers.Count >= cfgjson.MassMentionThreshold && Warnings.GetPermLevel(member) < ServerPermLevel.Tier3)
                {
                    string reason = "Mass mentions";
                    try
                    {
                        e.Message.DeleteAsync();
                        SendInfringingMessaageAsync(logChannel, e.Message, reason, null);
                    }
                    catch
                    {
                        // still warn anyway
                    }

                    DiscordMessage msg = await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Denied} {e.Message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                    var warning = await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                    await SendInfringingMessaageAsync(badMsgLog, e.Message, reason, warning.ContextLink);
                    return;
                }

                // Unapproved invites
                if (Warnings.GetPermLevel(member) < (ServerPermLevel)cfgjson.InviteTierRequirement)
                {

                    string checkedMessage = e.Message.Content.Replace('\\', '/');
                    foreach (string exclusion in cfgjson.InviteExclusion)
                    {
                        checkedMessage = checkedMessage.Replace("discord.gg/" + exclusion, "").Replace("discord.com/invite/" + exclusion, "");
                    }

                    if (checkedMessage.Contains("discord.gg/") || checkedMessage.Contains("discord.com/invite/"))
                    {
                        string reason = "Sent an unapproved invite";
                        e.Message.DeleteAsync();
                        try
                        {
                            SendInfringingMessaageAsync(logChannel, e.Message, reason, null);
                        }
                        catch
                        {
                            // still warn anyway
                        }
                        
                        DiscordMessage msg = await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Denied} {e.Message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                        var warning = await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                        await SendInfringingMessaageAsync(badMsgLog, e.Message, reason, warning.ContextLink);
                        return;
                    }

                }

                // Mass emoji
                if (!cfgjson.UnrestrictedEmojiChannels.Contains(e.Message.ChannelId) && e.Message.Content.Length >= cfgjson.MassEmojiThreshold)
                {
                    char[] tempArray = e.Message.Content.Replace("🏻", "").Replace("🏼", "").Replace("🏽", "").Replace("🏾", "").Replace("🏿", "").ToCharArray();
                    int pos = 0;
                    foreach (char c in tempArray)
                    {

                        if (c == '™' || c == '®' || c == '©')
                        {
                            tempArray[pos] = ' ';
                        }
                        if (c == '\u200d')
                        {
                            tempArray[pos] = ' ';
                            tempArray[pos + 1] = ' ';
                        }
                        ++pos;
                    }
                    string input = new string(tempArray);

                    var matches = emoji_rx.Matches(input);
                    if (matches.Count > cfgjson.MassEmojiThreshold)
                    {
                        string reason = "Mass emoji";
                        e.Message.DeleteAsync();
                        SendInfringingMessaageAsync(logChannel, e.Message, reason, null);
                        
                            if (Warnings.GetPermLevel(member) == ServerPermLevel.nothing && !db.HashExists("emojiPardoned", e.Message.Author.Id.ToString()))
                        {
                            await db.HashSetAsync("emojiPardoned", member.Id.ToString(), false);
                            await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Information} {e.Author.Mention}, if you want to play around with lots of emoji, please use <#{cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.");
                            return;
                        }

                        string output = $"{cfgjson.Emoji.Denied} {e.Message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**";
                        if (!db.HashExists("emojiPardoned", e.Author.Id.ToString()) || db.HashGet("emojiPardoned", e.Message.Author.Id.ToString()) == false)
                        {
                            output += $"\nIf you want to play around with lots of emoji, please use <#{cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.";
                            await db.HashSetAsync("emojiPardoned", member.Id.ToString(), true);
                        }

                        DiscordMessage msg = await e.Channel.SendMessageAsync(output);
                        var warning = await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                        await SendInfringingMessaageAsync(badMsgLog, e.Message, reason, warning.ContextLink);
                        return;
                    }

                    if (e.Message.Channel.Id == cfgjson.TechSupportChannel && e.Message.Content.Contains($"<@&{cfgjson.CommunityTechSupportRoleID}>")) {
                        if (supportRatelimit.ContainsKey(e.Message.Author.Id))
                        {
                            if (supportRatelimit[e.Message.Author.Id] > DateTime.Now)
                                
                                return;
                            else
                                supportRatelimit.Remove(e.Message.Author.Id);
                        }

                        supportRatelimit.Add(e.Message.Author.Id, DateTime.Now.Add(TimeSpan.FromMinutes(cfgjson.SupportRatelimitMinutes)));

                        DiscordChannel supportLogChannel = await client.GetChannelAsync(cfgjson.SupportLogChannel);
                        var embed = new DiscordEmbedBuilder()
                            .WithTimestamp(DateTime.Now)
                            .WithAuthor(e.Author.Username + '#' + e.Author.Discriminator, null, $"https://cdn.discordapp.com/avatars/{e.Author.Id}/{e.Author.AvatarHash}.png?size=128");
                       
                        var lastMsgs = await e.Channel.GetMessagesBeforeAsync(e.Message.Id, 50);
                        var msgMatch = lastMsgs.FirstOrDefault(m => m.Author.Id == e.Author.Id);

                        if (msgMatch != null)
                        {
                            embed.AddField("Previous message", Warnings.Truncate(msgMatch.Content, 1020, true));
                            if (msgMatch.Attachments.Count != 0)
                            {
                                embed.WithImageUrl(msgMatch.Attachments[0].Url);
                            }
                        }

                        embed.AddField("Current message", Warnings.Truncate(e.Message.Content, 1020));
                        if (e.Message.Attachments.Count != 0)
                        {
                            if (embed.ImageUrl == null)
                                embed.WithImageUrl(e.Message.Attachments[0].Url);
                            else
                                embed.ImageUrl = e.Message.Attachments[0].Url;
                        }

                        embed.AddField("Message Link", $"[`Jump to message`](https://discord.com/channels/{e.Guild.Id}/{e.Channel.Id}/{e.Message.Id})");
                        var logOut = await supportLogChannel.SendMessageAsync(null, embed);
                        logOut.CreateReactionAsync(DiscordEmoji.FromName(client, ":CliptokAcknowledge:", true));
                    }
                }

                // Seizure emoji
                var animatches = animoji_rx.Matches(e.Message.Content);
                if (animatches.Count > 0)
                {
                    foreach (Match dirtyid in animatches)
                    {
                        string id = id_rx.Matches(dirtyid.ToString())[0].ToString().Replace(">", "");
                        string url = "https://cdn.discordapp.com/emojis/" + id + ".gif";

                        if (SeizureDetection.GetGifProperties(url).IsSeizureInducing)
                        {
                            try
                            {
                                e.Message.DeleteAsync();
                                var embed = new DiscordEmbedBuilder()
                                    .WithDescription(e.Message.Content)
                                    .WithColor(new DiscordColor(0xf03916))
                                    .WithTimestamp(e.Message.Timestamp)
                                    .WithFooter(
                                        $"User ID: {e.Author.Id}",
                                        null
                                    )
                                    .WithAuthor(
                                        $"{e.Author.Username}#{e.Author.Discriminator} in #{e.Channel.Name}",
                                        null,
                                        e.Author.AvatarUrl
                                    );
                                logChannel.SendMessageAsync($"{cfgjson.Emoji.Denied} Deleted infringing message by {e.Author.Mention} in {e.Channel.Mention}:", embed);

                            }
                            catch
                            {
                                // still warn anyway
                            }
                            string reason = "sent a seizure-inducing emoji";
                            DiscordMessage msg = await e.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {e.Message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                            await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                            return;
                        }
                    }
                }
            }

            async Task UsernameCheckAsync(DiscordMember member)
            {
                foreach (var username in badUsernames)
                {
                    // emergency failsafe, for newlines and other mistaken entries
                    if (username.Length < 4)
                        continue;

                    if (member.Username.ToLower().Contains(username.ToLower()))
                    {
                        if (autoBannedUsersCache.Contains(member.Id))
                            break;
                        autoBannedUsersCache.Append(member.Id);
                        var guild = await discord.GetGuildAsync(cfgjson.ServerID);
                        await ModCmds.BanFromServerAsync(member.Id, "Automatic ban for matching patterns of common bot accounts. Please appeal if you are a human.", discord.CurrentUser.Id, guild, 7, null, default, true);
                        var embed = new DiscordEmbedBuilder()
                            .WithTimestamp(DateTime.Now)
                            .WithFooter($"User ID: {member.Id}", null)
                            .WithAuthor($"{member.Username}#{member.Discriminator}", null, member.AvatarUrl)
                            .AddField("Infringing name", member.Username)
                            .AddField("Matching pattern", username)
                            .WithColor(new DiscordColor(0xf03916));
                        var investigations = await discord.GetChannelAsync(cfgjson.InvestigationsChannelId);
                        await investigations.SendMessageAsync($"{cfgjson.Emoji.Banned} {member.Mention} was banned for matching blocked username patterns.", embed);
                        break;
                    }
                }
            }

            async Task SendInfringingMessaageAsync(DiscordChannel channel, DiscordMessage infringingMessage, string reason, string messageURL)
            {
                var embed = new DiscordEmbedBuilder()
                .WithDescription(infringingMessage.Content)
                .WithColor(new DiscordColor(0xf03916))
                .WithTimestamp(infringingMessage.Timestamp)
                .WithFooter(
                    $"User ID: {infringingMessage.Author.Id}",
                    null
                )
                .WithAuthor(
                    $"{infringingMessage.Author.Username}#{infringingMessage.Author.Discriminator} in #{infringingMessage.Channel.Name}",
                    null,
                    infringingMessage.Author.AvatarUrl
                )
                .AddField("Reason", reason, true);
                if (messageURL != null)
                    embed.AddField("Message link", $"[`Jump to warning`]({messageURL})", true);

                await channel.SendMessageAsync($"{cfgjson.Emoji.Denied} Deleted infringing message by {infringingMessage.Author.Mention} in {infringingMessage.Channel.Mention}:", embed);
            }

            async Task GuildMemberAdded(DiscordClient client, GuildMemberAddEventArgs e)
            {
                if (e.Guild.Id != cfgjson.ServerID)
                    return;

                if (await db.HashExistsAsync("mutes", e.Member.Id))
                {
                    // todo: store per-guild
                    DiscordRole mutedRole = e.Guild.GetRole(cfgjson.MutedRole);
                    await e.Member.GrantRoleAsync(mutedRole, "Reapplying mute: possible mute evasion.");
                }
                CheckAndDehoistMemberAsync(e.Member);;
            }

            async Task GuildMemberUpdated(DiscordClient client, GuildMemberUpdateEventArgs e)
            {
                var muteRole = e.Guild.GetRole(cfgjson.MutedRole);
                var userMute = await db.HashGetAsync("mutes", e.Member.Id);

                if (e.Member.Roles.Contains(muteRole) && userMute.IsNull)
                {
                    MemberPunishment newMute = new MemberPunishment()
                    {
                        MemberId = e.Member.Id,
                        ModId = discord.CurrentUser.Id,
                        ServerId = e.Guild.Id,
                        ExpireTime = null
                    };

                    db.HashSetAsync("mutes", e.Member.Id, JsonConvert.SerializeObject(newMute));
                }

                if (!userMute.IsNull && !e.Member.Roles.Contains(muteRole))
                    db.HashDeleteAsync("mutes", e.Member.Id);

                CheckAndDehoistMemberAsync(e.Member);
                UsernameCheckAsync(e.Member);
            }

            async Task UserUpdated(DiscordClient client, UserUpdateEventArgs e)
            {
                var guild = await client.GetGuildAsync(cfgjson.ServerID);
                var member = await guild.GetMemberAsync(e.UserAfter.Id);

                CheckAndDehoistMemberAsync(member);
                UsernameCheckAsync(member);
            }

            discord.Ready += OnReady;
            discord.MessageCreated += MessageCreated;
            discord.GuildMemberAdded += GuildMemberAdded;
            discord.MessageReactionAdded += OnReaction;
            discord.GuildMemberUpdated += GuildMemberUpdated;
            discord.UserUpdated += UserUpdated;

            commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = cfgjson.Core.Prefixes
            }); ;

            commands.RegisterCommands<Warnings>();
            commands.RegisterCommands<MuteCmds>();
            commands.RegisterCommands<UserRoleCmds>();
            commands.RegisterCommands<ModCmds>();

            await discord.ConnectAsync();

            while (true)
            {
                await Task.Delay(10000);
                Mutes.CheckMutesAsync();
                ModCmds.CheckBansAsync();
                ModCmds.CheckRemindersAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            }

        }
    }


}
