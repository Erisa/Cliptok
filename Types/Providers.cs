namespace Cliptok.Types
{
    internal static class Providers
    {
        internal class RolesAutocompleteProvider : IAutoCompleteProvider
        {
            public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext ctx)
            {
                Dictionary<string, ulong> options = [];
                if (Program.cfgjson.InsiderRoles is not null)
                {
                    foreach (var insiderRoleId in Program.cfgjson.InsiderRoles)
                    {
                        options.Add((await ctx.Guild.GetRoleAsync(insiderRoleId)).Name, insiderRoleId);
                    }
                }

                if (ctx.Command.FullName.Contains("roles"))
                {
                    if (Program.cfgjson.GiveawaysRole != default)
                        options.Add((await ctx.Guild.GetRoleAsync(Program.cfgjson.GiveawaysRole)).Name, Program.cfgjson.GiveawaysRole);

                    if (Program.cfgjson.CommunityTechSupportRoleID != default && await GetPermLevelAsync(ctx.Member) >= ServerPermLevel.TechnicalQueriesSlayer)
                        options.Add((await ctx.Guild.GetRoleAsync(Program.cfgjson.CommunityTechSupportRoleID)).Name, Program.cfgjson.CommunityTechSupportRoleID);
                }

                List<DiscordAutoCompleteChoice> list = new();

                foreach (var option in options)
                {
                    var focusedOption = ctx.Options.FirstOrDefault(option => option.Focused);
                    if (focusedOption.Value.ToString() == "" || option.Key.Contains(focusedOption.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(new DiscordAutoCompleteChoice(option.Key, option.Value.ToString()));
                    }
                }

                return list;
            }
        }
    }
}
