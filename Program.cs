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
        public static DiscordGuild homeGuild;

        public static Random rand = new();
        public static HasteBinClient hasteUploader;

        public static StringWriter outputCapture = new();

        static public readonly HttpClient httpClient = new();

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
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var logFormat = "[{Timestamp:yyyy-MM-dd HH:mm:ss zzz}] [{Level}] {Message}{NewLine}{Exception}";

            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Debug()
                .Filter.ByExcluding("Contains(@m, 'Unknown event:')")
#else
                .Filter.ByExcluding("Contains(@m, 'Unknown event:')")
                .MinimumLevel.Information()
#endif
                .WriteTo.Console(outputTemplate: logFormat, theme: AnsiConsoleTheme.Literate)
                .WriteTo.TextWriter(outputCapture, outputTemplate: logFormat)
                .WriteTo.DiscordSink(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information, outputTemplate: logFormat)
                .CreateLogger();

            var logFactory = new LoggerFactory().AddSerilog();

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

            UpdateLists();

            if (File.Exists("Lists/usernames.txt"))
                badUsernames = File.ReadAllLines("Lists/usernames.txt");
            else
                badUsernames = Array.Empty<string>();

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
#if DEBUG
                MinimumLogLevel = LogLevel.Debug,
#else
                MinimumLogLevel = LogLevel.Information,
#endif
                LoggerFactory = logFactory,
                Intents = DiscordIntents.All + 3145728
            });

            var slash = discord.UseSlashCommands();
            slash.SlashCommandErrored += InteractionEvents.SlashCommandErrorEvent;
            var slashCommandClasses = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.Namespace == "Cliptok.Commands.InteractionCommands" && !t.IsNested);
            foreach (var type in slashCommandClasses)
                slash.RegisterCommands(type, cfgjson.ServerID); ;

            discord.ComponentInteractionCreated += InteractionEvents.ComponentInteractionCreateEvent;
            discord.Ready += ReadyEvent.OnReady;
            discord.MessageCreated += MessageEvent.MessageCreated;
            discord.MessageUpdated += MessageEvent.MessageUpdated;
            discord.GuildMemberAdded += MemberEvents.GuildMemberAdded;
            discord.GuildMemberRemoved += MemberEvents.GuildMemberRemoved;
            discord.MessageReactionAdded += ReactionEvent.OnReaction;
            discord.GuildMemberUpdated += MemberEvents.GuildMemberUpdated;
            discord.UserUpdated += MemberEvents.UserUpdated;
            discord.ClientErrored += ErrorEvents.ClientError;
            discord.SocketErrored += ErrorEvents.Discord_SocketErrored;
            discord.ThreadCreated += ThreadEvents.Discord_ThreadCreated;
            discord.ThreadUpdated += ThreadEvents.Discord_ThreadUpdated;
            discord.ThreadDeleted += ThreadEvents.Discord_ThreadDeleted;
            discord.ThreadListSynced += ThreadEvents.Discord_ThreadListSynced;
            discord.ThreadMemberUpdated += ThreadEvents.Discord_ThreadMemberUpdated;
            discord.ThreadMembersUpdated += ThreadEvents.Discord_ThreadMembersUpdated;

            commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = cfgjson.Core.Prefixes
            });

            var commandClasses = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.Namespace == "Cliptok.Commands" && !t.IsNested);
            foreach (var type in commandClasses)
                commands.RegisterCommands(type);

            commands.CommandErrored += ErrorEvents.CommandsNextService_CommandErrored;

            await discord.ConnectAsync();

            await ReadyEvent.OnStartup(discord);

            // Only wait 3 seconds before the first set of tasks.
            await Task.Delay(3000);
            while (true)
            {
                try
                {
                    List<Task<bool>> taskList = new();
                    taskList.Add(Tasks.PunishmentTasks.CheckMutesAsync());
                    taskList.Add(Tasks.PunishmentTasks.CheckBansAsync());
                    taskList.Add(Tasks.ReminderTasks.CheckRemindersAsync());
                    taskList.Add(Tasks.RaidmodeTasks.CheckRaidmodeAsync(cfgjson.ServerID));
                    taskList.Add(Tasks.LockdownTasks.CheckUnlocksAsync());

                    // To prevent a future issue if checks take longer than 10 seconds,
                    // we only start the 10 second counter after all tasks have concluded.
                    await Task.WhenAll(taskList);
                }
                catch (Exception e)
                {
                    discord.Logger.LogError(CliptokEventID, "An Error occurred during task runs: {message}", e.ToString());
                }
                await Task.Delay(10000);
            }

        }
    }
}
