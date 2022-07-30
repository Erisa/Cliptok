namespace Cliptok.Helpers
{
    public class LogChannelNotFoundException : Exception
    {
        public LogChannelNotFoundException(string message) : base(message)
        {
        }
    }

    public class LogChannelHelper
    {
        internal static Dictionary<string, DiscordChannel> ChannelCache = new();
        internal static Dictionary<string, DiscordWebhookClient> WebhookCache = new();
        public static bool ready = false;

        public static async Task UnpackLogConfigAsync(ConfigJson config)
        {
            Dictionary<string, ulong> MigrationMapping = new()
            {
                { "mod", config.LogChannel},
                { "users", config.UserLogChannel },
                { "home", config.HomeChannel },
                { "investigations" , config.InvestigationsChannelId },
                { "support", config.SupportLogChannel },
                { "dms", config.DmLogChannelId },
                { "errors", config.ErrorLogChannelId },
                { "secret", config.MysteryLogChannelId },
                { "username", config.UsernameAPILogChannel}
            };

            if (config.LogChannels != null)
            {
                foreach (KeyValuePair<string, LogChannelConfig> logChannel in config.LogChannels)
                {
                    if (logChannel.Value.ChannelId != 0)
                    {
                        var channel = await Program.discord.GetChannelAsync(logChannel.Value.ChannelId);
                        ChannelCache.Add(logChannel.Key, channel);
                    }

                    DiscordWebhookClient webhookClient = new DiscordWebhookClient();
                    if (logChannel.Value.WebhookEnvVar != "")
                    {
                        await webhookClient.AddWebhookAsync(new Uri(Environment.GetEnvironmentVariable(logChannel.Value.WebhookEnvVar)));
                        WebhookCache.Add(logChannel.Key, webhookClient);
                    }
                    else if (logChannel.Value.WebhookUrl != "")
                    {
                        await webhookClient.AddWebhookAsync(new Uri(logChannel.Value.WebhookUrl));
                        WebhookCache.Add(logChannel.Key, webhookClient);
                    }
                }
            }

            foreach (KeyValuePair<string, ulong> migration in MigrationMapping)
            {
                if (migration.Value != 0 && !ChannelCache.ContainsKey(migration.Key))
                {
                    var channel = await Program.discord.GetChannelAsync(migration.Value);
                    ChannelCache.Add(migration.Key, channel);
                }
                else if (migration.Value == 0 && !ChannelCache.ContainsKey(migration.Key))
                {
                    // all channels that dont exist fallback to the home channel,
                    // which is the only channel that will always exist in config
                    var channel = await Program.discord.GetChannelAsync(config.HomeChannel);
                    ChannelCache.Add(migration.Key, channel);
                }
            }

            ready = true;
        }

        public static async Task<DiscordMessage> LogMessageAsync(string key, string content)
        {
            return await LogMessageAsync(key, new DiscordMessageBuilder().WithContent(content));
        }

        public static async Task<DiscordMessage> LogMessageAsync(string key, string content, DiscordEmbed embed)
        {
            return await LogMessageAsync(key, new DiscordMessageBuilder().WithContent(content).WithEmbed(embed));
        }

        public static async Task<DiscordMessage> LogMessageAsync(string key, DiscordEmbed embed)
        {
            return await LogMessageAsync(key, new DiscordMessageBuilder().WithEmbed(embed));
        }
        public static async Task<DiscordMessage> LogMessageAsync(string key, DiscordMessageBuilder message)
        {
            if (!ready)
                return null;

            try
            {
                if (WebhookCache.ContainsKey(key))
                {
                    return await FireWebhookFromMessageAsync(WebhookCache[key], message, key);
                }
                else if (ChannelCache.ContainsKey(key))
                {
                    return await ChannelCache[key].SendMessageAsync(message);
                }
                else
                {
                    throw new LogChannelNotFoundException($"A valid log channel for key '{key}' was not found!");
                }
            }
            catch (Exception ex)
            {
                Program.discord.Logger.LogError(ex, "Error ocurred trying to send message to key {key}", key);
                return null;
            }
        }

        public static async Task<DiscordMessage> LogDeletedMessagesAsync(string key, string content, List<DiscordMessage> messages, DiscordChannel channel)
        {
            string messageLog = await DiscordHelpers.CompileMessagesAsync(messages.AsEnumerable().Reverse().ToList(), channel);

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(messageLog));
            var msg = new DiscordMessageBuilder().WithContent(content).WithFile("messages.txt", stream);

            var hasteResult = await Program.hasteUploader.Post(messageLog);

            if (hasteResult.IsSuccess)
            {
                msg.WithEmbed(new DiscordEmbedBuilder().WithDescription($"[`📄 View online`]({Program.cfgjson.HastebinEndpoint}/raw/{hasteResult.Key})"));
            }

            return await LogMessageAsync(key, msg);
        }

        internal static async Task<DiscordMessage> FireWebhookFromMessageAsync(DiscordWebhookClient webhook, DiscordMessageBuilder message, string key)
        {
            var webhookBuilder = new DiscordWebhookBuilder()
                .AddComponents(message.Components)
                .WithAvatarUrl(Program.discord.CurrentUser.GetAvatarUrl(ImageFormat.Png, 1024))
                .WithUsername(Program.discord.CurrentUser.Username);

            if (message.Content is not null)
                webhookBuilder.WithContent(message.Content);

            if (message.Embeds.Count > 0)
                webhookBuilder.AddEmbeds(message.Embeds);

            if (message.Mentions is not null)
                webhookBuilder.AddMentions(message.Mentions);

            if (message.Files.Count > 0)
            {
                foreach (var file in message.Files)
                {
                    webhookBuilder.AddFile(file.FileName, file.Stream);
                }
            }

            if (ChannelCache.ContainsKey(key) && ChannelCache[key].IsThread)
                webhookBuilder.WithThreadId(ChannelCache[key].Id);

            var webhookResults = await webhook.BroadcastMessageAsync(webhookBuilder);

            return webhookResults.FirstOrDefault().Value;
        }

    }
}
