using Cliptok.Constants;

namespace Cliptok.Commands
{
    internal class TechSupportCmds
    {
        [Command("vcredist")]
        [Description("Outputs download URLs for the specified Visual C++ Redistributables version")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        public async Task RedistsCommand(
            SlashCommandContext ctx,

            [SlashChoiceProvider(typeof(VcRedistChoiceProvider))]
            [Parameter("version"), Description("Visual Studio version number or year")] long version
        )
        {
            VcRedist redist = VcRedistConstants.VcRedists
                .First((e) =>
                {
                    return version == e.Version;
                });

            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithTitle($"Visual C++ {redist.Year}{(redist.Year == 2015 ? "+" : "")} Redistributables (version {redist.Version})")
                .WithFooter("The above links are official and safe to download.")
                .WithColor(new("7160e8"));

            foreach (var url in redist.DownloadUrls)
            {
                embed.AddField($"{url.Key.ToString("G")}", $"{url.Value}");
            }

            await ctx.RespondAsync(null, embed.Build(), false);
        }

        [Command("asktextcmd")]
        [TextAlias("ask")]
        [Description("Outputs information on how and where to ask tech support questions. Replying to a message while triggering the command will mirror the reply in the response.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer]
        public async Task AskCmd(TextCommandContext ctx, [Description("Optional, a user to ping with the information")] DiscordUser user = default)
        {
            await ctx.Message.DeleteAsync();
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithColor(13920845);
            if (ctx.Channel.Id == Program.cfgjson.TechSupportChannel || ctx.Channel.ParentId == Program.cfgjson.SupportForumId)
            {
                embed.Title = "**__Need help?__**";
                embed.Description = $"You are in the right place! Please state your question with *plenty of detail* and mention the <@&{Program.cfgjson.CommunityTechSupportRoleID}> role and someone may be able to help you.\n\n" +
                                   $"Details includes error codes and other specific information.";
            }
            else
            {
                embed.Title = "**__Need Help Or Have a Problem?__**";
                embed.Description = $"You're probably looking for <#{Program.cfgjson.TechSupportChannel}> or <#{Program.cfgjson.SupportForumId}>!\n\n" +
                                   $"Once there, please be sure to provide **plenty of details**, ping the <@&{Program.cfgjson.CommunityTechSupportRoleID}> role, and *be patient!*\n\n" +
                                   $"Look under the `🔧 Support` category for the appropriate channel for your issue. See <#413274922413195275> for more info.";
            }

            if (user != default)
            {
                await ctx.Channel.SendMessageAsync(user.Mention, embed);
            }
            else if (ctx.Message.ReferencedMessage is not null)
            {
                var messageBuild = new DiscordMessageBuilder()
                    .AddEmbed(embed)
                    .WithReply(ctx.Message.ReferencedMessage.Id, mention: true);

                await ctx.Channel.SendMessageAsync(messageBuild);
            }
            else
            {
                await ctx.Channel.SendMessageAsync(embed);
            }
        }

        [Command("on-call")]
        [Description("Give yourself the CTS role.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer]
        [RequireHomeserverPerm(ServerPermLevel.TechnicalQueriesSlayer)]
        public async Task OnCallCommand(CommandContext ctx)
        {
            var ctsRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.CommunityTechSupportRoleID);
            await ctx.Member.GrantRoleAsync(ctsRole, "Used !on-call");
            await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"{Program.cfgjson.Emoji.On} Received Community Tech Support Role")
                .WithDescription($"{ctx.User.Mention} is available to help out in **#tech-support**.\n(Use `!off-call` when you're no longer available)")
                .WithColor(DiscordColor.Green)
            ));
        }

        [Command("off-call")]
        [Description("Remove the CTS role.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer]
        [RequireHomeserverPerm(ServerPermLevel.TechnicalQueriesSlayer)]
        public async Task OffCallCommand(CommandContext ctx)
        {
            var ctsRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.CommunityTechSupportRoleID);
            await ctx.Member.RevokeRoleAsync(ctsRole, "Used !off-call");
            await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"{Program.cfgjson.Emoji.Off} Removed Community Tech Support Role")
                .WithDescription($"{ctx.User.Mention} is no longer available to help out in **#tech-support**.\n(Use `!on-call` again when you're available)")
                .WithColor(DiscordColor.Red)
            ));
        }
    }

    internal class VcRedistChoiceProvider : IChoiceProvider
    {
        public async ValueTask<IEnumerable<DiscordApplicationCommandOptionChoice>> ProvideAsync(CommandParameter _)
        {
            return new List<DiscordApplicationCommandOptionChoice>
            {
                new("Visual Studio 2015+ - v140", "140"),
                new("Visual Studio 2013 - v120", "120"),
                new("Visual Studio 2012 - v110", "110"),
                new("Visual Studio 2010 - v100", "100"),
                new("Visual Studio 2008 - v90", "90"),
                new("Visual Studio 2005 - v80", "80")
            };
        }
    }
}
