namespace Cliptok.Commands
{
    [Command("mass")]
    [Description("Commands for performing mass actions.")]
    [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ModerateMembers)]
    [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
    public static class MassCmds
    {
        [Command("ban")]
        [TextAlias("bigbonk")]
        [Description("Ban multiple users from the server at once.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        public static async Task MassBanCmd(CommandContext ctx, [Parameter("input"), Description("The list of users to ban, separated by spaces, optionally followed by a reason."), RemainingText] string input)
        {
            if (ctx is SlashCommandContext)
                await ctx.DeferResponseAsync();
            
            var (users, reason) = ParseInput(input);

            if (users.Count == 1 || users.Count == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Not accepting a massban with a single user. Please use `!ban`.");
                return;
            }

            List<Task<bool>> taskList = new();
            int successes = 0;

            DiscordMessage loading = default;
            if (ctx is TextCommandContext)
            {
                await ctx.RespondAsync("Processing, please wait.");
                loading = await ctx.GetResponseAsync();
            }

            foreach (ulong user in users)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    taskList.Add(BanHelpers.BanSilently(ctx.Guild, user, ctx.User.Id));
                else
                    taskList.Add(BanHelpers.BanSilently(ctx.Guild, user, ctx.User.Id, $"Mass ban: {reason}"));
            }

            var tasks = await Task.WhenAll(taskList);

            foreach (var task in taskList)
            {
                if (task.Result)
                    successes += 1;
            }

            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Banned} **{successes}**/{users.Count} users were banned successfully.");
            if (ctx is TextCommandContext)
                await loading.DeleteAsync();
        }
        
        [Command("kick")]
        [Description("Kick multiple users from the server at once.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        public static async Task MassKickCmd(CommandContext ctx, [Parameter("input"), Description("The list of users to kick, separated by spaces, optionally followed by a reason."), RemainingText] string input)
        {
            if (ctx is SlashCommandContext)
                await ctx.DeferResponseAsync();

            var (users, reason) = ParseInput(input);

            if (users.Count == 1 || users.Count == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Not accepting a masskick with a single user. Please use `!kick`.");
                return;
            }

            List<Task<bool>> taskList = new();
            int successes = 0;

            DiscordMessage loading = default;
            if (ctx is TextCommandContext)
            {
                await ctx.RespondAsync("Processing, please wait.");
                loading = await ctx.GetResponseAsync();
            }

            foreach (ulong user in users)
            {
                try
                {
                    var member = await ctx.Guild.GetMemberAsync(user);
                    if (member is not null)
                    {

                        taskList.Add(KickHelpers.SafeKickAndLogAsync(member, $"Mass kick{(string.IsNullOrWhiteSpace(reason) ? "" : $": {reason}")}", ctx.Member));
                    }
                }
                catch
                {
                    // not successful, move on
                }
            }

            var tasks = await Task.WhenAll(taskList);

            foreach (var task in taskList)
            {
                if (task.Result)
                    successes += 1;
            }

            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Deleted} **{successes}**/{users.Count} users were kicked successfully.");
            if (ctx is TextCommandContext)
                await loading.DeleteAsync();
        }
        
        [Command("mute")]
        [Description("Mute multiple users at once.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        public static async Task MassMuteCmd(CommandContext ctx, [Parameter("input"), Description("The list of users to mute, separated by spaces, optionally followed by a reason."), RemainingText] string input)
        {
            if (ctx is SlashCommandContext)
                await ctx.DeferResponseAsync();
            
            var (users, reason) = ParseInput(input);

            if (users.Count == 1 || users.Count == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Not accepting a massmute with a single user. Please use `!mute`.");
                return;
            }

            List<Task<bool>> taskList = new();
            int successes = 0;

            DiscordMessage loading = default;
            if (ctx is TextCommandContext)
            {
                await ctx.RespondAsync("Processing, please wait.");
                loading = await ctx.GetResponseAsync();
            }

            foreach (ulong user in users)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    taskList.Add(MuteHelpers.MuteSilently(ctx.Guild, user, ctx.User.Id));
                else
                    taskList.Add(MuteHelpers.MuteSilently(ctx.Guild, user, ctx.User.Id, $"Mass mute: {reason}"));
            }

            var tasks = await Task.WhenAll(taskList);

            foreach (var task in taskList)
            {
                if (task.Result)
                    successes += 1;
            }

            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Muted} **{successes}**/{users.Count} users were muted successfully.");
            if (ctx is TextCommandContext)
                await loading.DeleteAsync();
        }
        
        [Command("unmute")]
        [Description("Unmute multiple users at once.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        public static async Task MassUnmuteCmd(CommandContext ctx, [Parameter("input"), Description("The list of users to unmute, separated by spaces, optionally followed by a reason."), RemainingText] string input)
        {
            if (ctx is SlashCommandContext)
                await ctx.DeferResponseAsync();
            
            var (users, reason) = ParseInput(input);

            if (users.Count == 1 || users.Count == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Not accepting a massunmute with a single user. Please use `!unmute`.");
                return;
            }

            List<Task<bool>> taskList = new();
            int successes = 0;

            DiscordMessage loading = default;
            if (ctx is TextCommandContext)
            {
                await ctx.RespondAsync("Processing, please wait.");
                loading = await ctx.GetResponseAsync();
            }

            foreach (ulong user in users)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    taskList.Add(MuteHelpers.UnmuteSilently(ctx.Guild, user));
                else
                    taskList.Add(MuteHelpers.UnmuteSilently(ctx.Guild, user, $"Mass unmute: {reason}"));
            }

            var tasks = await Task.WhenAll(taskList);

            foreach (var task in taskList)
            {
                if (task.Result)
                    successes += 1;
            }

            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} **{successes}**/{users.Count} users were unmuted successfully.");
            if (ctx is TextCommandContext)
                await loading.DeleteAsync();
        }
        
        [Command("dehoist")]
        [Description("Dehoist everyone on the server with a bad name. This may take a while and can exhaust rate limits.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public static async Task MassDehoist(CommandContext ctx)
        {
            DiscordMessage msg = default;
            if (ctx is SlashCommandContext)
                await ctx.DeferResponseAsync();
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it. This will take a while.");
                msg = await ctx.GetResponseAsync();
            }

            var (totalMembers, failedMembers) = await DehoistHelpers.MassDehoistAsync(ctx.Guild, ctx.User);

            if (ctx is TextCommandContext)
                _ = msg.DeleteAsync();
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully dehoisted {totalMembers - failedMembers} of {totalMembers} member(s)! (Check Audit Log for details)");
        }

        [Command("undehoist")]
        [Description("Remove the dehoist for users attached via a txt file.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public static async Task MassUndehoist(CommandContext ctx, [Parameter("file"), Description("A text file containing the list of users to undehoist.")] DiscordAttachment file = null)
        {
            DiscordAttachment attachment;
            if (ctx is TextCommandContext tctx && tctx.Message.Attachments.Count > 0)
                attachment = tctx.Message.Attachments[0];
            else
                attachment = file;
            
            int failedCount = 0;

            if (attachment is null)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Please upload an attachment as well.");
            }
            else
            {
                string strList;
                using (HttpClient client = new())
                {
                    strList = await client.GetStringAsync(attachment.Url);
                }

                var list = strList.Split(' ');

                DiscordMessage msg = default;
                if (ctx is SlashCommandContext)
                    await ctx.DeferResponseAsync();
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it. This will take a while.");
                    msg = await ctx.GetResponseAsync();
                }

                foreach (string strID in list)
                {
                    ulong id = Convert.ToUInt64(strID);
                    DiscordMember member = default;
                    try
                    {
                        member = await ctx.Guild.GetMemberAsync(id);
                    }
                    catch (DSharpPlus.Exceptions.NotFoundException)
                    {
                        failedCount++;
                        continue;
                    }

                    if (member.DisplayName[0] == DehoistHelpers.dehoistCharacter && !member.MemberFlags.Value.HasFlag(DiscordMemberFlags.AutomodQuarantinedUsername))
                    {
                        var newNickname = member.Nickname[1..];
                        await member.ModifyAsync(a =>
                        {
                            a.Nickname = newNickname;
                            a.AuditLogReason = $"[Mass undehoist by {DiscordHelpers.UniqueUsername(ctx.User)}]";
                        }
                        );
                    }
                    else
                    {
                        failedCount++;
                    }
                }

                if (ctx is TextCommandContext)
                    await msg.DeleteAsync();
                
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully undehoisted {list.Length - failedCount} of {list.Length} member(s)! (Check Audit Log for details)");

            }
        }
        
        private static (List<ulong> users, string reason) ParseInput(string input)
        {
            List<string> inputString = input.Replace("\n", " ").Replace("\r", "").Split(' ').ToList();
            List<ulong> users = new();
            string reason = "";
            foreach (var word in inputString)
            {
                if (ulong.TryParse(word, out var id))
                    users.Add(id);
                else
                    reason += $"{word} ";
            }
            reason = reason.Trim();
            
            return (users, reason);
        }
    }
    
    // An attempt at aliases...
    public static class MassTextAliases
    {
        [Command("massban")]
        [Description("Ban multiple users from the server at once.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [Hidden]
        public static async Task MassBanAliasCmd(TextCommandContext ctx, [Description("The list of users to ban, separated by newlines or spaces, optionally followed by a reason."), RemainingText] string input)
        {
            await MassCmds.MassBanCmd(ctx, input);
        }
        
        [Command("masskick")]
        [Description("Kick multiple users from the server at once.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [Hidden]
        public static async Task MassKickAliasCmd(TextCommandContext ctx, [Description("The list of users to kick, separated by newlines or spaces, optionally followed by a reason."), RemainingText] string input)
        {
            await MassCmds.MassKickCmd(ctx, input);
        }
        
        [Command("massmute")]
        [Description("Mute multiple users at once.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [Hidden]
        public static async Task MassMuteAliasCmd(CommandContext ctx, [Description("The list of users to mute, separated by newlines or spaces, optionally followed by a reason."), RemainingText] string input)
        {
            await MassCmds.MassMuteCmd(ctx, input);
        }
        
        [Command("massunmute")]
        [Description("Unmute multiple users at once.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [Hidden]
        public static async Task MassUnmuteAliasCmd(CommandContext ctx, [Description("The list of users to mute, separated by newlines or spaces, optionally followed by a reason."), RemainingText] string input)
        {
            await MassCmds.MassUnmuteCmd(ctx, input);
        }
        
        [Command("massdehoist")]
        [Description("Dehoist everyone on the server with a bad name. This may take a while and can exhaust rate limits.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [Hidden]
        public static async Task MassDehoistAliasCmd(TextCommandContext ctx)
        {
            await MassCmds.MassDehoist(ctx);
        }
        
        [Command("massundehoist")]
        [Description("Remove the dehoist for users attached via a txt file.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [Hidden]
        public static async Task MassUndehoistAliasCmd(TextCommandContext ctx)
        {
            await MassCmds.MassUndehoist(ctx);
        }
    }
}