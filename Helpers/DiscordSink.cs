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

                    if (dummyWriter.ToString().Length > 1984 && dummyWriter.ToString().Length < 4096)
                    {
                        LogChannelHelper.LogMessageAsync("errors", new DiscordEmbedBuilder().WithDescription($"```cs\n{dummyWriter}\n```"));

                    }
                    else if (dummyWriter.ToString().Length < 1984)
                    {
                        LogChannelHelper.LogMessageAsync("errors", $"```cs\n{dummyWriter}\n```");
                    }
                    else
                    {
                        var stream = new MemoryStream(Encoding.UTF8.GetBytes(dummyWriter.ToString()));
                        LogChannelHelper.LogMessageAsync("errors", new DiscordMessageBuilder().WithFile("error.txt", stream));
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
