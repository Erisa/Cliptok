using static Cliptok.Program;

namespace Cliptok.Events
{
    public class ErrorEvents
    {
        public static async Task CommandErrored(CommandsExtension _, CommandErroredEventArgs e)
        {
            // Because we no longer have DSharpPlus.CommandsNext or DSharpPlus.SlashCommands (only DSharpPlus.Commands), we can't point to different
            // error handlers based on command type in our command handler configuration. Instead, we can start here, and jump to the correct
            // handler based on the command type. TODO(#202): hopefully.
            
            // This is a lazy approach that just takes error type and points to the error handlers we already had.
            // Maybe it can be improved later?
            
            if (e.Context is TextCommandContext)
            {
                // Text command error
                await TextCommandErrored(e);
            }
            else if (e.Context is SlashCommandContext)
            {
                // Interaction command error (slash, user ctx, message ctx)
            }
            else
            {
                // Maybe left as CommandContext... TODO(#202): how to handle?
            }
        }
        
        public static async Task TextCommandErrored(CommandErroredEventArgs e)
        {
            if (e.Exception is CommandNotFoundException && (e.Context.Command is null || e.Context.Command.FullName != "help"))
                return;

            // avoid conflicts with modmail
            if (e.Context.Command.FullName == "edit" || e.Context.Command.FullName == "timestamp")
                return;

            e.Context.Client.Logger.LogError(CliptokEventID, e.Exception, "Exception occurred during {user}s invocation of {command}", e.Context.User.Username, e.Context.Command.FullName);

            var exs = new List<Exception>();
            if (e.Exception is AggregateException ae)
                exs.AddRange(ae.InnerExceptions);
            else
                exs.Add(e.Exception);

            foreach (var ex in exs)
            {
                if (ex is CommandNotFoundException && (e.Context.Command is null || e.Context.Command.FullName != "help"))
                    return;

                if (ex is ChecksFailedException && (e.Context.Command.Name != "help"))
                    return;

                var embed = new DiscordEmbedBuilder
                {
                    Color = new DiscordColor("#FF0000"),
                    Title = "An exception occurred when executing a command",
                    Description = $"{cfgjson.Emoji.BSOD} `{e.Exception.GetType()}` occurred when executing `{e.Context.Command.FullName}`.",
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
    }
}
