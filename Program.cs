using Cliptok.Modules;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
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
        public static List<ulong> processedMessages = new List<ulong>();
        public static Dictionary<string, string[]> wordLists = new Dictionary<string, string[]>();
        readonly static Regex emoji_rx = new Regex("((\u203c|\u2049|\u2139|[\u2194-\u2199]|[\u21a9-\u21aa]|[\u231a-\u231b]|\u23cf|[\u23e9-\u23f3]|[\u23f8-\u23fa]|\u24c2|[\u25aa–\u25ab]|\u25b6|\u25c0|[\u25fb–\u25fe]|[\u2600–\u2604]|\u260E|\u2611|[\u2614–\u2615]|\u2618|\u261D|\u2620|[\u2622–\u2623]|\u2626|\u262A|[\u262E–\u262F]|[\u2638–\u263A]|\u2640|\u2642|[\u2648–\u2653]|[\u265F–\u2660]|\u2663|[\u2665–\u2666]|\u2668|\u267B|[\u267E–\u267F]|[\u2692–\u2697]|\u2699|[\u269B–\u269C]|[\u26A0–\u26A1]|\u26A7|[\u26AA–\u26AB]|[\u26B0–\u26B1]|[\u26BD–\u26BE]|[\u26C4–\u26C5]|\u26C8|[\u26CE–\u26CF]|\u26D1|[\u26D3–\u26D4]|[\u26E9–\u26EA]|[\u26F0–\u26F5]|[\u26F7–\u26FA]|\u26FD|\u2702|\u2705|[\u2708–\u270D]|\u270F|\u2712|\u2714|\u2716|\u271D|\u2721|\u2728|[\u2733–\u2734]|\u2744|\u2747|\u274C|\u274E|[\u2753–\u2755]|\u2757|[\u2763–\u2764]|[\u2795–\u2797]|\u27A1|\u27B0|\u27BF|[\u2934–\u2935]|[\u2B05–\u2B07]|[\u2B1B–\u2B1C]|\u2B50|\u2B55|\u3030|\u303D|\u3297|\u3299|\ud83c[\ud000-\udfff]|\ud83d[\ud000-\udfff]|\ud83e[\ud000-\udfff]))|(<a{0,1}:[a-zA-Z0-9_.]{2,32}:[0-9]+>)");
        readonly static Regex animoji_rx = new Regex("(<a:.{2,32}:[0-9]{1,32}>)");
        readonly static Regex id_rx = new Regex("([0-9]{1,32}>)");

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
                Mutes.CheckMutesAsync();
                ModCmds.CheckBansAsync();
                ModCmds.CheckRemindersAsync();

                string commitHash = "aaaaaaa";
                string commitMessage = "N/A";
                string commitTime = "0000-00-00 00:00:00 +0000";
                if (File.Exists("CommitHash.txt"))
                {
                    using var sr = new StreamReader("CommitHash.txt");
                    commitHash = sr.ReadToEnd();
                }

                if (File.Exists("CommitMessage.txt"))
                {
                    using var sr = new StreamReader("CommitMessage.txt");
                    commitMessage = sr.ReadToEnd();
                }

                if (File.Exists("CommitTime.txt"))
                {
                    using var sr = new StreamReader("CommitTime.txt");
                    commitTime = sr.ReadToEnd();
                }

                var cliptokChannel = await client.GetChannelAsync(cfgjson.HomeChannel);
                cliptokChannel.SendMessageAsync($"{cfgjson.Emoji.Connected} Cliptok connected successfully!\n\n" +
                    $"**Version**: `{commitHash}`\n" +
                    $"**Version timestamp**: `{commitTime}`\n**Framework**: `{RuntimeInformation.FrameworkDescription}`\n**Platform**: `{RuntimeInformation.OSDescription}`\n\n" +
                    $"Most recent commit message:\n" +
                    $"```\n" +
                    $"{commitMessage}\n" +
                    $"```");
            }

            async Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
            {
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


                bool match = false;

                // Matching word list
                var wordListKeys = cfgjson.WordListList.Keys;
                foreach (string key in wordListKeys)
                {
                    if (CheckForNaughtyWords(e.Message.Content.ToLower(), cfgjson.WordListList[key]))
                    {
                        try
                        {
                            e.Message.DeleteAsync();
                            DiscordChannel logChannel = await discord.GetChannelAsync(Program.cfgjson.LogChannel);
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

                        match = true;
                        string reason = cfgjson.WordListList[key].Reason;
                        DiscordMessage msg = await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Denied} {e.Message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                        Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
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
                    DiscordChannel logChannel = await discord.GetChannelAsync(cfgjson.LogChannel);
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

                    string reason = "Mass mentions";
                    DiscordMessage msg = await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Denied} {e.Message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                    await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                    return;
                }

                // Unapproved invites
                if (Warnings.GetPermLevel(member) < (ServerPermLevel)cfgjson.InviteTierRequirement)
                {
                    string inviteExclusion = cfgjson.InviteExclusion;
                    if (cfgjson.InviteExclusion != null)
                        inviteExclusion = cfgjson.InviteExclusion;

                    string checkedMessage = e.Message.Content.Replace($"discord.gg/{inviteExclusion}", "").Replace($"discord.com/invite/{inviteExclusion}", "").Replace('\\', '/');

                    if (checkedMessage.Contains("discord.gg/") || checkedMessage.Contains("discord.com/invite/"))
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
                        string reason = "Sent an invite";
                        DiscordMessage msg = await e.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {e.Message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                        await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
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
                        e.Message.DeleteAsync();

                        if (Warnings.GetPermLevel(member) == ServerPermLevel.nothing && !db.HashExists("emojiPardoned", e.Message.Author.Id.ToString()))
                        {
                            await db.HashSetAsync("emojiPardoned", member.Id.ToString(), false);
                            await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Information} {e.Author.Mention}, if you want to play around with lots of emoji, please use <#{cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.");
                            return;
                        }

                        string reason = "Mass emoji";
                        string output = $"{Program.cfgjson.Emoji.Denied} {e.Message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**";
                        if (!db.HashExists("emojiPardoned", e.Author.Id.ToString()) || db.HashGet("emojiPardoned", e.Message.Author.Id.ToString()) == false)
                        {
                            output += $"\nIf you want to play around with lots of emoji, please use <#{cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.";
                            await db.HashSetAsync("emojiPardoned", member.Id.ToString(), true);
                        }

                        DiscordMessage msg = await e.Channel.SendMessageAsync(output);
                        await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                        return;
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
            }

            discord.Ready += OnReady;
            discord.MessageCreated += MessageCreated;
            discord.GuildMemberAdded += GuildMemberAdded;
            discord.MessageReactionAdded += OnReaction;

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
