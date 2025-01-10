namespace Cliptok.Commands.InteractionCommands
{
    public class InsidersInteractions
    {
        [Command("send-insiders-info-buttons"), Description("Sends a message with buttons to get Insider roles for #insiders-info.")]
        [RequireHomeserverPerm(ServerPermLevel.Admin, ownerOverride: true), RequirePermissions(permissions: DiscordPermission.ModerateMembers)]
        public static async Task SendInsidersInfoButtonMessage(SlashCommandContext ctx)
        {
            if (Program.cfgjson.InsiderInfoChannel != 0 && ctx.Channel.Id != Program.cfgjson.InsiderInfoChannel)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command only works in <#{Program.cfgjson.InsiderInfoChannel}>!", ephemeral: true);
                return;
            }
            
            DiscordComponent[] buttons =
            [
                new DiscordButtonComponent(DiscordButtonStyle.Primary, "insiders-info-roles-menu-callback", "Choose your Insider roles"),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, "insiders-info-chat-btn-callback", "I just want to chat for now")
            ];
            
            string insidersChannelMention;
            if (Program.cfgjson.InsidersChannel == 0)
            {
                insidersChannelMention = "#insiders";
                Program.discord.Logger.LogWarning("#insiders-info message sent with hardcoded #insiders mention! Is insidersChannel set in config.json?");
            }
            else
            {
                insidersChannelMention = $"<#{Program.cfgjson.InsidersChannel}>";
            }
            
            var builder = new DiscordInteractionResponseBuilder()
                .WithContent($"{Program.cfgjson.Emoji.Insider} Choose your Insider roles here! Or, you can choose to chat in {insidersChannelMention} without being notified about new builds.")
                .AddComponents(buttons);
            
            await ctx.RespondAsync(builder);
        }
    }
}