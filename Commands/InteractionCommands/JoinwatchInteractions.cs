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
                [Option("user", "The user to watch for joins and leaves of.")] DiscordUser _,
                [Option("note", "An optional note for context.")] string __ = "")
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command is deprecated and no longer works; all joinwatches have been converted to notes. Please use `/note add` instead, with the `show_on_join_and_leave` option.");
            }

            [SlashCommand("remove", "Stop watching for joins and leaves of a user.")]
            public async Task JoinwatchRemove(InteractionContext ctx,
                [Option("user", "The user to stop watching for joins and leaves of.")] DiscordUser user)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command is deprecated and no longer works; all joinwatches have been converted to notes. Please use `/note remove` instead.");
            }

            [SlashCommand("status", "Check the joinwatch status for a user.")]
            public async Task JoinwatchStatus(InteractionContext ctx,
                [Option("user", "The user whose joinwatch status to check.")] DiscordUser user)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command is deprecated and no longer works; all joinwatches have been converted to notes. Please use `/note list` or `/note details` instead.");
            }
        }
    }

}