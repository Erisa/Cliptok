using Cliptok.Helpers;
using Cliptok.Modules;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Logging;
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
        internal static EventId CliptokEventID { get; } = new EventId(1000, "Cliptok");

        public static string[] avatars;

        public static string[] badUsernames;
        public static List<ulong> autoBannedUsersCache = new();
        public static DiscordChannel logChannel;
        public static DiscordChannel userLogChannel;
        public static DiscordChannel badMsgLog;

        public static Random rand = new Random();
        public static HasteBinClient hasteUploader;

        public static async Task<bool> CheckAndDehoistMemberAsync(DiscordMember targetMember)
        {

            if (
                !(
                    targetMember.DisplayName[0] != ModCmds.dehoistCharacter
                    && (
                        cfgjson.AutoDehoistCharacters.Contains(targetMember.DisplayName[0])
                        || (targetMember.Nickname != null && targetMember.Nickname[0] != targetMember.Username[0] && cfgjson.SecondaryAutoDehoistCharacters.Contains(targetMember.Nickname[0]))
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

            hasteUploader = new HasteBinClient(cfgjson.HastebinEndpoint);

            var keys = cfgjson.WordListList.Keys;
            foreach (string key in keys)
            {
                var listOutput = File.ReadAllLines($"Lists/{key}");
                cfgjson.WordListList[key].Words = listOutput;
            }

            if (File.Exists("Lists/usernames.txt"))
                badUsernames = File.ReadAllLines("Lists/usernames.txt");
            else
                badUsernames = new string[0];

            avatars = File.ReadAllLines("Lists/avatars.txt");

            if (Environment.GetEnvironmentVariable("CLIPTOK_TOKEN") != null)
                token = Environment.GetEnvironmentVariable("CLIPTOK_TOKEN");
            else
                token = cfgjson.Core.Token;

            if (Environment.GetEnvironmentVariable("REDIS_URL") != null)
                redis = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS_URL"));
            else
            {
                string redisHost;
                if (Environment.GetEnvironmentVariable("REDIS_DOCKER_OVERRIDE") != null)
                    redisHost = "redis";
                else
                    redisHost = cfgjson.Redis.Host;
                redis = ConnectionMultiplexer.Connect($"{redisHost}:{cfgjson.Redis.Port}");
            }

            db = redis.GetDatabase();

            // Migration away from a broken attempt at a key in the past.
            db.KeyDelete("messages");

            discord = new DiscordClient(new DiscordConfiguration
            {
                Token = token,
                TokenType = TokenType.Bot,
                MinimumLogLevel = LogLevel.Information,
                Intents = DiscordIntents.All
            });

            var slash = discord.UseSlashCommands();
            slash.SlashCommandErrored += async (s, e) =>
            {
                if (e.Exception is SlashExecutionChecksFailedException slex)
                {
                    foreach (var check in slex.FailedChecks)
                        if (check is SlashRequireHomeserverPermAttribute att && e.Context.CommandName != "edit")
                        {
                            var level = Warnings.GetPermLevel(e.Context.Member);
                            var levelText = level.ToString();
                            if (level == ServerPermLevel.nothing && rand.Next(1, 100) == 69)
                                levelText = $"naught but a thing, my dear human. Congratulations, you win {Program.rand.Next(1, 10)} bonus points.";

                            await e.Context.CreateResponseAsync(
                                InteractionResponseType.ChannelMessageWithSource,
                                new DiscordInteractionResponseBuilder().WithContent(
                                    $"{cfgjson.Emoji.NoPermissions} Invalid permission level to use command **{e.Context.CommandName}**!\n" +
                                    $"Required: `{att.TargetLvl}`\n" +
                                    $"You have: `{Warnings.GetPermLevel(e.Context.Member)}`")
                                    .AsEphemeral(true)
                                );
                        }
                }
            };

            Task ClientError(DiscordClient client, ClientErrorEventArgs e)
            {
                client.Logger.LogError(CliptokEventID, e.Exception, "Client threw an exception");
                return Task.CompletedTask;
            }

            slash.RegisterCommands<SlashCommands>(cfgjson.ServerID);

            async Task OnReaction(DiscordClient client, MessageReactionAddEventArgs e)
            {
                Task.Run(async () =>
                {
                    if (e.Emoji.Id != cfgjson.HeartosoftId || e.Channel.IsPrivate || e.Guild.Id != cfgjson.ServerID)
                        return;

                    bool handled = false;

                    DiscordMessage targetMessage = await e.Channel.GetMessageAsync(e.Message.Id);

                    DiscordEmoji noHeartosoft = await e.Guild.GetEmojiAsync(cfgjson.NoHeartosoftId);

                    await Task.Delay(1000);

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
                });
            }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            async Task OnReady(DiscordClient client, ReadyEventArgs e)
            {
                Task.Run(async () =>
                {
                    Console.WriteLine($"Logged in as {client.CurrentUser.Username}#{client.CurrentUser.Discriminator}");
                    logChannel = await discord.GetChannelAsync(cfgjson.LogChannel);
                    userLogChannel = await discord.GetChannelAsync(cfgjson.UserLogChannel);
                    badMsgLog = await discord.GetChannelAsync(cfgjson.InvestigationsChannelId);
                    Mutes.CheckMutesAsync();
                    ModCmds.CheckBansAsync();
                    ModCmds.CheckRemindersAsync();

                    string commitHash = "";
                    string commitMessage = "";
                    string commitTime = "";

                    if (File.Exists("CommitHash.txt"))
                    {
                        using var sr = new StreamReader("CommitHash.txt");
                        commitHash = sr.ReadToEnd();
                    }

                    if (Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA") != null)
                    {
                        commitHash = Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA");
                        commitHash = commitHash.Substring(0, Math.Min(commitHash.Length, 7));
                    }

                    if (string.IsNullOrWhiteSpace(commitHash))
                    {
                        commitHash = "dev";
                    }

                    if (File.Exists("CommitMessage.txt"))
                    {
                        using var sr = new StreamReader("CommitMessage.txt");
                        commitMessage = sr.ReadToEnd();
                    }

                    if (Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_MESSAGE") != null)
                    {
                        commitMessage = Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_MESSAGE");
                    }

                    if (string.IsNullOrWhiteSpace(commitMessage))
                    {
                        commitMessage = "N/A (Only available when built with Docker)";
                    }

                    if (File.Exists("CommitTime.txt"))
                    {
                        using var sr = new StreamReader("CommitTime.txt");
                        commitTime = sr.ReadToEnd();
                    }

                    if (string.IsNullOrWhiteSpace(commitTime))
                    {
                        commitTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
                    }

                    var cliptokChannel = await client.GetChannelAsync(cfgjson.HomeChannel);
                    cliptokChannel.SendMessageAsync($"{cfgjson.Emoji.Connected} {discord.CurrentUser.Username} connected successfully!\n\n" +
                        $"**Version**: `{commitHash.Trim()}`\n" +
                        $"**Version timestamp**: `{commitTime}`\n**Framework**: `{RuntimeInformation.FrameworkDescription}`\n" +
                        $"**Platform**: `{RuntimeInformation.OSDescription}`\n" +
                        $"**Library**: `DSharpPlus {discord.VersionString}`\n\n" +
                        $"Most recent commit message:\n" +
                        $"```\n" +
                        $"{commitMessage}\n" +
                        $"```");

                });
            }

            async Task UsernameCheckAsync(DiscordMember member)
            {
                Task.Run(async () =>
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
                });

            }

            async Task GuildMemberAdded(DiscordClient client, GuildMemberAddEventArgs e)
            {
                Task.Run(async () =>
                {
                    if (e.Guild.Id != cfgjson.ServerID)
                        return;

                    var builder = new DiscordEmbedBuilder()
                       .WithColor(new DiscordColor(0x3E9D28))
                       .WithTimestamp(DateTimeOffset.Now)
                       .WithThumbnail(e.Member.AvatarUrl)
                       .WithAuthor(
                           name: $"{e.Member.Username}#{e.Member.Discriminator} has joined",
                           iconUrl: e.Member.AvatarUrl
                        )
                       .AddField("User", e.Member.Mention, false)
                       .AddField("User ID", e.Member.Id.ToString(), false)
                       .AddField("Action", "Joined the server", false)
                       .WithFooter($"{client.CurrentUser.Username}JoinEvent");

                    userLogChannel.SendMessageAsync($"{cfgjson.Emoji.UserJoin} **Member joined the server!** - {e.Member.Id}", builder);

                    if (await db.HashExistsAsync("mutes", e.Member.Id))
                    {
                        // todo: store per-guild
                        DiscordRole mutedRole = e.Guild.GetRole(cfgjson.MutedRole);
                        await e.Member.GrantRoleAsync(mutedRole, "Reapplying mute: possible mute evasion.");
                    }
                    CheckAndDehoistMemberAsync(e.Member);

                    if (avatars.Contains(e.Member.AvatarHash))
                    {
                        var _ = Bans.BanSilently(e.Guild, e.Member.Id, "Secret sauce");
                        await badMsgLog.SendMessageAsync($"{cfgjson.Emoji.Banned} Raid-banned {e.Member.Mention} for matching avatar: {e.Member.AvatarUrl.Replace("1024", "128")}");
                    }

                    string banDM = $"You have been automatically banned from **{e.Guild.Name}** for matching patterns of known raiders.\n" +
                            $"Please send an appeal and you will be unbanned as soon as possible: {Program.cfgjson.AppealLink}\n" +
                            $"The requirements for appeal can be ignored in this case. Sorry for any inconvenience caused.";

                    RedisValue check;
                    foreach (var IdAutoBanSet in Program.cfgjson.AutoBanIds)
                    {
                        check = Program.db.HashGet(IdAutoBanSet.Name, e.Member.Id);
                        if (check.HasValue == true)
                        {
                            return;
                        }
                        
                        if (e.Member.Id > IdAutoBanSet.LowerBound && e.Member.Id < IdAutoBanSet.UpperBound)
                        {
                            await e.Member.SendMessageAsync(banDM);

                            await e.Member.BanAsync(7, "Matching patterns of known raiders, please unban if appealed.");

                            await badMsgLog.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} Automatically appeal-banned {e.Member.Mention} for matching the creation date of the {IdAutoBanSet.Name} DM scam raiders.");
                        }
                        
                        Program.db.HashSet(IdAutoBanSet.Name, e.Member.Id, true);
                    }

                });
            }

            async Task GuildMemberRemoved(DiscordClient client, GuildMemberRemoveEventArgs e)
            {
                Task.Run(async () =>
                {
                    if (e.Guild.Id != cfgjson.ServerID)
                        return;

                    var muteRole = e.Guild.GetRole(cfgjson.MutedRole);
                    var userMute = await db.HashGetAsync("mutes", e.Member.Id);

                    if (!userMute.IsNull && !e.Member.Roles.Contains(muteRole))
                        db.HashDeleteAsync("mutes", e.Member.Id);

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

                    string rolesStr = "None";

                    if (e.Member.Roles.Count() != 0)
                    {
                        rolesStr = "";

                        foreach (DiscordRole role in e.Member.Roles.OrderBy(x => x.Position).Reverse())
                        {
                            rolesStr += role.Mention + " ";
                        }
                    }

                    var builder = new DiscordEmbedBuilder()
                        .WithColor(new DiscordColor(0xBA4119))
                        .WithTimestamp(DateTimeOffset.Now)
                        .WithThumbnail(e.Member.AvatarUrl)
                        .WithAuthor(
                            name: $"{e.Member.Username}#{e.Member.Discriminator} has left",
                            iconUrl: e.Member.AvatarUrl
                         )
                        .AddField("User", e.Member.Mention, false)
                        .AddField("User ID", e.Member.Id.ToString(), false)
                        .AddField("Action", "Left the server", false)
                        .AddField("Roles", rolesStr)
                        .WithFooter($"{client.CurrentUser.Username}LeaveEvent");

                    userLogChannel.SendMessageAsync($"{cfgjson.Emoji.UserLeave} **Member left the server!** - {e.Member.Id}", builder);
                });
            }

            async Task GuildMemberUpdated(DiscordClient client, GuildMemberUpdateEventArgs e)
            {
                Task.Run(async () =>
                {
                    var muteRole = e.Guild.GetRole(cfgjson.MutedRole);
                    var userMute = await db.HashGetAsync("mutes", e.Member.Id);

                    // If they're externally unmuted, untrack it?
                    // But not if they just joined.
                    var currentTime = DateTime.Now;
                    var joinTime = e.Member.JoinedAt.DateTime;
                    var differrence = currentTime.Subtract(joinTime).TotalSeconds;
                    if (differrence > 10 && !userMute.IsNull && !e.Member.Roles.Contains(muteRole))
                        db.HashDeleteAsync("mutes", e.Member.Id);

                    CheckAndDehoistMemberAsync(e.Member);
                    UsernameCheckAsync(e.Member);
                }
                );
            }

            async Task UserUpdated(DiscordClient client, UserUpdateEventArgs e)
            {
                Task.Run(async () =>
                {
                    var guild = await client.GetGuildAsync(cfgjson.ServerID);
                    var member = await guild.GetMemberAsync(e.UserAfter.Id);

                    CheckAndDehoistMemberAsync(member);
                    UsernameCheckAsync(member);
                });
            }

            async Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
            {
                MessageEvent.MessageHandlerAsync(client, e.Message, e.Channel);
            }

            async Task MessageUpdated(DiscordClient client, MessageUpdateEventArgs e)
            {
                MessageEvent.MessageHandlerAsync(client, e.Message, e.Channel, true);
            }

            async Task CommandsNextService_CommandErrored(CommandsNextExtension cnext, CommandErrorEventArgs e)
            {
                if (e.Exception is CommandNotFoundException && (e.Command == null || e.Command.QualifiedName != "help"))
                    return;

                // avoid conflicts with modmaail
                if (e.Command.QualifiedName == "edit")
                    return;

                e.Context.Client.Logger.LogError(CliptokEventID, e.Exception, "Exception occurred during {0}'s invocation of '{1}'", e.Context.User.Username, e.Context.Command.QualifiedName);

                var exs = new List<Exception>();
                if (e.Exception is AggregateException ae)
                    exs.AddRange(ae.InnerExceptions);
                else
                    exs.Add(e.Exception);

                foreach (var ex in exs)
                {
                    if (ex is CommandNotFoundException && (e.Command == null || e.Command.QualifiedName != "help"))
                        return;

                    if (ex is ChecksFailedException && (e.Command.Name != "help"))
                        return;

                    var embed = new DiscordEmbedBuilder
                    {
                        Color = new DiscordColor("#FF0000"),
                        Title = "An exception occurred when executing a command",
                        Description = $"{cfgjson.Emoji.BSOD} `{e.Exception.GetType()}` occurred when executing `{e.Command.QualifiedName}`.",
                        Timestamp = DateTime.UtcNow
                    };
                    embed.WithFooter(discord.CurrentUser.Username, discord.CurrentUser.AvatarUrl)
                        .AddField("Message", ex.Message);
                    if (e.Exception is System.ArgumentException)
                        embed.AddField("Note", "This usually means that you used the command incorrectly.\n" +
                            "Please double-check how to use this command.");
                    await e.Context.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
                }
            }

            Task Discord_ThreadCreated(DiscordClient client, ThreadCreateEventArgs e)
            {
                client.Logger.LogDebug(eventId: CliptokEventID, $"Thread created in {e.Guild.Name}. Thread Name: {e.Thread.Name}");
                return Task.CompletedTask;
            }

            Task Discord_ThreadUpdated(DiscordClient client, ThreadUpdateEventArgs e)
            {
                client.Logger.LogDebug(eventId: CliptokEventID, $"Thread updated in {e.Guild.Name}. New Thread Name: {e.ThreadAfter.Name}");
                return Task.CompletedTask;
            }

            Task Discord_ThreadDeleted(DiscordClient client, ThreadDeleteEventArgs e)
            {
                client.Logger.LogDebug(eventId: CliptokEventID, $"Thread deleted in {e.Guild.Name}. Thread Name: {e.Thread.Name ?? "Unknown"}");
                return Task.CompletedTask;
            }

            Task Discord_ThreadListSynced(DiscordClient client, ThreadListSyncEventArgs e)
            {
                client.Logger.LogDebug(eventId: CliptokEventID, $"Threads synced in {e.Guild.Name}.");
                return Task.CompletedTask;
            }

            Task Discord_ThreadMemberUpdated(DiscordClient client, ThreadMemberUpdateEventArgs e)
            {
                client.Logger.LogDebug(eventId: CliptokEventID, $"Thread member updated.");
                Console.WriteLine($"Discord_ThreadMemberUpdated fired for thread {e.ThreadMember.ThreadId}. User ID {e.ThreadMember.Id}.");
                return Task.CompletedTask;
            }

            Task Discord_ThreadMembersUpdated(DiscordClient client, ThreadMembersUpdateEventArgs e)
            {
                client.Logger.LogDebug(eventId: CliptokEventID, $"Thread members updated in {e.Guild.Name}.");
                return Task.CompletedTask;
            }

            discord.Ready += OnReady;
            discord.MessageCreated += MessageCreated;
            discord.MessageUpdated += MessageUpdated;
            discord.GuildMemberAdded += GuildMemberAdded;
            discord.GuildMemberRemoved += GuildMemberRemoved;
            discord.MessageReactionAdded += OnReaction;
            discord.GuildMemberUpdated += GuildMemberUpdated;
            discord.UserUpdated += UserUpdated;
            discord.ClientErrored += ClientError;
            discord.ThreadCreated += Discord_ThreadCreated;
            discord.ThreadUpdated += Discord_ThreadUpdated;
            discord.ThreadDeleted += Discord_ThreadDeleted;
            discord.ThreadListSynced += Discord_ThreadListSynced;
            discord.ThreadMemberUpdated += Discord_ThreadMemberUpdated;
            discord.ThreadMembersUpdated += Discord_ThreadMembersUpdated;

            commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = cfgjson.Core.Prefixes
            }); ;

            commands.RegisterCommands<Warnings>();
            commands.RegisterCommands<MuteCmds>();
            commands.RegisterCommands<UserRoleCmds>();
            commands.RegisterCommands<ModCmds>();
            commands.RegisterCommands<Lockdown>();
            commands.RegisterCommands<Bans>();
            commands.CommandErrored += CommandsNextService_CommandErrored;

            await discord.ConnectAsync();

            while (true)
            {
                await Task.Delay(10000);
                try
                {
                    Mutes.CheckMutesAsync();
                    ModCmds.CheckBansAsync();
                    ModCmds.CheckRemindersAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

        }
    }
}
