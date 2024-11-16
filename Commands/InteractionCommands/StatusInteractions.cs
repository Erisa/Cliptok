namespace Cliptok.Commands.InteractionCommands
{
    internal class StatusInteractions : ApplicationCommandModule
    {
        [SlashCommandGroup("status", "Status commands")]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [SlashCommandPermissions(permissions: DiscordPermission.ModerateMembers)]

        public class StatusSlashCommands
        {

            [SlashCommand("set", "Set Cliptoks status.", defaultPermission: false)]
            public async Task StatusSetCommand(
                InteractionContext ctx,
                [Option("text", "The text to use for the status.")] string statusText,
                [Choice("Custom", (long)DiscordActivityType.Custom)]
                [Choice("Playing", (long)DiscordActivityType.Playing)]
                [Choice("Streaming", (long)DiscordActivityType.Streaming)]
                [Choice("Listening to", (long)DiscordActivityType.ListeningTo)]
                [Choice("Watching", (long)DiscordActivityType.Watching)]
                [Choice("Competing", (long)DiscordActivityType.Competing)]
                [Option("type", "Defaults to custom. The type of status to use.")]  long statusType = (long)DiscordActivityType.Custom
            )
            {
                if (statusText.Length > 128)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Status messages must be less than 128 characters.");
                }

                await Program.db.StringSetAsync("config:status", statusText);
                await Program.db.StringSetAsync("config:status_type", statusType);

                await ctx.Client.UpdateStatusAsync(new DiscordActivity(statusText, (DiscordActivityType)statusType));

                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Status has been updated!\nType: `{((DiscordActivityType)statusType).ToString()}`\nText: `{statusText}`");
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
