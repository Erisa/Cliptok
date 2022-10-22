using Cliptok.Constants;

namespace Cliptok.Commands.InteractionCommands
{
    public class TechSupportInteractions : ApplicationCommandModule
    {
        [SlashCommand("vcredist", "Outputs download URLs for the specified Visual C++ Redistributables version")]
        public async Task RedistsCommand(
            InteractionContext ctx,

            [Choice("Visual Studio 2015+ - v140", 140)]
            [Choice("Visual Studio 2013 - v120", 120)]
            [Choice("Visual Studio 2012 - v110", 110)]
            [Choice("Visual Studio 2010 - v100", 100)]
            [Choice("Visual Studio 2008 - v90", 90)]
            [Choice("Visual Studio 2005 - v80", 80)]
            [Option("version", "Visual Studio version number or year")] long version
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
    }
}
