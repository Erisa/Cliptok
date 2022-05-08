using static Cliptok.Program;

namespace Cliptok.Events
{
    public class ErrorEvents
    {
        public static async Task CommandsNextService_CommandErrored(CommandsNextExtension cnext, CommandErrorEventArgs e)
        {
            if (e.Exception is CommandNotFoundException && (e.Command == null || e.Command.QualifiedName != "help"))
                return;

            // avoid conflicts with modmail
            if (e.Command.QualifiedName == "edit" || e.Command.QualifiedName == "timestamp")
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

        public static Task Discord_SocketErrored(DiscordClient client, SocketErrorEventArgs e)
        {
            client.Logger.LogError(eventId: CliptokEventID, e.Exception, "A socket error ocurred!");
            return Task.CompletedTask;
        }

        public static Task ClientError(DiscordClient client, ClientErrorEventArgs e)
        {
            client.Logger.LogError(CliptokEventID, e.Exception, "Client threw an exception");
            return Task.CompletedTask;
        }

    }
}
