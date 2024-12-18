﻿using static Cliptok.Program;

namespace Cliptok.Events
{
    public class ErrorEvents
    {
        public static async Task CommandErrored(CommandsExtension _, CommandErroredEventArgs e)
        {
            // Because we no longer have DSharpPlus.CommandsNext or DSharpPlus.SlashCommands (only DSharpPlus.Commands), we can't point to different
            // error handlers based on command type in our command handler configuration. Instead, we can start here, and jump to the correct
            // handler based on the command type.
            
            // This is a lazy approach that just takes error type and points to the error handlers we already had.
            // Maybe it can be improved later?
            
            if (e.Context is TextCommandContext)
            {
                // Text command error
                await TextCommandErrored(e);
            }
            else if (e.Context is SlashCommandContext)
            {
                // Interaction command error (slash, user ctx, message ctx)
                await InteractionEvents.SlashCommandErrored(e);
            }
        }
        
        public static async Task TextCommandErrored(CommandErroredEventArgs e)
        {
            // strip out "textcmd" from text command names
            var commandName = e.Context.Command.FullName.Replace("textcmd", "");
            
            if (e.Exception is CommandNotFoundException && (e.Context.Command is null || commandName != "help"))
                return;

            // avoid conflicts with modmail
            if (commandName == "edit" || commandName == "timestamp")
                return;

            e.Context.Client.Logger.LogError(CliptokEventID, e.Exception, "Exception occurred during {user}s invocation of {command}", e.Context.User.Username, commandName);

            var exs = new List<Exception>();
            if (e.Exception is AggregateException ae)
                exs.AddRange(ae.InnerExceptions);
            else
                exs.Add(e.Exception);

            foreach (var ex in exs)
            {
                if (ex is CommandNotFoundException && (e.Context.Command is null || commandName != "help"))
                    return;
                
                // If the only exception thrown was an ArgumentParseException, run permission checks.
                // If the user fails the permission checks, show a permission error instead of the ArgumentParseException.
                if (ex is ArgumentParseException && exs.Count == 1)
                {
                    var att = e.Context.Command.Attributes.FirstOrDefault(x => x is RequireHomeserverPermAttribute) as RequireHomeserverPermAttribute;
                    var level = (await GetPermLevelAsync(e.Context.Member));
                    var levelText = level.ToString();
                    if (level == ServerPermLevel.Nothing && Program.rand.Next(1, 100) == 69)
                        levelText = $"naught but a thing, my dear human. Congratulations, you win {Program.rand.Next(1, 10)} bonus points.";

                    if (att is not null && level < att.TargetLvl)
                    {
                        await e.Context.RespondAsync(
                            $"{Program.cfgjson.Emoji.NoPermissions} Invalid permissions to use command **{commandName}**!\n" +
                            $"Required: `{att.TargetLvl}`\nYou have: `{levelText}`");
                        return;
                    }
                }

                if (ex is ChecksFailedException cfex && (commandName != "help"))
                {
                    // Iterate over RequireHomeserverPermAttribute failures.
                    // Only evaluate the last one, so that if we are looking at a command in a group (say, debug shutdown),
                    // we only evaluate against permissions for the command (shutdown) instead of the group (debug) in case they differ.
                    var permErrIndex = 1;
                    foreach(var permErr in cfex.Errors.Where(x => x.ContextCheckAttribute is RequireHomeserverPermAttribute))
                    {
                        // Only evaluate the last failed RequireHomeserverPermAttribute
                        if (permErrIndex == cfex.Errors.Count(x => x.ContextCheckAttribute is RequireHomeserverPermAttribute))
                        {
                            var att = permErr.ContextCheckAttribute as RequireHomeserverPermAttribute;
                            var level = (await GetPermLevelAsync(e.Context.Member));
                            var levelText = level.ToString();
                            if (level == ServerPermLevel.Nothing && Program.rand.Next(1, 100) == 69)
                                levelText = $"naught but a thing, my dear human. Congratulations, you win {Program.rand.Next(1, 10)} bonus points.";

                            await e.Context.RespondAsync(
                                $"{Program.cfgjson.Emoji.NoPermissions} Invalid permissions to use command **{commandName}**!\n" +
                                $"Required: `{att.TargetLvl}`\nYou have: `{levelText}`");
                            
                            return;
                        }
                        permErrIndex++;
                    }
                    return;
                }

                var embed = new DiscordEmbedBuilder
                {
                    Color = new DiscordColor("#FF0000"),
                    Title = "An exception occurred when executing a command",
                    Description = $"{cfgjson.Emoji.BSOD} `{e.Exception.GetType()}` occurred when executing `{commandName}`.",
                    Timestamp = DateTime.UtcNow
                };
                embed.WithFooter(discord.CurrentUser.Username, discord.CurrentUser.AvatarUrl)
                    .AddField("Message", ex.Message.Replace("textcmd", ""));
                if (e.Exception is System.ArgumentException or DSharpPlus.Commands.Exceptions.ArgumentParseException)
                    embed.AddField("Note", "This usually means that you used the command incorrectly.\n" +
                        "Please double-check how to use this command.");
                await e.Context.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }
    }
}
