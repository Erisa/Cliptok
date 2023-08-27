namespace Cliptok.Commands.InteractionCommands
{
    internal class StatusInteractions : ApplicationCommandModule
    {
        [SlashCommandGroup("status", "Status commands")]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [SlashCommandPermissions(Permissions.ModerateMembers)]

        public class StatusSlashCommands
        {

            [SlashCommand("set", "Set Cliptoks status.", defaultPermission: false)]
            public async Task StatusSetCommand(
                InteractionContext ctx,
                [Option("text", "The text to use for the status.")] string statusText,
                [Choice("Custom", (long)ActivityType.Custom)]
                [Choice("Playing", (long)ActivityType.Playing)]
                [Choice("Streaming", (long)ActivityType.Streaming)]
                [Choice("Listening to", (long)ActivityType.ListeningTo)]
                [Choice("Watching", (long)ActivityType.Watching)]
                [Choice("Competing", (long)ActivityType.Competing)]
                [Option("type", "Defaults to custom. The type of status to use.")]  long statusType = (long)ActivityType.Custom
            )
            {
                if (statusText.Length > 128)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Status messages must be less than 128 characters.");
                }

                await Program.db.StringSetAsync("config:status", statusText);
                await Program.db.StringSetAsync("config:status_type", statusType);

                await ctx.Client.UpdateStatusAsync(new DiscordActivity(statusText, (ActivityType)statusType));

                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Status has been updated!\nType: `{((ActivityType)statusType).ToString()}`\nText: `{statusText}`");
            }

            [SlashCommand("clear", "Clear Cliptoks status.", defaultPermission: false)]
            public async Task StatusClearCommand(InteractionContext ctx)
            {
                await Program.db.KeyDeleteAsync("config:status");
                await Program.db.KeyDeleteAsync("config:status_type");

                await ctx.Client.UpdateStatusAsync(new DiscordActivity());

                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Deleted} Status has been cleared!");
            }

        }
    }
}
