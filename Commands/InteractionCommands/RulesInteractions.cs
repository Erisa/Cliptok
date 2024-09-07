namespace Cliptok.Commands.InteractionCommands
{
    public class RulesInteractions : ApplicationCommandModule
    {
        [HomeServer]
        [SlashCommandGroup("rules", "Misc. commands related to server rules", defaultPermission: true)]
        internal class RulesSlashCommands
        {
            [SlashCommand("all", "Shows all of the community rules.", defaultPermission: true)]
            public async Task RulesAllCommand(InteractionContext ctx)
            {
                List<string> rules = default;

                try
                {
                    var screeningForm = await ctx.Guild.GetMembershipScreeningFormAsync();
                    rules = [.. screeningForm.Fields.FirstOrDefault(field => field.Type is DiscordMembershipScreeningFieldType.Terms).Values];
                }
                catch
                {
                    // community must be disabled
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't see any rules set in Discord for this server!");
                    return;
                }

                var embed = new DiscordEmbedBuilder().WithColor(new DiscordColor(0xe4717b));

                foreach (var rule in rules)
                {
                    embed.AddField($"Rule {rules.IndexOf(rule) + 1}", rule);
                }

                await ctx.RespondAsync(embed: embed);

            }

            [SlashCommand("rule", "Shows a specific rule.", defaultPermission: true)]
            public async Task RuleCommand(InteractionContext ctx, [Option("rule_number", "The rule number to show.")] long ruleNumber)
            {
                IReadOnlyList<string> rules = default;

                try
                {
                    var screeningForm = await ctx.Guild.GetMembershipScreeningFormAsync();
                    rules = screeningForm.Fields.FirstOrDefault(field => field.Type is DiscordMembershipScreeningFieldType.Terms).Values;
                }
                catch
                {
                    // community must be disabled
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't see any rules set in Discord for this server!");
                    return;
                }

                if (ruleNumber < 1 || ruleNumber > rules.Count)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Rule number must be between 1 and {rules.Count}.");
                    return;
                }

                var embed = new DiscordEmbedBuilder().WithTitle($"Rule {ruleNumber}").WithDescription(rules[(int)ruleNumber - 1]).WithColor(new DiscordColor(0xe4717b));

                await ctx.RespondAsync(embed: embed);
            }

            [SlashCommand("search", "Search for a rule by keyword.", defaultPermission: true)]
            public async Task RuleSearchCommand(InteractionContext ctx, [Option("keyword", "The keyword to search for.")] string keyword)
            {
                List<string> rules = default;

                try
                {
                    var screeningForm = await ctx.Guild.GetMembershipScreeningFormAsync();
                    rules = [.. screeningForm.Fields.FirstOrDefault(field => field.Type is DiscordMembershipScreeningFieldType.Terms).Values];
                }
                catch
                {
                    // community must be disabled
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't see any rules set in Discord for this server!");
                    return;
                }

                var matchingRules = rules.Where(rule => rule.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

                if (matchingRules.Count == 0)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Unable to find any rules that contain the `{keyword}` keyword.", ephemeral: true);
                    return;
                }

                var embed = new DiscordEmbedBuilder().WithColor(new DiscordColor(0xe4717b));

                foreach (var rule in matchingRules)
                {
                    embed.AddField($"Rule {rules.IndexOf(rule) + 1}", rule);
                }

                await ctx.RespondAsync(embed: embed);
            }
        }
    }
}