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
                [SlashAutoCompleteProvider(typeof(Providers.RolesAutocompleteProvider))]
                [Parameter("role"), Description("The role to opt into.")] string role)
            {
                await ctx.DeferResponseAsync(ephemeral: true);

                DiscordMember member = ctx.Member;

                ulong roleId;
                try
                {
                    roleId = Convert.ToUInt64(role);
                }
                catch (FormatException)
                {
                    await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Invalid role! Please choose from the list.", ephemeral: true);
                    return;
                }

                if (roleId == Program.cfgjson.CommunityTechSupportRoleID && await GetPermLevelAsync(ctx.Member) < ServerPermLevel.TechnicalQueriesSlayer)
                {
                    await ctx.FollowupAsync($"{Program.cfgjson.Emoji.NoPermissions} You must be a TQS member to get the CTS role!", ephemeral: true);
                    return;
                }

                var roleData = await ctx.Guild.GetRoleAsync(roleId);

                await member.GrantRoleAsync(roleData, $"/roles grant used by {DiscordHelpers.UniqueUsername(ctx.User)}");
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Success} The role {roleData.Mention} has been successfully granted!", ephemeral: true);
            }

            [Command("remove")]
            [Description("Opt out of a role.")]
            public async Task RemoveRole(
                SlashCommandContext ctx,
                [SlashAutoCompleteProvider(typeof(Providers.RolesAutocompleteProvider))]
                [Parameter("role"), Description("The role to opt out of.")] string role)
            {
                await ctx.DeferResponseAsync(ephemeral: true);

                DiscordMember member = ctx.Member;

                ulong roleId;
                try
                {
                    roleId = Convert.ToUInt64(role);
                }
                catch (FormatException)
                {
                    await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Invalid role! Please choose from the list.", ephemeral: true);
                    return;
                }

                var roleData = await ctx.Guild.GetRoleAsync(roleId);

                await member.RevokeRoleAsync(roleData, $"/roles remove used by {DiscordHelpers.UniqueUsername(ctx.User)}");
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Success} The role {roleData.Mention} has been successfully removed!", ephemeral: true);
            }
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