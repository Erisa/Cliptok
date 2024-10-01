namespace Cliptok.Commands.InteractionCommands
{
    public class RulesInteractions
    {
        [HomeServer]
        [Command("rules")]
        [Description("Misc. commands related to server rules")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        internal class RulesSlashCommands
        {
            [Command("all")]
			[Description("Shows all of the community rules.")]
            public async Task RulesAllCommand(SlashCommandContext ctx)
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

            [Command("rule")]
			[Description("Shows a specific rule.")]
            public async Task RuleCommand(SlashCommandContext ctx, [Parameter("rule_number"), Description("The rule number to show.")] long ruleNumber)
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

            [Command("search")]
			[Description("Search for a rule by keyword.")]
            public async Task RuleSearchCommand(SlashCommandContext ctx, [Parameter("keyword"), Description("The keyword to search for.")] string keyword)
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