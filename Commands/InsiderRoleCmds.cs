namespace Cliptok.Commands
{
    public class InsiderRoleCmds
    {
        static ulong rolesCmdId = 0;

        public static async Task GiveUserRolesAsync(TextCommandContext ctx, Func<System.Reflection.PropertyInfo, bool> predicate)
        {
            if (Program.cfgjson.UserRoles is null)
            {
                // Config hasn't been updated yet.
                return;
            }

            DiscordGuild guild = await Program.discord.GetGuildAsync(ctx.Guild.Id);
            String response = "";
            System.Reflection.PropertyInfo[] roleIds = Program.cfgjson.UserRoles.GetType().GetProperties().Where(predicate).ToArray();

            for (int i = 0; i < roleIds.Length; i++)
            {
                // quick patch to exclude giveaways role & insider chat role
                if ((ulong)roleIds[i].GetValue(Program.cfgjson.UserRoles, null) == Program.cfgjson.UserRoles.Giveaways ||
                    (ulong)roleIds[i].GetValue(Program.cfgjson.UserRoles, null) == Program.cfgjson.UserRoles.InsiderChat)
                    continue;

                DiscordRole roleToGrant = await guild.GetRoleAsync((ulong)roleIds[i].GetValue(Program.cfgjson.UserRoles, null));
                await ctx.Member.GrantRoleAsync(roleToGrant);

                if (roleIds.Length == 1)
                {
                    response += roleToGrant.Mention;
                }
                else
                {
                    response += i == roleIds.Length - 1 ? $"and {roleToGrant.Mention}" : $"{roleToGrant.Mention}{(roleIds.Length != 2 ? "," : String.Empty)} ";
                }
            }

            await ctx.Channel.SendMessageAsync($"{ctx.User.Mention} has joined the {response} role{(roleIds.Length != 1 ? "s" : String.Empty)}.");
        }

        public static async Task RemoveUserRolesAsync(TextCommandContext ctx, Func<System.Reflection.PropertyInfo, bool> predicate)
        {
            if (Program.cfgjson.UserRoles is null)
            {
                // Config hasn't been updated yet.
                return;
            }

            DiscordGuild guild = await Program.discord.GetGuildAsync(ctx.Guild.Id);
            System.Reflection.PropertyInfo[] roleIds = Program.cfgjson.UserRoles.GetType().GetProperties().Where(predicate).ToArray();
            foreach (System.Reflection.PropertyInfo roleId in roleIds)
            {
                // quick patch to exclude giveaways role & insider chat role
                if ((ulong)roleId.GetValue(Program.cfgjson.UserRoles, null) == Program.cfgjson.UserRoles.Giveaways ||
                    (ulong)roleId.GetValue(Program.cfgjson.UserRoles, null) == Program.cfgjson.UserRoles.InsiderChat)
                    continue;

                DiscordRole roleToGrant = await guild.GetRoleAsync((ulong)roleId.GetValue(Program.cfgjson.UserRoles, null));
                await ctx.Member.RevokeRoleAsync(roleToGrant);
            }

            await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":CliptokSuccess:"));
        }

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
            HomeServer,
            UserRolesPresent
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
            HomeServer,
            UserRolesPresent
        ]
        public async Task KeepMeUpdated(TextCommandContext ctx)
        {
            await GiveUserRolesAsync(ctx, x => true);
        }

        [
            Command("dont-keep-me-updatedtextcmd"),
            TextAlias("dont-keep-me-updated"),
            Description("Takes away from you all opt-in roles"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task DontKeepMeUpdated(TextCommandContext ctx)
        {
            await RemoveUserRolesAsync(ctx, x => true);
        }
    }
}
