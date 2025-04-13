using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Extensions;
using DSharpPlus.Net.Gateway;
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
            Program.discord.Logger.LogCritical("The gateway connection has zombied, and the bot is being restarted to reconnect reliably.");
            Environment.Exit(1);
        }

        public async Task ReconnectRequestedAsync(IGatewayClient _) { }
        public async Task ReconnectFailedAsync(IGatewayClient _) {
            Program.discord.Logger.LogCritical("The gateway connection has irrecoverably failed, and the bot is being restarted to reconnect reliably.");
            Environment.Exit(1);
        }
        public async Task SessionInvalidatedAsync(IGatewayClient _) { }
        public async Task ResumeAttemptedAsync(IGatewayClient _) { }

    }

    class Program
    {
        public static DiscordClient discord;
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

                // allow for multi-line scams with \n to separate lines
                for (int i = 0; i < listOutput.Length; i++)
                {
                    listOutput[i] = listOutput[i].Replace("\\n", "\n").Replace("\\\n", "\\n");
                }

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
            });

            discordBuilder.UseCommands((_, builder) =>
            {
                builder.CommandErrored += ErrorEvents.CommandErrored;

                // Register commands
                var commandClasses = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.Namespace == "Cliptok.Commands");
                foreach (var type in commandClasses)
                    if (type.Name == "GlobalCmds")
                        builder.AddCommands(type);
                    else
                        builder.AddCommands(type, cfgjson.ServerID);

                // Register command checks
                builder.AddCheck<HomeServerCheck>();
                builder.AddCheck<RequireHomeserverPermCheck>();
                builder.AddCheck<IsBotOwnerCheck>();
                builder.AddCheck<UserRolesPresentCheck>();

                // Set custom prefixes from config.json
                TextCommandProcessor textCommandProcessor = new(new TextCommandConfiguration
                {
                    PrefixResolver = new DefaultPrefixResolver(true, Program.cfgjson.Core.Prefixes.ToArray()).ResolvePrefixAsync
                });
                builder.AddProcessor(textCommandProcessor);
            }, new CommandsConfiguration
            {
                // Disable the default D#+ error handler because we are using our own
                UseDefaultCommandErrorHandler = false
            });

            discordBuilder.ConfigureExtraFeatures(clientConfig =>
            {
                clientConfig.LogUnknownEvents = false;
                clientConfig.LogUnknownAuditlogs = false;
            });

            discordBuilder.ConfigureEventHandlers
            (
                builder => builder.HandleComponentInteractionCreated(InteractionEvents.ComponentInteractionCreateEvent)
                                  .HandleModalSubmitted(InteractionEvents.ModalSubmitted)
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
                                  .HandleChannelCreated(ChannelEvents.ChannelCreated)
                                  .HandleChannelUpdated(ChannelEvents.ChannelUpdated)
                                  .HandleChannelDeleted(ChannelEvents.ChannelDeleted)
                                  .HandleAutoModerationRuleExecuted(AutoModEvents.AutoModerationRuleExecuted)
                                  .HandleGuildAuditLogCreated(AuditLogEvents.GuildAuditLogCreated)
            );

            // TODO(erisa): At some point we might be forced to ConnectAsync() the builder directly
            // and then we will need to rework some other pieces that rely on Program.discord
            discord = discordBuilder.Build();
            await discord.ConnectAsync();

            await ReadyEvent.OnStartup(discord);

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
                        Tasks.PunishmentTasks.CleanUpPunishmentMessagesAsync(),
                        Tasks.ReminderTasks.CheckRemindersAsync(),
                        Tasks.RaidmodeTasks.CheckRaidmodeAsync(cfgjson.ServerID),
                        Tasks.LockdownTasks.CheckUnlocksAsync(),
                        Tasks.EventTasks.HandlePendingChannelCreateEventsAsync(),
                        Tasks.EventTasks.HandlePendingChannelUpdateEventsAsync(),
                        Tasks.EventTasks.HandlePendingChannelDeleteEventsAsync(),
                    ];

                    // This one has its own time management, run it asynchronously and throw caution to the wind.
                    Tasks.MassDehoistTasks.CheckAndMassDehoistTask();

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
