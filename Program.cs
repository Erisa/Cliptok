using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using MicrosoftBot.Modules;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MicrosoftBot
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
        public static Dictionary<string, string[]> wordLists = new Dictionary<string,string[]>();


        static void Main(string[] args)
        {
            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
        } 

        static async Task MainAsync(string[] _)
        {
            var json = "";
            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);

            var keys = cfgjson.WordListList.Keys;
            foreach (string key in keys)
            {
                string reason = cfgjson.WordListList[key];
                var listOutput = File.ReadAllLines($"Lists/{key}");
                wordLists[reason] = listOutput;
            }

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
                Token = cfgjson.Core.Token,
                TokenType = TokenType.Bot,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug,
                Intents = DiscordIntents.All
            });

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            async Task OnReady(DiscordClient client, ReadyEventArgs e)
            {
                Console.WriteLine($"Logged in as {client.CurrentUser.Username}#{client.CurrentUser.Discriminator}");
                logChannel = await discord.GetChannelAsync(cfgjson.LogChannel);
                Mutes.CheckMutesAsync();
                ModCmds.CheckBansAsync();
            }

            async Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
            {
                if (e.Channel.IsPrivate || e.Guild.Id != cfgjson.ServerID || e.Author.IsBot)
                    return;

                if (processedMessages.Contains(e.Message.Id))
                {
                    return;
                } else
                {
                    processedMessages.Add(e.Message.Id);
                }

                DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
                if (Warnings.GetPermLevel(member) >= ServerPermLevel.TrialMod)
                {
                    return;
                }


                bool match = false;
                cfgjson.RestrictedWords.ForEach(async delegate (string wordToCheck)
                {
                    if (e.Message.Content.ToLower().Contains(wordToCheck))
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
                            logChannel.SendMessageAsync($"{cfgjson.Emoji.Denied} Deleted infringing message by {e.Author.Mention} in {e.Channel.Mention}:", false, embed);
                        }
                        catch
                        {
                            // still warn anyway
                        }

                        match = true;
                        string reason = "Use of restricted word(s)";
                        DiscordMessage msg = await e.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {e.Message.Author.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                        await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                        return;
                    }

                });

                var wordListKeys = wordLists.Keys;
                foreach (string key in wordListKeys)
                {
                    foreach (string word in wordLists[key])
                    {
                        if (word != "" && word != "" &&  e.Message.Content.ToLower().Contains(word))
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
                                logChannel.SendMessageAsync($"{cfgjson.Emoji.Denied} Deleted infringing message by {e.Author.Mention} in {e.Channel.Mention}:", false, embed);
                            }
                            catch
                            {
                                // still warn anyway
                            }

                            match = true;
                            string reason = key;
                            DiscordMessage msg = await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Denied} {e.Message.Author.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                            Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                            return;
                        }
                    }
                    if (match)
                        return;
                }

                if (match)
                    return;

                if (e.Message.MentionedUsers.Count >= cfgjson.MassMentionThreshold)
                {
                    DiscordChannel logChannel = await discord.GetChannelAsync(Program.cfgjson.LogChannel);
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
                        logChannel.SendMessageAsync($"{cfgjson.Emoji.Denied} Deleted infringing message by {e.Author.Mention} in {e.Channel.Mention}:", false, embed);

                    }
                    catch
                    {
                        // still warn anyway
                    }

                    string reason = "Mass mentions";
                    DiscordMessage msg = await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Denied} {e.Message.Author.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                    await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                    return;
                }
                else if (Warnings.GetPermLevel(member) < (ServerPermLevel)cfgjson.InviteTierRequirement)
                {
                    string inviteExclusion = "microsoft";
                    if (cfgjson.InviteExclusion != null)
                        inviteExclusion = cfgjson.InviteExclusion;

                    string checkedMessage = e.Message.Content.Replace($"discord.gg/{inviteExclusion}", "").Replace($"discord.com/invite/{inviteExclusion}", "");

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
                            logChannel.SendMessageAsync($"{cfgjson.Emoji.Denied} Deleted infringing message by {e.Author.Mention} in {e.Channel.Mention}:", false, embed);

                        }
                        catch
                        {
                            // still warn anyway
                        }
                        string reason = "Sent an invite";
                        DiscordMessage msg = await e.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {e.Message.Author.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                        await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                        return;
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
                    await e.Member.GrantRoleAsync(mutedRole);
                }
            }

            discord.Ready += OnReady;
            discord.MessageCreated += MessageCreated;
            discord.GuildMemberAdded += GuildMemberAdded;


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
                await Task.Delay(60000);
                Mutes.CheckMutesAsync();
                ModCmds.CheckBansAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            }

        }
    }


}
