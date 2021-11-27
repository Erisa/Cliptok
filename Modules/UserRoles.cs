using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Cliptok.Modules
{
    public class UserRolesPresentAttribute : CheckBaseAttribute
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            return Program.cfgjson.UserRoles != null;
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }

    public static class UserRoles
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
            // In case we ever decide to have indivdual commands to remove roles.
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

            try
            {
                await ctx.Message.DeleteAsync();
            }
            catch
            {
                // Not an important exception to note.
            }
        }

    }
    [UserRolesPresent]
    public class UserRoleCmds : BaseCommandModule
    {
        [
            Command("swap-insider-rp"),
            Aliases("swap-insiders-rp"),
            Description("Removes the Windows 11 Insiders (Release Preview) role and replaces it with Windows 10 Insiders (Release Preview) role"),
            HomeServer
        ]
        public async Task SwapInsiderRpCmd(CommandContext ctx)
        {
            await UserRoles.RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderRP);
            await UserRoles.GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.Insider10RP);
        }

        [
            Command("join-insider-dev"),
            Aliases("join-insiders-dev"),
            Description("Gives you the Windows 11 Insiders (Dev) role"),
            HomeServer
        ]
        public async Task JoinInsiderDevCmd(CommandContext ctx)
        {
            await UserRoles.GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderDev);
        }

        [
            Command("join-insider-beta"),
            Aliases("join-insiders-beta"),
            Description("Gives you the Windows 11 Insiders (Beta) role"),
            HomeServer
        ]
        public async Task JoinInsiderBetaCmd(CommandContext ctx)
        {
            await UserRoles.GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderBeta);
        }

        [
            Command("join-insider-rp"),
            Aliases("join-insiders-rp"),
            Description("Gives you the Windows 11 Insiders (Release Preview) role"),
            HomeServer
        ]
        public async Task JoinInsiderRPCmd(CommandContext ctx)
        {
            await UserRoles.GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderRP);
        }

        [
            Command("join-insider-10"),
            Aliases("join-insiders-10"),
            Description("Gives you to the Windows 10 Insiders (Release Preview) role"),
            HomeServer
        ]
        public async Task JoinInsiders10Cmd(CommandContext ctx)
        {
            await UserRoles.GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.Insider10RP);
        }

        [
            Command("join-patch-tuesday"),
            Description("Gives you the 💻 Patch Tuesday role"),
            HomeServer
        ]
        public async Task JoinPatchTuesday(CommandContext ctx)
        {
            await UserRoles.GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.PatchTuesday);
        }

        [
            Command("keep-me-updated"),
            Description("Gives you all opt-in roles"),
            HomeServer
        ]
        public async Task KeepMeUpdated(CommandContext ctx)
        {
            await UserRoles.GiveUserRolesAsync(ctx, x => true);
        }

        [
            Command("leave-insiders"),
            Aliases("leave-insider"),
            Description("Removes you from Insider roles"),
            HomeServer
        ]
        public async Task LeaveInsiders(CommandContext ctx)
        {
            foreach (ulong roleId in new ulong[] { Program.cfgjson.UserRoles.InsiderDev, Program.cfgjson.UserRoles.InsiderBeta, Program.cfgjson.UserRoles.InsiderRP })
            {
                await UserRoles.RemoveUserRoleAsync(ctx, roleId);
            }

            try
            {
                await ctx.Member.SendMessageAsync("Sad to see you go but if you ever want to rejoin Insiders and continue getting notifications type `!join-insider-dev` in <#740272437719072808> channel");
            }
            catch (DSharpPlus.Exceptions.UnauthorizedException)
            {
                // do nothing, not important if DMs are closed
            }
        }

        [
            Command("dont-keep-me-updated"),
            Description("Takes away from you all opt-in roles"),
            HomeServer
        ]
        public async Task DontKeepMeUpdated(CommandContext ctx)
        {
            await UserRoles.RemoveUserRolesAsync(ctx, x => true);
        }

        [
            Command("leave-insider-dev"),
            Aliases("leave-insiders-dev"),
            Description("Removes the Windows 11 Insiders (Dev) role"),
            HomeServer
        ]
        public async Task LeaveInsiderDevCmd(CommandContext ctx)
        {
            await UserRoles.RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderDev);
        }

        [
            Command("leave-insider-beta"),
            Aliases("leave-insiders-beta"),
            Description("Removes the Windows 11 Insiders (Beta) role"),
            HomeServer
        ]
        public async Task LeaveInsiderBetaCmd(CommandContext ctx)
        {
            await UserRoles.RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderBeta);
        }

        [
            Command("leave-insider-10"),
            Aliases("leave-insiders-10"),
            Description("Removes the Windows 10 Insiders (Release Preview) role"),
            HomeServer
        ]
        public async Task LeaveInsiderRPCmd(CommandContext ctx)
        {
            await UserRoles.RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.Insider10RP);
        }

        [
            Command("leave-insider-rp"),
            Aliases("leave-insiders-rp"),
            Description("Removes the Windows 11 Insiders (Release Preview) role"),
            HomeServer
        ]
        public async Task LeaveInsider10RPCmd(CommandContext ctx)
        {
            await UserRoles.RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderRP);
        }

        [
            Command("leave-patch-tuesday"),
            Description("Removes the 💻 Patch Tuesday role"),
            HomeServer
        ]
        public async Task LeavePatchTuesday(CommandContext ctx)
        {
            await UserRoles.RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.PatchTuesday);
        }

    }
}
