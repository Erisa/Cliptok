namespace Cliptok.Commands
{
    public class TransferCmds
    {
        [Command("transfer")]
        [Description("Transfer data from one user to another.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [RequirePermissions(DiscordPermission.ModerateMembers)]
        public class TransferSlashCommands
        {
            [Command("warnings")]
            [Description("Transfer warnings from one user to another.")]
            public async Task TransferWarningsSlashCommand(SlashCommandContext ctx,
                [Parameter("source_user"), Description("The user currently holding the warnings.")] DiscordUser sourceUser,
                [Parameter("target_user"), Description("The user receiving the warnings.")] DiscordUser targetUser,
                [Parameter("merge"), Description("Whether to merge the source user's warnings and the target user's warnings.")] bool merge = false,
                [Parameter("force_override"), Description("DESTRUCTIVE OPERATION: Whether to OVERRIDE and DELETE the target users existing warnings.")] bool forceOverride = false
            )
            {
                await ctx.DeferResponseAsync(false);
                
                var result = await TransferWarningsAsync(sourceUser, targetUser, ctx.User, merge, forceOverride);
                
                await ctx.FollowupAsync(GetTransferResponseMessage(result, null, sourceUser, targetUser, merge, forceOverride));
            }
            
            [Command("notes")]
            [Description("Transfer notes from one user to another.")]
            public async Task TransferNotesSlashCommand(SlashCommandContext ctx,
                [Parameter("source_user"), Description("The user currently holding the notes.")] DiscordUser sourceUser,
                [Parameter("target_user"), Description("The user receiving the notes.")] DiscordUser targetUser,
                [Parameter("merge"), Description("Whether to merge the source user's notes and the target user's notes.")] bool merge = false,
                [Parameter("force_override"), Description("DESTRUCTIVE OPERATION: Whether to OVERRIDE and DELETE the target users existing notes.")] bool forceOverride = false
            )
            {
                await ctx.DeferResponseAsync(false);
                
                var result = await TransferNotesAsync(sourceUser, targetUser, ctx.User, merge, forceOverride);
                
                await ctx.FollowupAsync(GetTransferResponseMessage(null, result, sourceUser, targetUser, merge, forceOverride));
            }
            
            [Command("all")]
            [Description("Transfer all data from one user to another (warnings and notes).")]
            public async Task TransferAllSlashCommand(SlashCommandContext ctx,
                [Parameter("source_user"), Description("The user currently holding the data.")] DiscordUser sourceUser,
                [Parameter("target_user"), Description("The user receiving the data.")] DiscordUser targetUser,
                [Parameter("merge"), Description("Whether to merge the source user's data and the target user's data.")] bool merge = false,
                [Parameter("force_override"), Description("DESTRUCTIVE OPERATION: Whether to OVERRIDE and DELETE the target users existing data.")] bool forceOverride = false
            )
            {
                await ctx.DeferResponseAsync(false);
                
                var warningResult = await TransferWarningsAsync(sourceUser, targetUser, ctx.User, merge, forceOverride);
                var noteResult = await TransferNotesAsync(sourceUser, targetUser, ctx.User, merge, forceOverride);
                
                await ctx.FollowupAsync(GetTransferResponseMessage(warningResult, noteResult, sourceUser, targetUser, merge, forceOverride));
            }
            
            private static async Task<TransferResult> TransferWarningsAsync(DiscordUser sourceUser, DiscordUser targetUser, DiscordUser modUser, bool merge = false, bool forceOverride = false)
            {
                if (sourceUser == targetUser)
                    return TransferResult.FailedSameUser;
                
                var sourceWarnings = (await Program.redis.HashGetAllAsync(sourceUser.Id.ToString()))
                    .Where(x => JsonConvert.DeserializeObject<UserWarning>(x.Value).Type == WarningType.Warning).ToDictionary(
                        x => x.Name.ToString(),
                        x => JsonConvert.DeserializeObject<UserWarning>(x.Value)
                    );
                var targetWarnings = (await Program.redis.HashGetAllAsync(targetUser.Id.ToString()))
                    .Where(x => JsonConvert.DeserializeObject<UserWarning>(x.Value).Type == WarningType.Warning).ToDictionary(
                        x => x.Name.ToString(),
                        x => JsonConvert.DeserializeObject<UserWarning>(x.Value)
                    );

                if (sourceWarnings.Count == 0)
                {
                    return TransferResult.FailedNoSourceData;
                }
                else if (merge)
                {
                    foreach (var warning in sourceWarnings)
                    {
                        await Program.redis.HashSetAsync(targetUser.Id.ToString(), warning.Key, JsonConvert.SerializeObject(warning.Value));
                        await Program.redis.HashDeleteAsync(sourceUser.Id.ToString(), warning.Key);
                    }
                }
                else if (targetWarnings.Count > 0 && !forceOverride)
                {
                    return TransferResult.FailedConflict;
                }
                else if (targetWarnings.Count > 0 && forceOverride)
                {
                    // Delete all target warnings
                    foreach (var warning in targetWarnings)
                        await Program.redis.HashDeleteAsync(targetUser.Id.ToString(), warning.Key);
                    
                    // Add source warnings
                    foreach (var warning in sourceWarnings)
                    {
                        await Program.redis.HashSetAsync(targetUser.Id.ToString(), warning.Key, JsonConvert.SerializeObject(warning.Value));
                        await Program.redis.HashDeleteAsync(sourceUser.Id.ToString(), warning.Key);
                    }
                }
                else
                {
                    foreach (var warning in sourceWarnings)
                    {
                        await Program.redis.HashSetAsync(targetUser.Id.ToString(), warning.Key, JsonConvert.SerializeObject(warning.Value));
                        await Program.redis.HashDeleteAsync(sourceUser.Id.ToString(), warning.Key);
                    }
                }

                string operationText = "";
                if (merge)
                    operationText = "merge ";
                else if (forceOverride)
                    operationText = "force ";
                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Information} Warnings from {sourceUser.Mention} were {operationText}transferred to {targetUser.Mention} by `{DiscordHelpers.UniqueUsername(modUser)}`")
                        .AddEmbed(await WarningHelpers.GenerateWarningsEmbedAsync(targetUser))
                );
                
                return TransferResult.Success;
            }
            
            private static async Task<TransferResult> TransferNotesAsync(DiscordUser sourceUser, DiscordUser targetUser, DiscordUser modUser, bool merge = false, bool forceOverride = false)
            {
                if (sourceUser == targetUser)
                    return TransferResult.FailedSameUser;
                
                var sourceNotes = (await Program.redis.HashGetAllAsync(sourceUser.Id.ToString()))
                    .Where(x => JsonConvert.DeserializeObject<UserNote>(x.Value).Type == WarningType.Note).ToDictionary(
                        x => x.Name.ToString(),
                        x => JsonConvert.DeserializeObject<UserNote>(x.Value)
                    );
                var targetNotes = (await Program.redis.HashGetAllAsync(targetUser.Id.ToString()))
                    .Where(x => JsonConvert.DeserializeObject<UserNote>(x.Value).Type == WarningType.Note).ToDictionary(
                        x => x.Name.ToString(),
                        x => JsonConvert.DeserializeObject<UserNote>(x.Value)
                    );

                if (sourceNotes.Count == 0)
                {
                    return TransferResult.FailedNoSourceData;
                }
                else if (merge)
                {
                    foreach (var note in sourceNotes)
                    {
                        await Program.redis.HashSetAsync(targetUser.Id.ToString(), note.Key, JsonConvert.SerializeObject(note.Value));
                        await Program.redis.HashDeleteAsync(sourceUser.Id.ToString(), note.Key);
                    }
                }
                else if (targetNotes.Count > 0 && !forceOverride)
                {
                    return TransferResult.FailedConflict;
                }
                else if (targetNotes.Count > 0 && forceOverride)
                {
                    // Delete all target notes
                    foreach (var note in targetNotes)
                        await Program.redis.HashDeleteAsync(targetUser.Id.ToString(), note.Key);
                    
                    // Add source warnings
                    foreach (var note in sourceNotes)
                    {
                        await Program.redis.HashSetAsync(targetUser.Id.ToString(), note.Key, JsonConvert.SerializeObject(note.Value));
                        await Program.redis.HashDeleteAsync(sourceUser.Id.ToString(), note.Key);
                    }
                }
                else
                {
                    foreach (var note in sourceNotes)
                    {
                        await Program.redis.HashSetAsync(targetUser.Id.ToString(), note.Key, JsonConvert.SerializeObject(note.Value));
                        await Program.redis.HashDeleteAsync(sourceUser.Id.ToString(), note.Key);
                    }
                }

                string operationText = "";
                if (merge)
                    operationText = "merge ";
                else if (forceOverride)
                    operationText = "force ";
                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Information} Notes from {sourceUser.Mention} were {operationText}transferred to {targetUser.Mention} by `{DiscordHelpers.UniqueUsername(modUser)}`")
                        .AddEmbed(await WarningHelpers.GenerateWarningsEmbedAsync(targetUser))
                );
                
                return TransferResult.Success;
            }
            
            private enum TransferResult
            {
                Success,
                FailedSameUser,
                FailedNoSourceData,
                FailedConflict
            }
            
            private static string GetTransferResponseMessage(TransferResult? warningsTransferResult, TransferResult? notesTransferResult, DiscordUser sourceUser, DiscordUser targetUser, bool merge = false, bool forceOverride = false)
            {
                string operationText = "";
                if (merge)
                    operationText = "merge ";
                else if (forceOverride)
                    operationText = "force ";
                
                // If both transfers had the same result, return one output message where warnings/notes/etc. are just called "data"
                if (warningsTransferResult == notesTransferResult)
                    return warningsTransferResult switch
                    {
                        TransferResult.Success => $"{Program.cfgjson.Emoji.Success} Successfully {operationText}transferred data from {sourceUser.Mention} to {targetUser.Mention}!\n",
                        TransferResult.FailedSameUser => $"{Program.cfgjson.Emoji.Error} The source and target users cannot be the same!\n",
                        TransferResult.FailedNoSourceData => $"{Program.cfgjson.Emoji.Error} The source user has no data to transfer.\n",
                        TransferResult.FailedConflict => $"{Program.cfgjson.Emoji.Warning} **CAUTION**: The target user has data.\n\n" +
                                                         $"If you are sure you want to **OVERRIDE** and **DELETE** this data, please consider the consequences before adding `force_override: True` to the command.\nIf you wish to **NOT** override the target's data, please use `merge: True` instead.\n",
                    };
                
                // Otherwise return a message for each result
                string response = "";
                switch (warningsTransferResult)
                {
                    case TransferResult.Success:
                        response += $"{Program.cfgjson.Emoji.Success} Successfully {operationText}transferred warnings from {sourceUser.Mention} to {targetUser.Mention}!\n";
                        break;
                    case TransferResult.FailedSameUser:
                        response += $"{Program.cfgjson.Emoji.Error} The source and target users cannot be the same!\n";
                        break;
                    case TransferResult.FailedNoSourceData:
                        response += $"{Program.cfgjson.Emoji.Error} The source user has no warnings to transfer.\n";
                        break;
                    case TransferResult.FailedConflict:
                        response += $"{Program.cfgjson.Emoji.Warning} **CAUTION**: The target user has warnings.\n\n" +
                                   $"If you are sure you want to **OVERRIDE** and **DELETE** these warnings, please consider the consequences before adding `force_override: True` to the command.\nIf you wish to **NOT** override the target's warnings, please use `merge: True` instead.\n";
                        break;
                }
                
                switch (notesTransferResult)
                {
                    case TransferResult.Success:
                        response += $"{Program.cfgjson.Emoji.Success} Successfully {operationText}transferred notes from {sourceUser.Mention} to {targetUser.Mention}!";
                        break;
                    case TransferResult.FailedSameUser:
                        response += $"{Program.cfgjson.Emoji.Error} The source and target users cannot be the same!";
                        break;
                    case TransferResult.FailedNoSourceData:
                        response += $"{Program.cfgjson.Emoji.Error} The source user has no notes to transfer.";
                        break;
                    case TransferResult.FailedConflict:
                        response += $"{Program.cfgjson.Emoji.Warning} **CAUTION**: The target user has notes.\n\n" +
                                   $"If you are sure you want to **OVERRIDE** and **DELETE** these notes, please consider the consequences before adding `force_override: True` to the command.\nIf you wish to **NOT** override the target's notes, please use `merge: True` instead.";
                        break;
                }
                
                return response.Trim();
            }
        }
    }
}