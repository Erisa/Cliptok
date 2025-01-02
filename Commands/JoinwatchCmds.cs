namespace Cliptok.Commands
{
    public class JoinwatchCmds
    {
        [Command("joinwatch")]
        [Description("Watch for joins and leaves of a given user. Output goes to #investigations.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public class JoinwatchCmd
        {
            [DefaultGroupCommand]
            [Command("toggle")]
            [Description("Toggle joinwatch for a given user.")]
            [AllowedProcessors(typeof(TextCommandProcessor))]
            public async Task JoinwatchToggle(CommandContext ctx,
                [Parameter("user"), Description("The user to watch for joins and leaves of.")] DiscordUser user,
                [Parameter("note"), Description("An optional note for context.")] string note = "")
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command is deprecated and no longer works; all joinwatches have been converted to notes. To add a note for this user, please use `/note add user:{user.Id} note:{(string.IsNullOrEmpty(note) ? "<context>" : note)} show_on_join_and_leave:True`; to remove one, use `/note delete user:{user.Id} note:<note>`.");
            }

            [Command("add")]
			[Description("Watch for joins and leaves of a given user. Output goes to #investigations.")]
            public async Task JoinwatchAdd(CommandContext ctx,
                [Parameter("user"), Description("The user to watch for joins and leaves of.")] DiscordUser user,
                [Parameter("note"), Description("An optional note for context.")] string note = "")
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command is deprecated and no longer works; all joinwatches have been converted to notes. Please use `/note add` instead, like this: `/note add user:{user.Id} note:{(string.IsNullOrEmpty(note) ? "<context>" : note)} show_on_join_and_leave:True`");
            }

            [Command("remove")]
			[Description("Stop watching for joins and leaves of a user.")]
            public async Task JoinwatchRemove(CommandContext ctx,
                [Parameter("user"), Description("The user to stop watching for joins and leaves of.")] DiscordUser user)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command is deprecated and no longer works; all joinwatches have been converted to notes. Please use `/note delete` instead, like this: `/note delete user:{user.Id} note:<note>`");
            }

            [Command("status")]
			[Description("Check the joinwatch status for a user.")]
            public async Task JoinwatchStatus(CommandContext ctx,
                [Parameter("user"), Description("The user whose joinwatch status to check.")] DiscordUser user)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command is deprecated and no longer works; all joinwatches have been converted to notes. Please use `/note list user:{user.Id}` to show all of this user's notes, or `/note details user:{user.Id} note:<note>` for details on a specific note, instead. Notes with \"Show on Join & Leave\" enabled will behave like joinwatches.");
            }
        }
    }
}