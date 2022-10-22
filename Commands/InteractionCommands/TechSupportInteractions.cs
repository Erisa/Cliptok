using Cliptok.Constants;

namespace Cliptok.Commands.InteractionCommands
{
    public class TechSupportInteractions : ApplicationCommandModule
    {
        [SlashCommand("vcredist", "Outputs download URLs for the specified Visual C++ Redistributables version")]
        public async Task RedistsCommand(
            InteractionContext ctx,
            [Option("version", "Visual Studio version number or year")] long version
            )
        {
            VcRedist redist = VcRedistConstants.VcRedists
                .Where((e) =>
                {
                    if (version is (>= 2015 or 140))
                    {
                        return e.Year == 2015;
                    }
                    else
                    {
                        return version == e.Year || version == e.Version;
                    }
                })
                .FirstOrDefault();

            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithColor(new("7160e8"));

            if (redist.Year != 0)
            {
                foreach (var url in redist.DownloadUrls)
                {
                    embed.AddField($"{url.Key.ToString("G")}", $"{url.Value}");
                }
                embed.WithTitle($"Visual C++ {redist.Year}{(redist.Year == 2015 ? "+" : "")} Redistributables (version {redist.Version})")
                    .WithFooter("The above links are official and safe to download.");
            }
            else
            {
                embed.WithDescription("No results were found for the specified version or year.");
            }

            await ctx.RespondAsync(null, embed.Build(), false);
        }
    }
}
