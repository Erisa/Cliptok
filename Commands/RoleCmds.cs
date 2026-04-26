namespace Cliptok.Commands
{
    public class RoleCmds
    {
        static ulong rolesCmdId = 0;

        [Command("grant")]
        [Description("Grant a user access to the server, bypassing any verification requirements.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task Grant(CommandContext ctx, [Parameter("user"), Description("The user to grant server access to.")] DiscordUser user)
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

            await member.ModifyAsync(x =>
            {
                x.MemberFlags = (DiscordMemberFlags)member.MemberFlags | DiscordMemberFlags.BypassesVerification;
                x.AuditLogReason = $"grant command used by {DiscordHelpers.UniqueUsername(ctx.User)}";
            });

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
                        { "Windows 11 Insiders (Future Platforms)", "insiderCanary" },
                        { "Windows 11 Insiders (Experimental)", "insiderDev" },
                        { "Windows 11 Insiders (Beta)", "insiderBeta" },
                        { "Windows 11 Insiders (Release Preview)", "insiderRP" },
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