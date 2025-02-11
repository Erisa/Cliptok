namespace Cliptok.Commands
{
    public class RoleCmds
    {
        [Command("grant")]
        [Description("Grant a user Tier 1, bypassing any verification requirements.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task Grant(CommandContext ctx, [Parameter("user"), Description("The user to grant Tier 1 to.")] DiscordUser user)
        {
            DiscordMember member = default;
            try
            {
                member = await ctx.Guild.GetMemberAsync(user.Id);
            }
            catch (Exception)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user does not appear to be in the server!");
                return;
            }

            if (!DiscordHelpers.AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to grant {member.Mention}! Check the role order.");
                return;
            }

            if (member.MemberFlags.Value.HasFlag(DiscordMemberFlags.BypassesVerification))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {member.Mention} has already been allowed access to the server!");
                return;
            }

            await member.ModifyAsync(x => x.MemberFlags = (DiscordMemberFlags)member.MemberFlags | DiscordMemberFlags.BypassesVerification);

            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} {member.Mention} can now access the server!");
        }

        [HomeServer]
        [Command("roles")]
        [Description("Opt in/out of roles.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        internal class RoleSlashCommands
        {
            [Command("grant")]
            [Description("Opt into a role.")]
            public async Task GrantRole(
                SlashCommandContext ctx,
                [SlashAutoCompleteProvider(typeof(RolesAutocompleteProvider))]
                [Parameter("role"), Description("The role to opt into.")] string role)
            {
                DiscordMember member = ctx.Member;

                ulong roleId = role switch
                {
                    "insiderCanary" => Program.cfgjson.UserRoles.InsiderCanary,
                    "insiderDev" => Program.cfgjson.UserRoles.InsiderDev,
                    "insiderBeta" => Program.cfgjson.UserRoles.InsiderBeta,
                    "insiderRP" => Program.cfgjson.UserRoles.InsiderRP,
                    "insider10RP" => Program.cfgjson.UserRoles.Insider10RP,
                    "patchTuesday" => Program.cfgjson.UserRoles.PatchTuesday,
                    "giveaways" => Program.cfgjson.UserRoles.Giveaways,
                    "cts" => Program.cfgjson.CommunityTechSupportRoleID,
                    _ => 0
                };

                if (roleId == 0)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Invalid role! Please choose from the list.", ephemeral: true);
                    return;
                }

                if (roleId == Program.cfgjson.CommunityTechSupportRoleID && await GetPermLevelAsync(ctx.Member) < ServerPermLevel.TechnicalQueriesSlayer)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.NoPermissions} You must be a TQS member to get the CTS role!", ephemeral: true);
                    return;
                }

                var roleData = await ctx.Guild.GetRoleAsync(roleId);

                await member.GrantRoleAsync(roleData, $"/roles grant used by {DiscordHelpers.UniqueUsername(ctx.User)}");
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} The role {roleData.Mention} has been successfully granted!", ephemeral: true, mentions: false);
            }

            [Command("remove")]
            [Description("Opt out of a role.")]
            public async Task RemoveRole(
                SlashCommandContext ctx,
                [SlashAutoCompleteProvider(typeof(RolesAutocompleteProvider))]
                [Parameter("role"), Description("The role to opt out of.")] string role)
            {
                DiscordMember member = ctx.Member;

                ulong roleId = role switch
                {
                    "insiderCanary" => Program.cfgjson.UserRoles.InsiderCanary,
                    "insiderDev" => Program.cfgjson.UserRoles.InsiderDev,
                    "insiderBeta" => Program.cfgjson.UserRoles.InsiderBeta,
                    "insiderRP" => Program.cfgjson.UserRoles.InsiderRP,
                    "insider10RP" => Program.cfgjson.UserRoles.Insider10RP,
                    "patchTuesday" => Program.cfgjson.UserRoles.PatchTuesday,
                    "giveaways" => Program.cfgjson.UserRoles.Giveaways,
                    "cts" => Program.cfgjson.CommunityTechSupportRoleID,
                    _ => 0
                };

                if (roleId == 0)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Invalid role! Please choose from the list.", ephemeral: true);
                    return;
                }

                var roleData = await ctx.Guild.GetRoleAsync(roleId);

                await member.RevokeRoleAsync(roleData, $"/roles remove used by {DiscordHelpers.UniqueUsername(ctx.User)}");
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} The role {roleData.Mention} has been successfully removed!", ephemeral: true, mentions: false);
            }
        }

        internal class RolesAutocompleteProvider : IAutoCompleteProvider
        {
            public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext ctx)
            {
                Dictionary<string, string> options = new()
                    {
                        { "Windows 11 Insiders (Canary)", "insiderCanary" },
                        { "Windows 11 Insiders (Dev)", "insiderDev" },
                        { "Windows 11 Insiders (Beta)", "insiderBeta" },
                        { "Windows 11 Insiders (Release Preview)", "insiderRP" },
                        { "Windows 10 Insiders (Release Preview)", "insider10RP" },
                        { "Patch Tuesday", "patchTuesday" },
                        { "Giveaways", "giveaways" },
                        { "Community Tech Support (CTS)", "cts" }
                    };

                var memberHasTqs = await GetPermLevelAsync(ctx.Member) >= ServerPermLevel.TechnicalQueriesSlayer;

                List<DiscordAutoCompleteChoice> list = new();

                foreach (var option in options)
                {
                    var focusedOption = ctx.Options.FirstOrDefault(option => option.Focused);
                    if (focusedOption.Value.ToString() == "" || option.Key.Contains(focusedOption.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        if (option.Value == "cts" && !memberHasTqs) continue;
                        list.Add(new DiscordAutoCompleteChoice(option.Key, option.Value));
                    }
                }

                return list;
            }
        }

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
            Command("swap-insider-rptextcmd"),
            TextAlias("swap-insider-rp", "swap-insiders-rp"),
            Description("Removes the Windows 11 Insiders (Release Preview) role and replaces it with Windows 10 Insiders (Release Preview) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task SwapInsiderRpCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderRP);
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.Insider10RP);
        }

        [
            Command("swap-insider-devtextcmd"),
            TextAlias("swap-insider-dev", "swap-insiders-dev", "swap-insider-canary", "swap-insiders-canary", "swap-insider-can", "swap-insiders-can"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            Description("Removes the Windows 11 Insiders (Canary) role and replaces it with Windows 10 Insiders (Dev) role"),
            HomeServer,
            UserRolesPresent
        ]
        public async Task SwapInsiderDevCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderCanary);
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderDev);
        }


        [
            Command("join-insider-devtextcmd"),
            TextAlias("join-insider-dev", "join-insiders-dev"),
            Description("Gives you the Windows 11 Insiders (Dev) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task JoinInsiderDevCmd(TextCommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderDev);
        }

        [
            Command("join-insider-canarytextcmd"),
            TextAlias("join-insider-canary", "join-insiders-canary", "join-insider-can", "join-insiders-can"),
            Description("Gives you the Windows 11 Insiders (Canary) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task JoinInsiderCanaryCmd(TextCommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderCanary);
        }


        [
            Command("join-insider-betatextcmd"),
            TextAlias("join-insider-beta", "join-insiders-beta"),
            Description("Gives you the Windows 11 Insiders (Beta) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task JoinInsiderBetaCmd(TextCommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderBeta);
        }

        [
            Command("join-insider-rptextcmd"),
            TextAlias("join-insider-rp", "join-insiders-rp", "join-insiders-11-rp", "join-insider-11-rp"),
            Description("Gives you the Windows 11 Insiders (Release Preview) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task JoinInsiderRPCmd(TextCommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderRP);
        }

        [
            Command("join-insider-10textcmd"),
            TextAlias("join-insider-10", "join-insiders-10"),
            Description("Gives you to the Windows 10 Insiders (Release Preview) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task JoinInsiders10Cmd(TextCommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.Insider10RP);
        }

        [
            Command("join-patch-tuesdaytextcmd"),
            TextAlias("join-patch-tuesday"),
            Description("Gives you the 💻 Patch Tuesday role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task JoinPatchTuesday(TextCommandContext ctx)
        {
            await GiveUserRoleAsync(ctx, Program.cfgjson.UserRoles.PatchTuesday);
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
            Command("leave-insiderstextcmd"),
            TextAlias("leave-insiders", "leave-insider"),
            Description("Removes you from Insider roles"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
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

        [
            Command("leave-insider-devtextcmd"),
            TextAlias("leave-insider-dev", "leave-insiders-dev"),
            Description("Removes the Windows 11 Insiders (Dev) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task LeaveInsiderDevCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderDev);
        }

        [
            Command("leave-insider-canarytextcmd"),
            TextAlias("leave-insider-canary", "leave-insiders-canary", "leave-insider-can", "leave-insiders-can"),
            Description("Removes the Windows 11 Insiders (Canary) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task LeaveInsiderCanaryCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderCanary);
        }

        [
            Command("leave-insider-betatextcmd"),
            TextAlias("leave-insider-beta", "leave-insiders-beta"),
            Description("Removes the Windows 11 Insiders (Beta) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task LeaveInsiderBetaCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderBeta);
        }

        [
            Command("leave-insider-10textcmd"),
            TextAlias("leave-insider-10", "leave-insiders-10"),
            Description("Removes the Windows 10 Insiders (Release Preview) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task LeaveInsiderRPCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.Insider10RP);
        }

        [
            Command("leave-insider-rptextcmd"),
            TextAlias("leave-insider-rp", "leave-insiders-rp", "leave-insiders-11-rp", "leave-insider-11-rp"),
            Description("Removes the Windows 11 Insiders (Release Preview) role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task LeaveInsider10RPCmd(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.InsiderRP);
        }

        [
            Command("leave-patch-tuesdaytextcmd"),
            TextAlias("leave-patch-tuesday"),
            Description("Removes the 💻 Patch Tuesday role"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            UserRolesPresent
        ]
        public async Task LeavePatchTuesday(TextCommandContext ctx)
        {
            await RemoveUserRoleAsync(ctx, Program.cfgjson.UserRoles.PatchTuesday);
        }
    }
}