namespace Cliptok.Commands
{
    public class InsiderRoleCmds
    {
        static ulong rolesCmdId = 0;

        [
            Command("insider-roletextcmd"),
            TextAlias(
                "join-insider-dev", "join-insiders-dev",
                "join-insider-canary", "join-insiders-canary", "join-insider-can", "join-insiders-can",
                "join-insider-beta", "join-insiders-beta",
                "join-insider-rp", "join-insiders-rp", "join-insiders-11-rp", "join-insider-11-rp",
                "join-patch-tuesday",
                "leave-insiders", "leave-insider",
                "leave-insider-dev", "leave-insiders-dev",
                "leave-insider-canary", "leave-insiders-canary", "leave-insider-can", "leave-insiders-can",
                "leave-insider-beta", "leave-insiders-beta",
                "leave-insider-rp", "leave-insiders-rp", "leave-insiders-11-rp", "leave-insider-11-rp",
                "leave-patch-tuesday"
            ),
            Description("Use /roles instead"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task InsiderRoleCmd(TextCommandContext ctx)
        {
            if (rolesCmdId == 0)
                rolesCmdId = (await Program.discord.GetGuildApplicationCommandsAsync(ctx.Guild.Id)).First(x => x.Name == "roles").Id;

            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} This command is deprecated, please use </roles grant:{rolesCmdId}> or </roles remove:{rolesCmdId}> instead.");
        }

        [
            Command("keep-me-updatedtextcmd"),
            TextAlias("keep-me-updated"),
            Description("Gives you all opt-in roles"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task KeepMeUpdated(TextCommandContext ctx)
        {
            if (Program.cfgjson.InsiderRoles is null)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Insider roles are not configured! This command cannot be used.");
                return;
            }

            List<DiscordRole> roles = [];
            foreach (var roleId in Program.cfgjson.InsiderRoles)
            {
                roles.Add(await ctx.Guild.GetRoleAsync(roleId));
            }
            roles.AddRange(ctx.Member.Roles);

            await ctx.Member.ModifyAsync(m =>
            {
                m.Roles = roles;
                m.AuditLogReason = $"!keep-me-updated used by {ctx.User.Username}";
            });

            await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":CliptokSuccess:"));
        }

        [
            Command("dont-keep-me-updatedtextcmd"),
            TextAlias("dont-keep-me-updated"),
            Description("Takes away from you all opt-in roles"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task DontKeepMeUpdated(TextCommandContext ctx)
        {
            if (Program.cfgjson.InsiderRoles is null)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Insider roles are not configured! This command cannot be used.");
                return;
            }

            List<DiscordRole> roles = ctx.Member.Roles.ToList();
            foreach (var roleId in Program.cfgjson.InsiderRoles)
            {
                var role = roles.FirstOrDefault(r => r.Id == roleId);
                if (role != default)
                    roles.Remove(role);
            }

            await ctx.Member.ModifyAsync(m =>
            {
                m.Roles = roles;
                m.AuditLogReason = $"!dont-keep-me-updated used by {ctx.User.Username}";
            });

            await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":CliptokSuccess:"));
        }
    }
}
