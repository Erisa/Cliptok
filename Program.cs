using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using MicrosoftBot.Modules;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
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

            string redisHost;
            if (Environment.GetEnvironmentVariable("REDIS_DOCKER_OVERRIDE")  != null)
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
                UseInternalLogHandler = true,
                LogLevel = LogLevel.Debug
            });

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            discord.Ready += async e =>
            {
                Console.WriteLine($"Logged in as {e.Client.CurrentUser.Username}#{e.Client.CurrentUser.Discriminator}");
                logChannel = await discord.GetChannelAsync(cfgjson.LogChannel);
                await Task.Delay(4000);
                Mutes.CheckMutesAsync();
            };

            commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = cfgjson.Core.Prefixes
            }); ;

            commands.RegisterCommands<Warnings>();
            commands.RegisterCommands<MuteCmds>();

            await discord.ConnectAsync();

            while (true)
            {
                await Task.Delay(60000);
                Mutes.CheckMutesAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                
            }

        }
    }


}
