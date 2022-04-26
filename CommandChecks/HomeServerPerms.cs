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
            TrialModerator,
            Moderator,
            Admin,
            Owner = int.MaxValue
        }

        public static ServerPermLevel GetPermLevel(DiscordMember target)
        {
            if (target.Guild.Id != Program.cfgjson.ServerID)
                return ServerPermLevel.Nothing;

            // Torch approved of this.
            if (target.IsOwner)
                return ServerPermLevel.Owner;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.AdminRole)))
                return ServerPermLevel.Admin;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.ModRole)))
                return ServerPermLevel.Moderator;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.MutedRole)))
                return ServerPermLevel.Muted;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TrialModRole)))
                return ServerPermLevel.TrialModerator;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[9])))
                return ServerPermLevel.TierX;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[8])))
                return ServerPermLevel.TierS;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[7])))
                return ServerPermLevel.Tier8;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[6])))
                return ServerPermLevel.Tier7;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[5])))
                return ServerPermLevel.Tier6;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[4])))
                return ServerPermLevel.Tier5;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[3])))
                return ServerPermLevel.Tier4;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[2])))
                return ServerPermLevel.Tier3;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[1])))
                return ServerPermLevel.Tier2;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[0])))
                return ServerPermLevel.Tier1;
            else
                return ServerPermLevel.Nothing;
        }

        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
        public class RequireHomeserverPermAttribute : CheckBaseAttribute
        {
            public ServerPermLevel TargetLvl { get; set; }
            public bool WorkOutside { get; set; }

            public RequireHomeserverPermAttribute(ServerPermLevel targetlvl, bool workOutside = false)
            {
                WorkOutside = workOutside;
                TargetLvl = targetlvl;
            }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
            {
                // If the command is supposed to stay within the server and its being used outside, fail silently
                if (!WorkOutside && (ctx.Channel.IsPrivate || ctx.Guild.Id != Program.cfgjson.ServerID))
                    return false;

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
                        return false;
                    }
                }
                else
                {
                    member = ctx.Member;
                }

                var level = GetPermLevel(member);
                if (level >= TargetLvl)
                    return true;

                else if (!help && ctx.Command.QualifiedName != "edit")
                {
                    var levelText = level.ToString();
                    if (level == ServerPermLevel.Nothing && Program.rand.Next(1, 100) == 69)
                        levelText = $"naught but a thing, my dear human. Congratulations, you win {Program.rand.Next(1, 10)} bonus points.";

                    await ctx.RespondAsync(
                        $"{Program.cfgjson.Emoji.NoPermissions} Invalid permissions to use command **{ctx.Command.Name}**!\n" +
                        $"Required: `{TargetLvl}`\nYou have: `{levelText}`");
                }
                return false;
            }
        }

        public class HomeServerAttribute : CheckBaseAttribute
        {
            public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
            {
                return !ctx.Channel.IsPrivate && ctx.Guild.Id == Program.cfgjson.ServerID;
            }
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        public class SlashRequireHomeserverPermAttribute : SlashCheckBaseAttribute
        {
            public ServerPermLevel TargetLvl;

            public SlashRequireHomeserverPermAttribute(ServerPermLevel targetlvl)
                => TargetLvl = targetlvl;

            public override async Task<bool> ExecuteChecksAsync(InteractionContext ctx)
            {
                if (ctx.Guild.Id != Program.cfgjson.ServerID)
                    return false;

                var level = GetPermLevel(ctx.Member);
                if (level >= TargetLvl)
                    return true;
                else
                    return false;
            }
        }

    }
}
