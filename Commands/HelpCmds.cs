using System.Reflection;

namespace Cliptok.Commands
{
    public class HelpCmds
    {
        // Most of this is taken from DSharpPlus.CommandsNext and adapted to fit here.
        // https://github.com/DSharpPlus/DSharpPlus/blob/1c1aa15/DSharpPlus.CommandsNext/CommandsNextExtension.cs#L829
        [Command("helptextcmd"), Description("Displays command help.")]
        [TextAlias("help")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        public static async Task Help(CommandContext ctx, [Description("Command to provide help for."), RemainingText] string command = "")
        {
            var commandSplit = command.Split(' ');

            DiscordEmbedBuilder helpEmbed = new()
            {
                Title = "Help",
                Color = new DiscordColor("#0080ff")
            };

            IEnumerable<Command> cmds = ctx.Extension.Commands.Values.Where(cmd =>
                 cmd.Attributes.Any(attr => attr is AllowedProcessorsAttribute apAttr
                                            && apAttr.Processors.Contains(typeof(TextCommandProcessor)))
                 && !cmd.Attributes.Any(attr => attr is HiddenAttribute && command == ""));

            if (commandSplit.Length != 0 && commandSplit[0] != "")
            {
                commandSplit[0] += "textcmd";

                Command? cmd = null;
                IEnumerable<Command>? searchIn = cmds;
                for (int i = 0; i < commandSplit.Length; i++)
                {
                    if (searchIn is null)
                    {
                        cmd = null;
                        break;
                    }

                    StringComparison comparison = StringComparison.InvariantCultureIgnoreCase;
                    StringComparer comparer = StringComparer.InvariantCultureIgnoreCase;
                    cmd = searchIn.FirstOrDefault(xc => xc.Name.Equals(commandSplit[i], comparison) || xc.Name.Equals(commandSplit[i].Replace("textcmd", ""), comparison) || ((xc.Attributes.FirstOrDefault(x => x is TextAliasAttribute) as TextAliasAttribute)?.Aliases.Contains(commandSplit[i].Replace("textcmd", ""), comparer) ?? false));

                    if (cmd is null)
                    {
                        break;
                    }

                    // Only run checks on the last command in the chain.
                    // So if we are looking at a command group here, only run checks against the actual command,
                    // not the group(s) it's under.
                    if (i == commandSplit.Length - 1)
                    {
                        IEnumerable<ContextCheckAttribute> failedChecks = (await CheckPermissionsAsync(ctx, cmd)).ToList();
                        if (failedChecks.Any())
                        {
                            if (failedChecks.All(x => x is RequireHomeserverPermAttribute))
                            {
                                var att = failedChecks.FirstOrDefault(x => x is RequireHomeserverPermAttribute) as RequireHomeserverPermAttribute;
                                if (att is not null)
                                {
                                    var level = (await GetPermLevelAsync(ctx.Member));
                                    var levelText = level.ToString();
                                    if (level == ServerPermLevel.Nothing && Program.rand.Next(1, 100) == 69)
                                        levelText = $"naught but a thing, my dear human. Congratulations, you win {Program.rand.Next(1, 10)} bonus points.";

                                    await ctx.RespondAsync(
                                        $"{Program.cfgjson.Emoji.NoPermissions} Invalid permissions to use command **{command.Replace("textcmd", "")}**!\n" +
                                        $"Required: `{att.TargetLvl}`\nYou have: `{levelText}`");

                                    return;
                                }
                            }

                            return;
                        }
                    }

                    searchIn = cmd.Subcommands.Any() ? cmd.Subcommands : null;
                }

                if (cmd is null)
                {
                    throw new CommandNotFoundException(string.Join(" ", commandSplit));
                }

                helpEmbed.Description = $"`{cmd.Name.Replace("textcmd", "")}`: {cmd.Description ?? "No description provided."}";


                if (cmd.Subcommands.Count > 0 && cmd.Subcommands.Any(subCommand => subCommand.Attributes.Any(attr => attr is DefaultGroupCommandAttribute)))
                {
                    helpEmbed.Description += "\n\nThis group can be executed as a standalone command.";
                }

                var aliases = cmd.Method?.GetCustomAttributes<TextAliasAttribute>().FirstOrDefault()?.Aliases ?? (cmd.Attributes.FirstOrDefault(x => x is TextAliasAttribute) as TextAliasAttribute)?.Aliases ?? null;
                if (aliases is not null && (aliases.Length > 1 || (aliases.Length == 1 && aliases[0] != cmd.Name.Replace("textcmd", ""))))
                {
                    var aliasStr = "";
                    foreach (var alias in aliases)
                    {
                        if (alias == cmd.Name.Replace("textcmd", ""))
                            continue;

                        aliasStr += $"`{alias}`, ";
                    }
                    aliasStr = aliasStr.TrimEnd(',', ' ');
                    helpEmbed.AddField("Aliases", aliasStr);
                }

                var arguments = cmd.Method?.GetParameters();
                if (arguments is null)
                {
                    // This is a group command; try to show the arguments for the default subcommand
                    var defaultGroupCommand = cmd.Subcommands.FirstOrDefault(sc => sc.Attributes.Any(a => a is DefaultGroupCommandAttribute));
                    arguments = defaultGroupCommand?.Method?.GetParameters();
                }

                if (arguments is not null && arguments.Length > 0)
                {
                    var argumentsStr = $"`{cmd.Name.Replace("textcmd", "")}";
                    foreach (var arg in arguments)
                    {
                        if (arg.ParameterType == typeof(CommandContext) || arg.ParameterType.IsSubclassOf(typeof(CommandContext)))
                            continue;

                        bool isCatchAll = arg.GetCustomAttribute<RemainingTextAttribute>() != null;
                        argumentsStr += $"{(arg.IsOptional || isCatchAll ? " [" : " <")}{arg.Name}{(isCatchAll ? "..." : "")}{(arg.IsOptional || isCatchAll ? "]" : ">")}";
                    }

                    argumentsStr += "`\n";

                    foreach (var arg in arguments)
                    {
                        if (arg.ParameterType == typeof(CommandContext) || arg.ParameterType.IsSubclassOf(typeof(CommandContext)))
                            continue;

                        argumentsStr += $"`{arg.Name} ({arg.ParameterType.Name})`: {arg.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description provided."}\n";
                    }

                    helpEmbed.AddField("Arguments", argumentsStr.Trim());
                }
                //helpBuilder.WithCommand(cmd);

                if (cmd.Subcommands.Any())
                {
                    IEnumerable<Command> commandsToSearch = cmd.Subcommands;
                    List<Command> eligibleCommands = [];
                    foreach (Command? candidateCommand in commandsToSearch)
                    {
                        if (candidateCommand.Attributes.Any(x => x is AllowedProcessorsAttribute apa && !apa.Processors.Contains(typeof(TextCommandProcessor))))
                            continue;

                        var executionChecks = candidateCommand.Attributes.Where(x => x is ContextCheckAttribute);

                        if (executionChecks == null || !executionChecks.Any())
                        {
                            eligibleCommands.Add(candidateCommand);
                            continue;
                        }

                        IEnumerable<ContextCheckAttribute> candidateFailedChecks = await CheckPermissionsAsync(ctx, candidateCommand);
                        if (!candidateFailedChecks.Any())
                        {
                            eligibleCommands.Add(candidateCommand);
                        }
                    }

                    if (eligibleCommands.Count != 0)
                    {
                        eligibleCommands = eligibleCommands.OrderBy(x => x.Name).ToList();
                        string cmdList = "";
                        foreach (var subCommand in eligibleCommands)
                        {
                            cmdList += $"`{subCommand.Name}`, ";
                        }
                        helpEmbed.AddField("Subcommands", cmdList.TrimEnd(',', ' '));
                        //helpBuilder.WithSubcommands(eligibleCommands.OrderBy(xc => xc.Name));
                    }
                }
            }
            else
            {
                IEnumerable<Command> commandsToSearch = cmds;
                List<Command> eligibleCommands = [];
                foreach (Command? sc in commandsToSearch)
                {
                    var executionChecks = sc.Attributes.Where(x => x is ContextCheckAttribute);

                    if (!executionChecks.Any())
                    {
                        eligibleCommands.Add(sc);
                        continue;
                    }

                    IEnumerable<ContextCheckAttribute> candidateFailedChecks = await CheckPermissionsAsync(ctx, sc);
                    if (!candidateFailedChecks.Any())
                    {
                        eligibleCommands.Add(sc);
                    }
                }

                if (eligibleCommands.Count != 0)
                {
                    eligibleCommands = eligibleCommands.OrderBy(x => x.Name).ToList();
                    string cmdList = "";
                    foreach (var eligibleCommand in eligibleCommands)
                    {
                        cmdList += $"`{eligibleCommand.Name.Replace("textcmd", "")}`, ";
                    }
                    helpEmbed.AddField("Commands", cmdList.TrimEnd(',', ' '));
                    helpEmbed.Description = "Listing all top-level commands and groups. Specify a command to see more information.";
                    //helpBuilder.WithSubcommands(eligibleCommands.OrderBy(xc => xc.Name));
                }
            }

            DiscordMessageBuilder builder = new DiscordMessageBuilder().AddEmbed(helpEmbed);

            await ctx.RespondAsync(builder);
        }

        // Runs command context checks manually. Returns a list of failed checks.
        // Unfortunately DSharpPlus.Commands does not provide a way to execute a command's context checks manually,
        // so this will have to do. This may not include all checks, but it includes everything I could think of. -Milkshake
        private static async Task<IEnumerable<ContextCheckAttribute>> CheckPermissionsAsync(CommandContext ctx, Command cmd)
        {
            var contextChecks = cmd.Attributes.Where(x => x is ContextCheckAttribute);
            var failedChecks = new List<ContextCheckAttribute>();

            // similar to home server perm check logic
            DiscordMember member = null;
            if (ctx.Channel.IsPrivate || ctx.Guild.Id != Program.cfgjson.ServerID)
            {
                var guild = await ctx.Client.GetGuildAsync(Program.cfgjson.ServerID);
                try
                {
                    member = await guild.GetMemberAsync(ctx.User.Id);
                }
                catch (DSharpPlus.Exceptions.NotFoundException)
                {
                    // member is null, remember this for later
                }
            }
            else
            {
                member = ctx.Member;
            }

            foreach (var check in contextChecks)
            {
                // if command requiring homes erver is used outside, fail the check
                if (check is HomeServerAttribute homeServerAttribute
                    && (ctx.Channel.IsPrivate || ctx.Guild is null || ctx.Guild.Id != Program.cfgjson.ServerID)
                   )
                {
                    failedChecks.Add(homeServerAttribute);
                }

                if (check is RequireHomeserverPermAttribute requireHomeserverPermAttribute)
                {
                    // Fail if guild is wrong but this command does not work outside of the home server
                    if (
                        (ctx.Channel.IsPrivate || ctx.Guild is null || ctx.Guild.Id != Program.cfgjson.ServerID)
                        && !requireHomeserverPermAttribute.WorkOutside)
                    {
                        failedChecks.Add(requireHomeserverPermAttribute);
                    }
                    else
                    {
                        var level = await GetPermLevelAsync(member);
                        if (level < requireHomeserverPermAttribute.TargetLvl)
                        {
                            if (requireHomeserverPermAttribute.OwnerOverride && !Program.cfgjson.BotOwners.Contains(ctx.User.Id)
                                || !requireHomeserverPermAttribute.OwnerOverride)
                            {
                                failedChecks.Add(requireHomeserverPermAttribute);
                            }
                        }
                    }
                }

                if (check is RequirePermissionsAttribute requirePermissionsAttribute)
                {
                    if (member is null || ctx.Guild is null
                        || !ctx.Channel.PermissionsFor(ctx.Member).HasAllPermissions(requirePermissionsAttribute.UserPermissions)
                        || !ctx.Channel.PermissionsFor(ctx.Guild.CurrentMember).HasAllPermissions(requirePermissionsAttribute.BotPermissions))
                    {
                        failedChecks.Add(requirePermissionsAttribute);
                    }
                }

                if (check is IsBotOwnerAttribute isBotOwnerAttribute)
                {
                    if (!Program.cfgjson.BotOwners.Contains(ctx.User.Id))
                    {
                        failedChecks.Add(isBotOwnerAttribute);
                    }
                }

                if (check is UserRolesPresentAttribute userRolesPresentAttribute)
                {
                    if (Program.cfgjson.UserRoles is null)
                    {
                        failedChecks.Add(userRolesPresentAttribute);
                    }
                }
            }

            return failedChecks;
        }
    }
}
