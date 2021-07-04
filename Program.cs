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
using System.Threading.Tasks;

namespace Cliptok
{
    class Program : BaseCommandModule
    {
        public static DiscordClient discord;
        static CommandsNextExtension commands;
        public static Random rnd = new();
        public static ConfigJson cfgjson;
        public static ConnectionMultiplexer redis;
        public static IDatabase db;

        public static string[] badUsernames;
        public static List<ulong> autoBannedUsersCache = new();
        public static DiscordChannel logChannel;
        public static DiscordChannel badMsgLog;


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
                }
                else
                {
                    commitHash = "dev";
                }

                if (File.Exists("CommitMessage.txt"))
                {
                    using var sr = new StreamReader("CommitMessage.txt");
                    commitMessage = sr.ReadToEnd();
                }
                else
                {
                    commitMessage = "N/A (Bot was built for Windows)";
                }

                if (File.Exists("CommitTime.txt"))
                {
                    using var sr = new StreamReader("CommitTime.txt");
                    commitTime = sr.ReadToEnd();
                }
                else
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
                        IEnumerable<ulong> enumerable = autoBannedUsersCache.Append(member.Id);
                        var guild = await discord.GetGuildAsync(cfgjson.ServerID);
                        await Bans.BanFromServerAsync(member.Id, "Automatic ban for matching patterns of common bot accounts. Please appeal if you are a human.", discord.CurrentUser.Id, guild, 7, null, default, true);
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
                CheckAndDehoistMemberAsync(e.Member); ;
            }

            async Task GuildMemberUpdated(DiscordClient client, GuildMemberUpdateEventArgs e)
            {
                var muteRole = e.Guild.GetRole(cfgjson.MutedRole);
                var userMute = await db.HashGetAsync("mutes", e.Member.Id);

                if (e.Member.Roles.Contains(muteRole) && userMute.IsNull)
                {
                    MemberPunishment newMute = new()
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

            async Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
            {
                await MessageEvent.MessageHandlerAsync(client, e.Message);
            }

            async Task MessageUpdated(DiscordClient client, MessageUpdateEventArgs e)
            {
                await MessageEvent.MessageHandlerAsync(client, e.Message, true);
            }

            discord.Ready += OnReady;
            discord.MessageCreated += MessageCreated;
            discord.MessageUpdated += MessageUpdated;
            discord.GuildMemberAdded += GuildMemberAdded;
            discord.MessageReactionAdded += OnReaction;
            discord.GuildMemberUpdated += GuildMemberUpdated;
            discord.UserUpdated += UserUpdated;

            commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = cfgjson.Core.Prefixes
            });

            commands.RegisterCommands<Warnings>();
            commands.RegisterCommands<MuteCmds>();
            commands.RegisterCommands<UserRoleCmds>();
            commands.RegisterCommands<ModCmds>();
            commands.RegisterCommands<Lockdown>();
            commands.RegisterCommands<Bans>();
            commands.RegisterCommands<Reports>();

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
