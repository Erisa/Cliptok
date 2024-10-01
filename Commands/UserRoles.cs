namespace Cliptok.Commands
{
    [UserRolesPresent]
    public class UserRoleCmds
    {
        public static async Task GiveUserRoleAsync(TextCommandContext ctx, ulong role)
        {
            await GiveUserRolesAsync(ctx, x => (ulong)x.GetValue(Program.cfgjson.UserRoles, null) == role);
        }

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
                // quick patch to exclude giveaways role
                if ((ulong)roleIds[i].GetValue(Program.cfgjson.UserRoles, null) == Program.cfgjson.UserRoles.Giveaways)
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

        public static async Task RemoveUserRoleAsync(TextCommandContext ctx, ulong role)
        {
            // In case we ever decide to have individual commands to remove roles.
            await RemoveUserRolesAsync(ctx, x => (ulong)x.GetValue(Program.cfgjson.UserRoles, null) == role);
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
                // quick patch to exclude giveaways role
                if ((ulong)roleId.GetValue(Program.cfgjson.UserRoles, null) == Program.cfgjson.UserRoles.Giveaways)
                    continue;

                DiscordRole roleToGrant = await guild.GetRoleAsync((ulong)roleId.GetValue(Program.cfgjson.UserRoles, null));
                await ctx.Member.RevokeRoleAsync(roleToGrant);
            }

            await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":CliptokSuccess:"));
        }

        [
            Command("swap-insider-rp"),
            TextAlias("swap-insiders-rp"),
            Description("Removes the Windows 11 Insiders (Release Preview) role and replaces it with Windows 10 Insiders (Release Preview) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task SwapInsiderRpCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderRP);
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.Insider10RP);
        }

        [
            Command("swap-insider-dev"),
            TextAlias("swap-insiders-dev", "swap-insider-canary", "swap-insiders-canary", "swap-insider-can", "swap-insiders-can"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            Description("Removes the Windows 11 Insiders (Canary) role and replaces it with Windows 10 Insiders (Dev) role"),
            HomeServer
        ]
        public async Task SwapInsiderDevCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderCanary);
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderDev);
        }


        [
            Command("join-insider-dev"),
            TextAlias("join-insiders-dev"),
            Description("Gives you the Windows 11 Insiders (Dev) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task JoinInsiderDevCmd(TextCommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderDev);
        }

        [
            Command("join-insider-canary"),
            TextAlias("join-insiders-canary", "join-insider-can", "join-insiders-can"),
            Description("Gives you the Windows 11 Insiders (Canary) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task JoinInsiderCanaryCmd(TextCommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderCanary);
        }


        [
            Command("join-insider-beta"),
            TextAlias("join-insiders-beta"),
            Description("Gives you the Windows 11 Insiders (Beta) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task JoinInsiderBetaCmd(TextCommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderBeta);
        }

        [
            Command("join-insider-rp"),
            TextAlias("join-insiders-rp", "join-insiders-11-rp", "join-insider-11-rp"),
            Description("Gives you the Windows 11 Insiders (Release Preview) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task JoinInsiderRPCmd(TextCommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderRP);
        }

        [
            Command("join-insider-10"),
            TextAlias("join-insiders-10"),
            Description("Gives you to the Windows 10 Insiders (Release Preview) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task JoinInsiders10Cmd(TextCommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.Insider10RP);
        }

        [
            Command("join-patch-tuesday"),
            Description("Gives you the 💻 Patch Tuesday role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task JoinPatchTuesday(TextCommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.PatchTuesday);
        }

        [
            Command("keep-me-updated"),
            Description("Gives you all opt-in roles"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task KeepMeUpdated(TextCommandContext ctx)
        {
            await GiveUserRolesAsync(ctx, x => true);
        }

        [
            Command("leave-insiders"),
            TextAlias("leave-insider"),
            Description("Removes you from Insider roles"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task LeaveInsiders(TextCommandContext ctx)
        {
            foreach (ulong roleId in new ulong[] { Program.cfgjson.UserRoles.InsiderDev, Program.cfgjson.UserRoles.InsiderBeta, Program.cfgjson.UserRoles.InsiderRP, Program.cfgjson.UserRoles.InsiderCanary, Program.cfgjson.UserRoles.InsiderDev })
            {
                await RemoveUserRoleAsync(ctx, roleId);
            }

            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Insider} You are no longer receiving Windows Insider notifications. If you ever wish to receive Insider notifications again, you can check the <#740272437719072808> description for the commands.");
            var msg = await ctx.GetResponseAsync();
            await Task.Delay(10000);
            await msg.DeleteAsync();
        }

        [
            Command("dont-keep-me-updated"),
            Description("Takes away from you all opt-in roles"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task DontKeepMeUpdated(TextCommandContext ctx)
        {
            await RemoveUserRolesAsync(ctx, x => true);
        }

        [
            Command("leave-insider-dev"),
            TextAlias("leave-insiders-dev"),
            Description("Removes the Windows 11 Insiders (Dev) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task LeaveInsiderDevCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderDev);
        }

        [
            Command("leave-insider-canary"),
            TextAlias("leave-insiders-canary", "leave-insider-can", "leave-insiders-can"),
            Description("Removes the Windows 11 Insiders (Canary) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task LeaveInsiderCanaryCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderCanary);
        }

        [
            Command("leave-insider-beta"),
            TextAlias("leave-insiders-beta"),
            Description("Removes the Windows 11 Insiders (Beta) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task LeaveInsiderBetaCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderBeta);
        }

        [
            Command("leave-insider-10"),
            TextAlias("leave-insiders-10"),
            Description("Removes the Windows 10 Insiders (Release Preview) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task LeaveInsiderRPCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.Insider10RP);
        }

        [
            Command("leave-insider-rp"),
            TextAlias("leave-insiders-rp", "leave-insiders-11-rp", "leave-insider-11-rp"),
            Description("Removes the Windows 11 Insiders (Release Preview) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task LeaveInsider10RPCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderRP);
        }

        [
            Command("leave-patch-tuesday"),
            Description("Removes the 💻 Patch Tuesday role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task LeavePatchTuesday(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.PatchTuesday);
        }

    }
}
