namespace Cliptok.Commands
{
    internal class StatusCmds
    {
        [Command("status")]
        [Description("Status commands")]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermission.ModerateMembers)]

        public class StatusSlashCommands
        {

            [Command("set")]
			[Description("Set Cliptoks status.")]
            [AllowedProcessors(typeof(SlashCommandProcessor))]
            public async Task StatusSetCommand(
                SlashCommandContext ctx,
                [Parameter("text"), Description("The text to use for the status.")] string statusText,
                [Parameter("type"), Description("Defaults to custom. The type of status to use.")]  DiscordActivityType statusType = DiscordActivityType.Custom
            )
            {
                if (statusText.Length > 128)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Status messages must be less than 128 characters.");
                }

                await Program.db.StringSetAsync("config:status", statusText);
                await Program.db.StringSetAsync("config:status_type", (long)statusType);

                await ctx.Client.UpdateStatusAsync(new DiscordActivity(statusText, statusType));

                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Status has been updated!\nType: `{statusType.ToString()}`\nText: `{statusText}`");
            }

            [Command("clear")]
			[Description("Clear Cliptoks status.")]
            [AllowedProcessors(typeof(SlashCommandProcessor))]
            public async Task StatusClearCommand(SlashCommandContext ctx)
            {
                await Program.db.KeyDeleteAsync("config:status");
                await Program.db.KeyDeleteAsync("config:status_type");

                await ctx.Client.UpdateStatusAsync(new DiscordActivity());

                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Deleted} Status has been cleared!");
            }

        }
    }
}
