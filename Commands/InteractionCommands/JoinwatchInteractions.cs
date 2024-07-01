namespace Cliptok.Commands.InteractionCommands
{
    internal class JoinwatchInteractions : ApplicationCommandModule
    {
        [SlashCommandGroup("joinwatch", "Watch for joins and leaves of a given user. Output goes to #investigations.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public class JoinwatchSlashCmds
        {
            [SlashCommand("add", "Watch for joins and leaves of a given user. Output goes to #investigations.")]
            public async Task JoinwatchAdd(InteractionContext ctx,
                [Option("user", "The user to watch for joins and leaves of.")] DiscordUser user,
                [Option("note", "An optional note for context.")] string note = "")
            {
                var joinWatchlist = await Program.db.ListRangeAsync("joinWatchedUsers");

                if (joinWatchlist.Contains(user.Id))
                {
                    // User is already watched

                    // Get current note; if it's the same, do nothing
                    var currentNote = await Program.db.HashGetAsync("joinWatchedUsersNotes", user.Id);
                    if (currentNote == note || (string.IsNullOrWhiteSpace(currentNote) && string.IsNullOrWhiteSpace(note)))
                    {
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {user.Mention} is already being watched with the same note! Nothing to do.");
                        return;
                    }

                    // If note is different, update it

                    // If new note is empty, remove instead of changing to empty string!
                    if (string.IsNullOrWhiteSpace(note))
                    {
                        await Program.db.HashDeleteAsync("joinWatchedUsersNotes", user.Id);
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully removed the note for {user.Mention}! They are still being watched.");
                    }
                    else
                    {
                        await Program.db.HashSetAsync("joinWatchedUsersNotes", user.Id, note);
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully updated the note for {user.Mention}:\n> {note}");
                    }
                }
                else
                {
                    // User is not joinwatched, watch
                    await Program.db.ListRightPushAsync("joinWatchedUsers", user.Id);
                    if (note != "")
                        await Program.db.HashSetAsync("joinWatchedUsersNotes", user.Id, note);
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Now watching for joins/leaves of {user.Mention} to send to the investigations channel"
                        + (note == "" ? "!" : $" with the following note:\n>>> {note}"));
                }
            }

            [SlashCommand("remove", "Stop watching for joins and leaves of a user.")]
            public async Task JoinwatchRemove(InteractionContext ctx,
                [Option("user", "The user to stop watching for joins and leaves of.")] DiscordUser user)
            {
                var joinWatchlist = await Program.db.ListRangeAsync("joinWatchedUsers");

                // Check user watch status first; error if not watched
                if (!joinWatchlist.Contains(user.Id))
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {user.Mention} is not being watched! Nothing to do.");
                    return;
                }

                Program.db.ListRemove("joinWatchedUsers", joinWatchlist.First(x => x == user.Id));
                await Program.db.HashDeleteAsync("joinWatchedUsersNotes", user.Id);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully unwatched {user.Mention}!");
            }

            [SlashCommand("status", "Check the joinwatch status for a user.")]
            public async Task JoinwatchStatus(InteractionContext ctx,
                [Option("user", "The user whose joinwatch status to check.")] DiscordUser user)
            {
                var joinWatchlist = await Program.db.ListRangeAsync("joinWatchedUsers");

                if (joinWatchlist.Contains(user.Id))
                {
                    var note = await Program.db.HashGetAsync("joinWatchedUsersNotes", user.Id);

                    if (string.IsNullOrWhiteSpace(note))
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Information} {user.Mention} is currently being watched, but no note is set.");
                    else
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Information} {user.Mention} is currently being watched with the following note:\n> {note}");
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {user.Mention} is not being watched!");
                }
            }
        }
    }

}