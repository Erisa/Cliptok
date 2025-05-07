using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace Cliptok
{
    class DiscordSink : ILogEventSink
    {
        readonly ITextFormatter _textFormatter;
        readonly object _syncRoot = new object();

        public DiscordSink(ITextFormatter textFormatter)
        {
            if (textFormatter is null) throw new ArgumentNullException(nameof(textFormatter));
            _textFormatter = textFormatter;
        }

        public async void Emit(LogEvent logEvent)
        {
            // wait for exceptions to propagate
            // TODO(erisa): find a better way to do this
            if (logEvent.Exception is not null)
                await Task.Delay(100);

            if (logEvent is null) throw new ArgumentNullException(nameof(logEvent));
            lock (_syncRoot)
            {
                try
                {
                    StringWriter dummyWriter = new();
                    _textFormatter.Format(logEvent, dummyWriter);

                    dummyWriter.Flush();

                    if (dummyWriter.ToString().ToLower().Contains("ratelimit"))
                    {
                        return;
                    }

                    string extraText = "";

                    if (
                        Program.cfgjson.PingBotOwnersOnBadErrors &&
                        logEvent.Level >= LogEventLevel.Error &&
                        logEvent.Exception is not null &&
                            (
                                logEvent.Exception.GetType() == typeof(NullReferenceException) ||
                                logEvent.Exception.GetType() == typeof(RedisTimeoutException)
                            )
                        )
                    {
                        var pingList = Program.cfgjson.BotOwners.Select(x => $"<@{x}>").ToList();
                        extraText = string.Join(", ", pingList);
                    }

                    // Skip errors caused by null values in audit log entries.
                    if (
                        logEvent.Exception is not null &&
                        logEvent.Exception.GetType() == typeof(InvalidOperationException) &&
                        logEvent.Exception.StackTrace is not null &&
                        logEvent.Exception.StackTrace.Contains("DSharpPlus.Entities.AuditLogs.AuditLogParser.ParseAuditLogEntryAsync")
                    )
                    {
                        return;
                    }

                    if (dummyWriter.ToString().Length > (1984 - extraText.Length) && dummyWriter.ToString().Length < (4096 - extraText.Length))
                    {
                        LogChannelHelper.LogMessageAsync("errors", new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithDescription($"{extraText}\n```cs\n{dummyWriter}\n```")).WithAllowedMentions(Mentions.All));

                    }
                    else if (dummyWriter.ToString().Length < (1984 - extraText.Length))
                    {
                        LogChannelHelper.LogMessageAsync("errors", new DiscordMessageBuilder().WithContent($"{extraText}\n```cs\n{dummyWriter}\n```").WithAllowedMentions(Mentions.All));
                    }
                    else
                    {
                        var stream = new MemoryStream(Encoding.UTF8.GetBytes(dummyWriter.ToString()));
                        var resp = new DiscordMessageBuilder().AddFile("error.txt", stream).WithAllowedMentions(Mentions.All);
                        if (extraText.Length > 0)
                        {
                            resp.WithContent(extraText);
                        }
                        LogChannelHelper.LogMessageAsync("errors", resp);
                    }
                }
                catch
                {
                    // well we cant log an error that happened while reporting an error, can we?
                }
            }
        }
    }


    public static class DiscordSinkConfigurationExtensions
    {
        const string DefaultOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}";

        public static LoggerConfiguration DiscordSink(
            this LoggerSinkConfiguration sinkConfiguration,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string outputTemplate = DefaultOutputTemplate,
            IFormatProvider formatProvider = null,
            LoggingLevelSwitch levelSwitch = null)
        {
            if (outputTemplate is null) throw new ArgumentNullException(nameof(outputTemplate));

            var formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
            var sink = new DiscordSink(formatter);
            return sinkConfiguration.Sink(sink, restrictedToMinimumLevel, levelSwitch);
        }

        public static LoggerConfiguration DiscordSink(
            this LoggerSinkConfiguration sinkConfiguration,
            ITextFormatter formatter,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            LoggingLevelSwitch levelSwitch = null)
        {
            if (formatter is null) throw new ArgumentNullException(nameof(formatter));

            var sink = new DiscordSink(formatter);
            return sinkConfiguration.Sink(sink, restrictedToMinimumLevel, levelSwitch);
        }
    }

}
