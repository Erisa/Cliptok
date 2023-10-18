namespace Cliptok.Commands.InteractionCommands
{
    internal class JoinwatchInteractions : ApplicationCommandModule
    {
        [SlashCommand("joinwatch", "Watch for joins and leaves of a given user. Output goes to #investigations.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task JoinwatchSlashCmd(InteractionContext ctx,
            [Option("user", "The user to watch for joins and leaves of.")] DiscordUser user,
            [Option("note", "An optional note for context.")] string note = "")
        {
            var joinWatchlist = await Program.db.ListRangeAsync("joinWatchedUsers");

            if (joinWatchlist.Contains(user.Id))
            {
                if (note != "")
                {
                    // User is already joinwatched, just update note
                    await Program.db.HashSetAsync("joinWatchedUsersNotes", user.Id, note);
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully updated the note for {user.Mention}. Run again with no note to unwatch.");
                    return;
                }

                // User is already joinwatched, unwatch
                Program.db.ListRemove("joinWatchedUsers", joinWatchlist.First(x => x == user.Id));
                await Program.db.HashDeleteAsync("joinWatchedUsersNotes", user.Id);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully unwatched {user.Mention}, since they were already in the list.");
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
    }

}