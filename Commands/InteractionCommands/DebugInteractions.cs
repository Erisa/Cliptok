namespace Cliptok.Commands.InteractionCommands
{
    internal class DebugInteractions : ApplicationCommandModule
    {
        [SlashCommand("scamcheck", "Check if a link or message is known to the anti-phishing API.", defaultPermission: true)]
        [Description("Check if a link or message is known to the anti-phishing API.")]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task ScamCheck(InteractionContext ctx, [Option("input", "Domain or message content to scan.")] string content)
        {
            var urlMatches = Constants.RegexConstants.url_rx.Matches(content);
            if (urlMatches.Count > 0 && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") != null && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") != "useyourimagination")
            {
                var (match, httpStatus, responseText, _) = await APIs.PhishingAPI.PhishingAPICheckAsync(content);

                string responseToSend;
                if (match)
                {
                    responseToSend = $"Match found:\n```json\n{responseText}\n```";

                }
                else
                {
                    responseToSend = $"No valid match found.\nHTTP Status `{(int)httpStatus}`, result:\n```json\n{responseText}\n```";
                }

                if (responseToSend.Length > 1940)
                {
                    try
                    {
                        HasteBinResult hasteURL = await Program.hasteUploader.Post(responseText);
                        if (hasteURL.IsSuccess)
                            responseToSend = hasteURL.FullUrl + ".json";
                        else
                            responseToSend = "Response was too big and Hastebin failed, sorry.";
                    }
                    catch
                    {
                        responseToSend = "Response was too big and Hastebin failed, sorry.";
                    }
                }
                await ctx.RespondAsync(responseToSend);
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Anti-phishing API is not configured, nothing for me to do.");
            }
        }

        [SlashCommand("tellraw", "You know what you're here for.")]
        [SlashRequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task TellRaw(InteractionContext ctx, [Option("input", "???")] string input, [Option("channel", "Work it out.")] DiscordChannel discordChannel = default)
        {
            if (discordChannel == default)
                discordChannel = ctx.Channel;

            try
            {
                await discordChannel.SendMessageAsync(input);
            }
            catch
            {
                await ctx.RespondAsync($"Your dumb message didn't want to send. Congrats, I'm proud of you.", ephemeral: true);
                return;
            }
            await ctx.RespondAsync($"I sent your stupid message to {discordChannel.Mention}.", ephemeral: true);
            await Program.mysteryLogChannel.SendMessageAsync(
                new DiscordMessageBuilder()
                .WithContent($"{ctx.User.Mention} used tellraw in {discordChannel.Mention}:")
                .WithAllowedMentions(Mentions.None)
                .WithEmbed(new DiscordEmbedBuilder().WithDescription(input))
            );
        }
    }
}
