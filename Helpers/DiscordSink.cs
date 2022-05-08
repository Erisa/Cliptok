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
            if (textFormatter == null) throw new ArgumentNullException(nameof(textFormatter));
            _textFormatter = textFormatter;
        }

        public async void Emit(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            lock (_syncRoot)
            {
                try
                {
                    if (Program.errorLogChannel != null && logEvent.Level >= LogEventLevel.Warning)
                    {
                        StringWriter dummyWriter = new();
                        _textFormatter.Format(logEvent, dummyWriter);

                        dummyWriter.Flush();

                        if (dummyWriter.ToString().ToLower().Contains("Ratelimit"))
                        {
                            return;
                        }

                        if (dummyWriter.ToString().Length > 1984 && dummyWriter.ToString().Length < 4096)
                        {
                            Program.errorLogChannel.SendMessageAsync(new DiscordEmbedBuilder().WithDescription($"```cs\n{dummyWriter}\n```"));

                        }
                        else if (dummyWriter.ToString().Length < 1984)
                        {
                            Program.errorLogChannel.SendMessageAsync($"```cs\n{dummyWriter}\n```");
                        }
                        else
                        {
                            var stream = new MemoryStream(Encoding.UTF8.GetBytes(dummyWriter.ToString()));
                            Program.errorLogChannel.SendMessageAsync(new DiscordMessageBuilder().WithFile("error.txt", stream));
                        }
                    }
                } catch (Exception ex)
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
            if (outputTemplate == null) throw new ArgumentNullException(nameof(outputTemplate));

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
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));

            var sink = new DiscordSink(formatter);
            return sinkConfiguration.Sink(sink, restrictedToMinimumLevel, levelSwitch);
        }
    }

}
