namespace Cliptok.Commands
{
    [UserRolesPresent]
    public class UserRoleCmds : BaseCommandModule
    {
        public static async Task GiveUserRoleAsync(CommandContext ctx, ulong role)
        {
            await GiveUserRolesAsync(ctx, x => (ulong)x.GetValue(Program.cfgjson.UserRoles, null) == role);
        }

        public static async Task GiveUserRolesAsync(CommandContext ctx, Func<System.Reflection.PropertyInfo, bool> predicate)
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
                DiscordRole roleToGrant = guild.GetRole((ulong)roleIds[i].GetValue(Program.cfgjson.UserRoles, null));
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

        public static async Task RemoveUserRoleAsync(CommandContext ctx, ulong role)
        {
            // In case we ever decide to have individual commands to remove roles.
            await RemoveUserRolesAsync(ctx, x => (ulong)x.GetValue(Program.cfgjson.UserRoles, null) == role);
        }

        public static async Task RemoveUserRolesAsync(CommandContext ctx, Func<System.Reflection.PropertyInfo, bool> predicate)
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
                DiscordRole roleToGrant = guild.GetRole((ulong)roleId.GetValue(Program.cfgjson.UserRoles, null));
                await ctx.Member.RevokeRoleAsync(roleToGrant);
            }

            await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":CliptokSuccess:"));
        }

        [
            Command("swap-insider-rp"),
            Aliases("swap-insiders-rp"),
            Description("Removes the Windows 11 Insiders (Release Preview) role and replaces it with Windows 10 Insiders (Release Preview) role"),
            HomeServer
        ]
        public async Task SwapInsiderRpCmd(CommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderRP);
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.Insider10RP);
        }

        [
            Command("swap-insider-dev"),
            Aliases("swap-insiders-dev", "swap-insider-canary", "swap-insiders-canary", "swap-insider-can", "swap-insiders-can"),
            Description("Removes the Windows 11 Insiders (Canary) role and replaces it with Windows 10 Insiders (Dev) role"),
            HomeServer
        ]
        public async Task SwapInsiderDevCmd(CommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderCanary);
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderDev);
        }


        [
            Command("join-insider-dev"),
            Aliases("join-insiders-dev"),
            Description("Gives you the Windows 11 Insiders (Dev) role"),
            HomeServer
        ]
        public async Task JoinInsiderDevCmd(CommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderDev);
        }

        [
            Command("join-insider-canary"),
            Aliases("join-insiders-canary", "join-insider-can", "join-insiders-can"),
            Description("Gives you the Windows 11 Insiders (Canary) role"),
            HomeServer
        ]
        public async Task JoinInsiderCanaryCmd(CommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderCanary);
        }


        [
            Command("join-insider-beta"),
            Aliases("join-insiders-beta"),
            Description("Gives you the Windows 11 Insiders (Beta) role"),
            HomeServer
        ]
        public async Task JoinInsiderBetaCmd(CommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderBeta);
        }

        [
            Command("join-insider-rp"),
            Aliases("join-insiders-rp", "join-insiders-11-rp", "join-insider-11-rp"),
            Description("Gives you the Windows 11 Insiders (Release Preview) role"),
            HomeServer
        ]
        public async Task JoinInsiderRPCmd(CommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderRP);
        }

        [
            Command("join-insider-10"),
            Aliases("join-insiders-10"),
            Description("Gives you to the Windows 10 Insiders (Release Preview) role"),
            HomeServer
        ]
        public async Task JoinInsiders10Cmd(CommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.Insider10RP);
        }

        [
            Command("join-patch-tuesday"),
            Description("Gives you the 💻 Patch Tuesday role"),
            HomeServer
        ]
        public async Task JoinPatchTuesday(CommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.PatchTuesday);
        }

        [
            Command("keep-me-updated"),
            Description("Gives you all opt-in roles"),
            HomeServer
        ]
        public async Task KeepMeUpdated(CommandContext ctx)
        {
            await GiveUserRolesAsync(ctx, x => true);
        }

        [
            Command("leave-insiders"),
            Aliases("leave-insider"),
            Description("Removes you from Insider roles"),
            HomeServer
        ]
        public async Task LeaveInsiders(CommandContext ctx)
        {
            foreach (ulong roleId in new ulong[] { Program.cfgjson.UserRoles.InsiderDev, Program.cfgjson.UserRoles.InsiderBeta, Program.cfgjson.UserRoles.InsiderRP, Program.cfgjson.UserRoles.InsiderCanary, Program.cfgjson.UserRoles.InsiderDev })
            {
                await RemoveUserRoleAsync(ctx, roleId);
            }

            var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Insider} You are no longer receiving Windows Insider notifications. If you ever wish to receive Insider notifications again, you can check the <#740272437719072808> description for the commands.");
            await Task.Delay(10000);
            await msg.DeleteAsync();
        }

        [
            Command("dont-keep-me-updated"),
            Description("Takes away from you all opt-in roles"),
            HomeServer
        ]
        public async Task DontKeepMeUpdated(CommandContext ctx)
        {
            await RemoveUserRolesAsync(ctx, x => true);
        }

        [
            Command("leave-insider-dev"),
            Aliases("leave-insiders-dev"),
            Description("Removes the Windows 11 Insiders (Dev) role"),
            HomeServer
        ]
        public async Task LeaveInsiderDevCmd(CommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderDev);
        }

        [
            Command("leave-insider-canary"),
            Aliases("leave-insiders-canary", "leave-insider-can", "leave-insiders-can"),
            Description("Removes the Windows 11 Insiders (Canary) role"),
            HomeServer
        ]
        public async Task LeaveInsiderCanaryCmd(CommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderCanary);
        }

        [
            Command("leave-insider-beta"),
            Aliases("leave-insiders-beta"),
            Description("Removes the Windows 11 Insiders (Beta) role"),
            HomeServer
        ]
        public async Task LeaveInsiderBetaCmd(CommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderBeta);
        }

        [
            Command("leave-insider-10"),
            Aliases("leave-insiders-10"),
            Description("Removes the Windows 10 Insiders (Release Preview) role"),
            HomeServer
        ]
        public async Task LeaveInsiderRPCmd(CommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.Insider10RP);
        }

        [
            Command("leave-insider-rp"),
            Aliases("leave-insiders-rp", "leave-insiders-11-rp", "leave-insider-11-rp"),
            Description("Removes the Windows 11 Insiders (Release Preview) role"),
            HomeServer
        ]
        public async Task LeaveInsider10RPCmd(CommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderRP);
        }

        [
            Command("leave-patch-tuesday"),
            Description("Removes the 💻 Patch Tuesday role"),
            HomeServer
        ]
        public async Task LeavePatchTuesday(CommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.PatchTuesday);
        }

    }
}
