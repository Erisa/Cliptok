using Cliptok.Constants;

namespace Cliptok.Commands.InteractionCommands
{
    internal class DebugInteractions
    {
        [Command("scamcheck")]
        [Description("Check if a link or message is known to the anti-phishing API.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermissions.ModerateMembers)]
        public async Task ScamCheck(SlashCommandContext ctx, [Parameter("input"), Description("Domain or message content to scan.")] string content)
        {
            var urlMatches = Constants.RegexConstants.url_rx.Matches(content);
            if (urlMatches.Count > 0 && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") is not null && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") != "useyourimagination")
            {
                var (match, httpStatus, responseText, _) = await APIs.PhishingAPI.PhishingAPICheckAsync(content);

                string responseToSend;
                if (match)
                {
                    responseToSend = $"Match found:\n`";
                }
                else
                {
                    responseToSend = $"No valid match found.\nHTTP Status `{(int)httpStatus}`, result:\n";
                }

                responseToSend += await StringHelpers.CodeOrHasteBinAsync(responseText, "json");

                await ctx.RespondAsync(responseToSend);
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Anti-phishing API is not configured, nothing for me to do.");
            }
        }

        [Command("tellraw")]
        [Description("You know what you're here for.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [SlashRequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(DiscordPermissions.ModerateMembers)]
        public async Task TellRaw(SlashCommandContext ctx, [Parameter("input"), Description("???")] string input, [Parameter("reply_msg_id"), Description("ID of message to use in a reply context.")] string replyID = "0", [Parameter("pingreply"), Description("Ping pong.")] bool pingreply = true, [Parameter("channel"), Description("Either mention or ID. Not a name.")] string discordChannel = default)
        {
            DiscordChannel channelObj = default;

            if (discordChannel == default)
                channelObj = ctx.Channel;
            else
            {
                ulong channelId;
                if (!ulong.TryParse(discordChannel, out channelId))
                {
                    var captures = RegexConstants.channel_rx.Match(discordChannel).Groups[1].Captures;
                    if (captures.Count > 0)
                        channelId = Convert.ToUInt64(captures[0].Value);
                    else
                    {
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} The channel you gave can't be parsed. Please give either an ID or a mention of a channel.", ephemeral: true);
                        return;
                    }
                }
                try
                {
                    channelObj = await ctx.Client.GetChannelAsync(channelId);
                }
                catch
                {
                    // caught immediately after
                }
                if (channelObj == default)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I can't find a channel with the provided ID!", ephemeral: true);
                    return;
                }
            }

            try
            {
                await channelObj.SendMessageAsync(new DiscordMessageBuilder().WithContent(input).WithReply(Convert.ToUInt64(replyID), pingreply, false));
            }
            catch
            {
                await ctx.RespondAsync($"Your dumb message didn't want to send. Congrats, I'm proud of you.", ephemeral: true);
                return;
            }
            await ctx.RespondAsync($"I sent your stupid message to {channelObj.Mention}.", ephemeral: true);
            await LogChannelHelper.LogMessageAsync("secret",
                new DiscordMessageBuilder()
                .WithContent($"{ctx.User.Mention} used tellraw in {channelObj.Mention}:")
                .WithAllowedMentions(Mentions.None)
                .AddEmbed(new DiscordEmbedBuilder().WithDescription(input))
            );
        }

        [Command("userinfo")]
        [Description("Retrieve information about a given user.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        public async Task UserInfoSlashCommand(SlashCommandContext ctx, [Parameter("user"), Description("The user to retrieve information about.")] DiscordUser user, [Parameter("public"), Description("Whether to show the output publicly.")] bool publicMessage = false)
        {
            await ctx.RespondAsync(embed: await DiscordHelpers.GenerateUserEmbed(user, ctx.Guild), ephemeral: !publicMessage);
        }

        [Command("muteinfo")]
        [Description("Show information about the mute for a user.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermissions.ModerateMembers)]
        public async Task MuteInfoSlashCommand(
            SlashCommandContext ctx,
            [Parameter("user"), Description("The user whose mute information to show.")] DiscordUser targetUser,
            [Parameter("public"), Description("Whether to show the output publicly. Default: false")] bool isPublic = false)
        {
            await ctx.RespondAsync(embed: await MuteHelpers.MuteStatusEmbed(targetUser, ctx.Guild), ephemeral: !isPublic);
        }

        [Command("baninfo")]
        [Description("Show information about the ban for a user.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermissions.ModerateMembers)]
        public async Task BanInfoSlashCommand(
            SlashCommandContext ctx,
            [Parameter("user"), Description("The user whose ban information to show.")] DiscordUser targetUser,
            [Parameter("public"), Description("Whether to show the output publicly. Default: false")] bool isPublic = false)
        {
            await ctx.RespondAsync(embed: await BanHelpers.BanStatusEmbed(targetUser, ctx.Guild), ephemeral: !isPublic);
        }
    }
}
