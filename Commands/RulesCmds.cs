namespace Cliptok.Commands
{
    public class RulesCmds
    {
        [HomeServer]
        [Command("rules")]
        [Description("Misc. commands related to server rules")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        internal class RulesSlashCommands
        {
            [Command("all")]
			[Description("Shows all of the community rules.")]
            public async Task RulesAllCommand(SlashCommandContext ctx, [Parameter("public"), Description("Whether to show the response publicly.")] bool? isPublic = null)
            {
                var publicResponse = await DeterminePublicResponse(ctx.Member, ctx.Channel, isPublic);

                List<string> rules = default;

                try
                {
                    var screeningForm = await ctx.Guild.GetMembershipScreeningFormAsync();
                    rules = [.. screeningForm.Fields.FirstOrDefault(field => field.Type is DiscordMembershipScreeningFieldType.Terms).Values];
                }
                catch
                {
                    // community must be disabled
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't see any rules set in Discord for this server!", ephemeral: !publicResponse);
                    return;
                }

                var embed = new DiscordEmbedBuilder().WithColor(new DiscordColor(0xe4717b));

                foreach (var rule in rules)
                {
                    embed.AddField($"Rule {rules.IndexOf(rule) + 1}", rule);
                }

                await ctx.RespondAsync(embed: embed, ephemeral: !publicResponse);

            }

            [Command("rule")]
			[Description("Shows a specific rule.")]
            public async Task RuleCommand(SlashCommandContext ctx,
                [Parameter("rule_number"), Description("The rule number to show.")] long ruleNumber,
                [Parameter("public"), Description("Whether to show the response publicly.")] bool? isPublic = null)
            {
                var publicResponse = await DeterminePublicResponse(ctx.Member, ctx.Channel, isPublic);

                IReadOnlyList<string> rules = default;

                try
                {
                    var screeningForm = await ctx.Guild.GetMembershipScreeningFormAsync();
                    rules = screeningForm.Fields.FirstOrDefault(field => field.Type is DiscordMembershipScreeningFieldType.Terms).Values;
                }
                catch
                {
                    // community must be disabled
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't see any rules set in Discord for this server!", ephemeral: !publicResponse);
                    return;
                }

                if (ruleNumber < 1 || ruleNumber > rules.Count)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Rule number must be between 1 and {rules.Count}.", ephemeral: !publicResponse);
                    return;
                }

                var embed = new DiscordEmbedBuilder().WithTitle($"Rule {ruleNumber}").WithDescription(rules[(int)ruleNumber - 1]).WithColor(new DiscordColor(0xe4717b));

                await ctx.RespondAsync(embed: embed, ephemeral: !publicResponse);
            }

            [Command("search")]
			[Description("Search for a rule by keyword.")]
            public async Task RuleSearchCommand(SlashCommandContext ctx,
                [Parameter("keyword"), Description("The keyword to search for.")] string keyword,
                [Parameter("public"), Description("Whether to show the response publicly.")] bool? isPublic = null)
            {
                var publicResponse = await DeterminePublicResponse(ctx.Member, ctx.Channel, isPublic);

                List<string> rules = default;

                try
                {
                    var screeningForm = await ctx.Guild.GetMembershipScreeningFormAsync();
                    rules = [.. screeningForm.Fields.FirstOrDefault(field => field.Type is DiscordMembershipScreeningFieldType.Terms).Values];
                }
                catch
                {
                    // community must be disabled
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't see any rules set in Discord for this server!", ephemeral: !publicResponse);
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

                await ctx.RespondAsync(embed: embed, ephemeral: !publicResponse);
            }

            // Returns: true for public response, false for private
            private async Task<bool> DeterminePublicResponse(DiscordMember member, DiscordChannel channel, bool? isPublic)
            {
                if (Program.cfgjson.RulesAllowedPublicChannels.Contains(channel.Id) || Program.cfgjson.RulesAllowedPublicChannels.Contains(channel.Parent.Id))
                {
                    if (isPublic is null)
                        return true;

                    return isPublic.Value;
                }

                if (await GetPermLevelAsync(member) >= ServerPermLevel.TrialModerator)
                {
                    if (isPublic is null)
                        return false;

                    return isPublic.Value;
                }

                return false;
            }
        }
    }
}