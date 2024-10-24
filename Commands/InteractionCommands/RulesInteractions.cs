namespace Cliptok.Commands.InteractionCommands
{
    public class RulesInteractions : ApplicationCommandModule
    {
        [HomeServer]
        [SlashCommandGroup("rules", "Misc. commands related to server rules", defaultPermission: true)]
        internal class RulesSlashCommands
        {
            [SlashCommand("all", "Shows all of the community rules.", defaultPermission: true)]
            public async Task RulesAllCommand(InteractionContext ctx, [Option("public", "Whether to show the response publicly.")] bool? isPublic = null)
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

            [SlashCommand("rule", "Shows a specific rule.", defaultPermission: true)]
            public async Task RuleCommand(InteractionContext ctx, [Option("rule_number", "The rule number to show.")] long ruleNumber, [Option("public", "Whether to show the response publicly.")] bool? isPublic = null)
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

            [SlashCommand("search", "Search for a rule by keyword.", defaultPermission: true)]
            public async Task RuleSearchCommand(InteractionContext ctx, [Option("keyword", "The keyword to search for.")] string keyword, [Option("public", "Whether to show the response publicly.")] bool? isPublic = null)
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