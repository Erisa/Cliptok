using DSharpPlus.Extensions;
using DSharpPlus.Net.Gateway;
using DSharpPlus.SlashCommands;
using Serilog.Sinks.Grafana.Loki;
using System.Reflection;

namespace Cliptok
{
    public class AvatarResponseBody
    {
        [JsonProperty("matched")]
        public bool Matched { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }
    }

    class GatewayController : IGatewayController
    {
        public async Task HeartbeatedAsync(IGatewayClient client)
        {
            HeartbeatEvent.OnHeartbeat(client);
        }

        public async Task ZombiedAsync(IGatewayClient client)
        {
            Program.discord.Logger.LogWarning("Gateway entered zombied state. Attempted to reconnect.");
            await client.ReconnectAsync();
        }

        public async Task ReconnectRequestedAsync(IGatewayClient _) { }
        public async Task ReconnectFailedAsync(IGatewayClient _) { }
        public async Task SessionInvalidatedAsync(IGatewayClient _) { }
        public async Task ResumeAttemptedAsync(IGatewayClient _) { }

    }

    class Program : BaseCommandModule
    {
        public static DiscordClient discord;
        static CommandsNextExtension commands;
        public static Random rnd = new();
        public static ConfigJson cfgjson;
        public static ConnectionMultiplexer redis;
        public static IDatabase db;
        internal static EventId CliptokEventID { get; } = new EventId(1000, "Cliptok");
        internal static EventId LogChannelErrorID { get; } = new EventId(1001, "LogChannelError");


        public static string[] avatars;

        public static string[] badUsernames;
        public static List<ulong> autoBannedUsersCache = new();
        public static DiscordGuild homeGuild;

        public static Random rand = new();
        public static HasteBinClient hasteUploader;

        static public readonly HttpClient httpClient = new();

        public static List<ServerApiResponseJson> serverApiList = new();
        
        public static DiscordChannel ForumChannelAutoWarnFallbackChannel;

        public static void UpdateLists()
        {
            foreach (var list in cfgjson.WordListList)
            {
                var listOutput = File.ReadAllLines($"Lists/{list.Name}");
                cfgjson.WordListList[cfgjson.WordListList.FindIndex(a => a.Name == list.Name)].Words = listOutput;
            }
        }

        static async Task Main(string[] _)
        {
            Console.OutputEncoding = Encoding.UTF8;

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var logFormat = "[{Timestamp:yyyy-MM-dd HH:mm:ss zzz}] [{Level}] {Message}{NewLine}{Exception}";

            var loggerConfig = new LoggerConfiguration()
                .WriteTo.Logger(lc =>
                    lc.Filter.ByExcluding("EventId.Id = 1001")
                .WriteTo.DiscordSink(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information, outputTemplate: logFormat)
                )
                .WriteTo.Console(outputTemplate: logFormat, theme: AnsiConsoleTheme.Literate);

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

            switch (cfgjson.LogLevel)
            {
                case Level.Information:
                    loggerConfig.MinimumLevel.Information();
                    break;
                case Level.Warning:
                    loggerConfig.MinimumLevel.Warning();
                    break;
                case Level.Error:
                    loggerConfig.MinimumLevel.Error();
                    break;
                case Level.Debug:
                    loggerConfig.MinimumLevel.Debug();
                    break;
                case Level.Verbose:
                    loggerConfig.MinimumLevel.Verbose();
                    break;
                default:
                    loggerConfig.MinimumLevel.Information();
                    break;
            }

            if (cfgjson.LokiURL is not null && cfgjson.LokiServiceName is not null)
            {
                loggerConfig.WriteTo.GrafanaLoki(cfgjson.LokiURL, [new LokiLabel { Key = "app", Value = cfgjson.LokiServiceName }]);
            }

            Log.Logger = loggerConfig.CreateLogger();

            hasteUploader = new HasteBinClient(cfgjson.HastebinEndpoint);

            UpdateLists();

            if (File.Exists("Lists/usernames.txt"))
                badUsernames = File.ReadAllLines("Lists/usernames.txt");
            else
                badUsernames = Array.Empty<string>();

            avatars = File.ReadAllLines("Lists/avatars.txt");

            if (Environment.GetEnvironmentVariable("CLIPTOK_TOKEN") is not null)
                token = Environment.GetEnvironmentVariable("CLIPTOK_TOKEN");
            else
                token = cfgjson.Core.Token;

            if (Environment.GetEnvironmentVariable("REDIS_URL") is not null)
                redis = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS_URL"));
            else
            {
                string redisHost;
                if (Environment.GetEnvironmentVariable("REDIS_DOCKER_OVERRIDE") is not null)
                    redisHost = "redis";
                else
                    redisHost = cfgjson.Redis.Host;
                redis = ConnectionMultiplexer.Connect($"{redisHost}:{cfgjson.Redis.Port}");
            }

            db = redis.GetDatabase();

            // Migration away from a broken attempt at a key in the past.
            db.KeyDelete("messages");

            DiscordClientBuilder discordBuilder = DiscordClientBuilder.CreateDefault(token, DiscordIntents.All);

            discordBuilder.ConfigureLogging(logging =>
            {
                logging.AddSerilog();
            });

            discordBuilder.ConfigureServices(services =>
            {
                services.Replace<IGatewayController, GatewayController>();
#pragma warning disable CS0618 // Type or member is obsolete
                services.AddSlashCommandsExtension(slash =>
                {
                slash.SlashCommandErrored += InteractionEvents.SlashCommandErrorEvent;
                slash.ContextMenuErrored += InteractionEvents.ContextCommandErrorEvent;

                var slashCommandClasses = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.Namespace == "Cliptok.Commands.InteractionCommands" && !t.IsNested);
                foreach (var type in slashCommandClasses)
                    slash.RegisterCommands(type, cfgjson.ServerID);
                });
            });

#pragma warning restore CS0618 // Type or member is obsolete
            discordBuilder.ConfigureExtraFeatures(clientConfig =>
            {
                clientConfig.LogUnknownEvents = false;
                clientConfig.LogUnknownAuditlogs = false;
            });

            discordBuilder.ConfigureEventHandlers
            (
                builder => builder.HandleComponentInteractionCreated(InteractionEvents.ComponentInteractionCreateEvent)
                                  .HandleSessionCreated(ReadyEvent.OnReady)
                                  .HandleMessageCreated(MessageEvent.MessageCreated)
                                  .HandleMessageUpdated(MessageEvent.MessageUpdated)
                                  .HandleMessageDeleted(MessageEvent.MessageDeleted)
                                  .HandleGuildMemberAdded(MemberEvents.GuildMemberAdded)
                                  .HandleGuildMemberRemoved(MemberEvents.GuildMemberRemoved)
                                  .HandleMessageReactionAdded(ReactionEvent.OnReaction)
                                  .HandleGuildMemberUpdated(MemberEvents.GuildMemberUpdated)
                                  .HandleUserUpdated(MemberEvents.UserUpdated)
                                  .HandleThreadCreated(ThreadEvents.Discord_ThreadCreated)
                                  .HandleThreadDeleted(ThreadEvents.Discord_ThreadDeleted)
                                  .HandleThreadListSynced(ThreadEvents.Discord_ThreadListSynced)
                                  .HandleThreadMemberUpdated(ThreadEvents.Discord_ThreadMemberUpdated)
                                  .HandleThreadMembersUpdated(ThreadEvents.Discord_ThreadMembersUpdated)
                                  .HandleGuildBanRemoved(UnbanEvent.OnUnban)
                                  .HandleVoiceStateUpdated(VoiceEvents.VoiceStateUpdate)
                                  .HandleChannelUpdated(ChannelEvents.ChannelUpdated)
                                  .HandleChannelDeleted(ChannelEvents.ChannelDeleted)
                                  .HandleAutoModerationRuleExecuted(AutoModEvents.AutoModerationRuleExecuted)
            );

            discordBuilder.UseCommandsNext(commands =>
            {
                var commandClasses = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.Namespace == "Cliptok.Commands" && !t.IsNested);
                foreach (var type in commandClasses)
                    commands.RegisterCommands(type);

                commands.CommandErrored += ErrorEvents.CommandsNextService_CommandErrored;
            }, new CommandsNextConfiguration
            {
                StringPrefixes = cfgjson.Core.Prefixes
            });

            // TODO(erisa): At some point we might be forced to ConnectAsync() the builder directly
            // and then we will need to rework some other pieces that rely on Program.discord
            discord = discordBuilder.Build();
            await discord.ConnectAsync();

            await ReadyEvent.OnStartup(discord);
            
            // Migration from joinwatch to user notes
            var joinWatchedUsersList = await Program.db.ListRangeAsync("joinWatchedUsers");
            var joinWatchNotesList = await Program.db.HashGetAllAsync("joinWatchedUsersNotes");
            int successfulMigrations = 0;
            int numJoinWatches = joinWatchedUsersList.Length;
            foreach (var user in joinWatchedUsersList)
            {
                // Get text for note; use joinwatch context if available, or "N/A; imported from joinwatch without context" otherwise
                string noteText;
                if (joinWatchNotesList.FirstOrDefault(x => x.Name == user) == default)
                    noteText = "N/A; imported from joinwatch without context";
                else
                    noteText = joinWatchNotesList.First(x => x.Name == user).Value;
                
                // Construct note
                var note = new UserNote
                {
                    TargetUserId = Convert.ToUInt64(user),
                    ModUserId = discord.CurrentUser.Id,
                    NoteText = noteText,
                    ShowOnModmail = false,
                    ShowOnWarn = false,
                    ShowAllMods = false,
                    ShowOnce = false,
                    ShowOnJoinAndLeave = true,
                    NoteId = db.StringIncrement("totalWarnings"),
                    Timestamp = DateTime.Now,
                    Type = WarningType.Note
                };
                
                // Save note & remove joinwatch
                await db.HashSetAsync(note.TargetUserId.ToString(), note.NoteId, JsonConvert.SerializeObject(note));
                await db.ListRemoveAsync("joinWatchedUsers", note.TargetUserId);
                await db.HashDeleteAsync("joinWatchedUsersNotes", note.TargetUserId);
                successfulMigrations++;
            }
            if (successfulMigrations > 0)
            {
                discord.Logger.LogInformation(CliptokEventID, "Successfully migrated {count}/{total} joinwatches to notes.", successfulMigrations, numJoinWatches);
            }
            
            if (cfgjson.ForumChannelAutoWarnFallbackChannel != 0)
                ForumChannelAutoWarnFallbackChannel = await discord.GetChannelAsync(cfgjson.ForumChannelAutoWarnFallbackChannel);

            // Only wait 3 seconds before the first set of tasks.
            await Task.Delay(3000);
            int loopCount = 0;
            while (true)
            {
                try
                {
                    List<Task<bool>> taskList =
                    [
                        Tasks.PunishmentTasks.CheckMutesAsync(),
                        Tasks.PunishmentTasks.CheckBansAsync(),
                        Tasks.PunishmentTasks.CheckAutomaticWarningsAsync(),
                        Tasks.ReminderTasks.CheckRemindersAsync(),
                        Tasks.RaidmodeTasks.CheckRaidmodeAsync(cfgjson.ServerID),
                        Tasks.LockdownTasks.CheckUnlocksAsync(),
                        Tasks.EventTasks.HandlePendingChannelUpdateEventsAsync(),
                        Tasks.EventTasks.HandlePendingChannelDeleteEventsAsync(),
                    ];

                    // To prevent a future issue if checks take longer than 10 seconds,
                    // we only start the 10 second counter after all tasks have concluded.
                    await Task.WhenAll(taskList);
                }
                catch (Exception e)
                {
                    discord.Logger.LogError(CliptokEventID, e, "An Error occurred during task runs}");
                }

                loopCount += 1;

                // after 180 cycles, roughly 30 minutes has passed
                if (loopCount == 180)
                {
                    List<ServerApiResponseJson> fetchResult;
                    try
                    {
                        fetchResult = await APIs.ServerAPI.FetchMaliciousServersList();
                        if (fetchResult is not null)
                        {
                            serverApiList = fetchResult;
                            discord.Logger.LogDebug("Successfully updated malicious invite list with {count} servers.", fetchResult.Count);
                        }
                    }
                    catch (Exception e)
                    {
                        discord.Logger.LogError(CliptokEventID, e, "An Error occurred during server list update");
                    }
                    loopCount = 0;
                }
                await Task.Delay(10000);
            }

        }
    }
}
