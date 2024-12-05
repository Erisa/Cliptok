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
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command is deprecated and no longer works; all joinwatches have been converted to notes. Please use `/note add` instead, like this: `/note add user:{user.Id} note:{(string.IsNullOrEmpty(note) ? "<context>" : note)} show_on_join_and_leave:True`");
            }

            [SlashCommand("remove", "Stop watching for joins and leaves of a user.")]
            public async Task JoinwatchRemove(InteractionContext ctx,
                [Option("user", "The user to stop watching for joins and leaves of.")] DiscordUser user)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command is deprecated and no longer works; all joinwatches have been converted to notes. Please use `/note delete` instead, like this: `/note delete user:{user.Id} note:<note>`");
            }

            [SlashCommand("status", "Check the joinwatch status for a user.")]
            public async Task JoinwatchStatus(InteractionContext ctx,
                [Option("user", "The user whose joinwatch status to check.")] DiscordUser user)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command is deprecated and no longer works; all joinwatches have been converted to notes. Please use `/note list user:{user.Id}` to show all of this user's notes, or `/note details user:{user.Id} note:<note>` for details on a specific note, instead. Notes with \"Show on Join & Leave\" enabled will behave like joinwatches.");
            }
        }
    }

}