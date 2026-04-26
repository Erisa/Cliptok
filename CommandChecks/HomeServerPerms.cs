namespace Cliptok.CommandChecks
{
    public class ServerPerms
    {
        public enum ServerPermLevel
        {
            Muted = -1,
            Nothing = 0,
            Tier1,
            Tier2,
            Tier3,
            Tier4,
            Tier5,
            Tier6,
            Tier7,
            Tier8,
            TierS,
            TierX,
            TechnicalQueriesSlayer,
            TrialModerator,
            Moderator,
            Admin,
            Owner = int.MaxValue
        }

        public static async Task<ServerPermLevel> GetPermLevelAsync(DiscordMember target)
        {
            if (target is null || target.Guild is null || target.Guild.Id != Program.cfgjson.ServerID)
                return ServerPermLevel.Nothing;

            if (target.IsOwner)
                return ServerPermLevel.Owner;

            if (target.Id == Program.discord.CurrentUser.Id)
                return ServerPermLevel.Admin;

            bool HasRole(ulong roleId) => target.Roles.Any(r => r.Id == roleId);

            return HasRole(Program.cfgjson.AdminRole) ? ServerPermLevel.Admin
                : HasRole(Program.cfgjson.ModRole) ? ServerPermLevel.Moderator
                : HasRole(Program.cfgjson.MutedRole) ? ServerPermLevel.Muted
                : HasRole(Program.cfgjson.TrialModRole) ? ServerPermLevel.TrialModerator
                : HasRole(Program.cfgjson.TqsRoleId) ? ServerPermLevel.TechnicalQueriesSlayer
                : HasRole(Program.cfgjson.TierRoles[9]) ? ServerPermLevel.TierX
                : HasRole(Program.cfgjson.TierRoles[8]) ? ServerPermLevel.TierS
                : HasRole(Program.cfgjson.TierRoles[7]) ? ServerPermLevel.Tier8
                : HasRole(Program.cfgjson.TierRoles[6]) ? ServerPermLevel.Tier7
                : HasRole(Program.cfgjson.TierRoles[5]) ? ServerPermLevel.Tier6
                : HasRole(Program.cfgjson.TierRoles[4]) ? ServerPermLevel.Tier5
                : HasRole(Program.cfgjson.TierRoles[3]) ? ServerPermLevel.Tier4
                : HasRole(Program.cfgjson.TierRoles[2]) ? ServerPermLevel.Tier3
                : HasRole(Program.cfgjson.TierRoles[1]) ? ServerPermLevel.Tier2
                : HasRole(Program.cfgjson.TierRoles[0]) ? ServerPermLevel.Tier1
                : ServerPermLevel.Nothing;
        }

        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
        public class RequireHomeserverPermAttribute : ContextCheckAttribute
        {
            public ServerPermLevel TargetLvl { get; set; }
            public bool WorkOutside { get; set; }

            public bool OwnerOverride { get; set; }

            public RequireHomeserverPermAttribute(ServerPermLevel targetlvl, bool workOutside = false, bool ownerOverride = false)
            {
                WorkOutside = workOutside;
                OwnerOverride = ownerOverride;
                TargetLvl = targetlvl;
            }
        }

        public class RequireHomeserverPermCheck : IContextCheck<RequireHomeserverPermAttribute>
        {
            public async ValueTask<string?> ExecuteCheckAsync(RequireHomeserverPermAttribute attribute, CommandContext ctx)
            {
                // If the command is supposed to stay within the server and its being used outside, fail silently
                if (!attribute.WorkOutside && (ctx.Channel.IsPrivate || ctx.Guild.Id != Program.cfgjson.ServerID))
                    return "This command must be used in the home server, but was executed outside of it.";

                // bot owners can bypass perm checks ONLY if the command allows it.
                if (attribute.OwnerOverride && Program.cfgjson.BotOwners.Contains(ctx.User.Id))
                    return null;

                DiscordMember member;
                if (ctx.Channel.IsPrivate || ctx.Guild.Id != Program.cfgjson.ServerID)
                {
                    var guild = await ctx.Client.GetGuildAsync(Program.cfgjson.ServerID);
                    try
                    {
                        member = await guild.GetMemberAsync(ctx.User.Id);
                    }
                    catch (DSharpPlus.Exceptions.NotFoundException)
                    {
                        return "The invoking user must be a member of the home server; they are not.";
                    }
                }
                else
                {
                    member = ctx.Member;
                }

                var level = await GetPermLevelAsync(member);
                if (level >= attribute.TargetLvl)
                    return null;

                return "The invoking user does not have permission to use this command.";
            }
        }

        public class HomeServerAttribute : ContextCheckAttribute;

        public class HomeServerCheck : IContextCheck<HomeServerAttribute>
        {
            public async ValueTask<string?> ExecuteCheckAsync(HomeServerAttribute attribute, CommandContext ctx)
            {
                return !ctx.Channel.IsPrivate && ctx.Guild.Id == Program.cfgjson.ServerID ? null : "This command must be used in the home server, but was executed outside of it.";
            }
        }
    }
}
