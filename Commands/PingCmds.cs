namespace Cliptok.Commands
{
    public class PingCmds
    {
        [Command("ping")]
        [Description("Pong? This command lets you know whether I'm working well.")]
        [AllowedProcessors(typeof(TextCommandProcessor), typeof(SlashCommandProcessor))]
        public async Task Ping(CommandContext ctx)
        {
            ctx.Client.Logger.LogDebug(ctx.Client.GetConnectionLatency(Program.cfgjson.ServerID).ToString());

            await ctx.RespondAsync("Pinging...");
            var now = DateTime.UtcNow;

            DiscordMessage return_message = await ctx.GetResponseAsync();

            var ping = Math.Round((now - return_message.CreationTimestamp).TotalMilliseconds);
            char[] choices = new char[] { 'a', 'e', 'o', 'u', 'i', 'y' };
            char letter = choices[Program.rand.Next(0, choices.Length)];
            await return_message.ModifyAsync($"P{letter}ng! 🏓\n" +
                                             $"• It took me `{ping}ms` to reply to your message!\n" +
                                             $"• Last Websocket Heartbeat took `{Math.Round(ctx.Client.GetConnectionLatency(0).TotalMilliseconds, 0)}ms`!");
        }
    }
}
