namespace Cliptok.Commands
{
    public class KickCmds
    {
        [Command("kick")]
        [TextAlias("yeet", "shoo", "goaway", "defenestrate")]
        [Description("Kicks a user, removing them from the server until they rejoin.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(DiscordPermission.KickMembers)]
        public async Task KickCmd(CommandContext ctx, [Parameter("user"), Description("The user you want to kick from the server.")] DiscordUser target, [Parameter("reason"), Description("The reason for kicking this user."), RemainingText] string reason = "No reason specified.")
        {
            if (target.IsBot)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} To prevent accidents, I won't kick bots. If you really need to do this, do it manually in Discord.");
                return;
            }
            
            if (ctx is TextCommandContext)
                await ctx.As<TextCommandContext>().Message.DeleteAsync();

            reason = reason.Replace("`", "\\`").Replace("*", "\\*");

            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(target.Id);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be in the server!");
                return;
            }

            if (DiscordHelpers.AllowedToMod(ctx.Member, member))
            {
                if (DiscordHelpers.AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
                {
                    await KickAndLogAsync(member, reason, ctx.Member);
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Ejected} {target.Mention} has been kicked: **{reason}**");
                    if (ctx is SlashCommandContext)
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Done!", ephemeral: true);
                    return;
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to kick **{DiscordHelpers.UniqueUsername(target)}**!", ephemeral: true);
                    return;
                }
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You aren't allowed to kick **{DiscordHelpers.UniqueUsername(target)}**!", ephemeral: true);
                return;
            }
        }

        [Command("masskicktextcmd")]
        [TextAlias("masskick")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task MassKickCmd(TextCommandContext ctx, [RemainingText] string input)
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
            
            if (users.Count == 1 || users.Count == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Not accepting a masskick with a single user. Please use `!ban`.");
                return;
            }

            List<Task<bool>> taskList = new();
            int successes = 0;

            await ctx.RespondAsync("Processing, please wait.");
            var loading = await ctx.GetResponseAsync();

            foreach (ulong user in users)
            {
                try
                {
                    var member = await ctx.Guild.GetMemberAsync(user);
                    if (member is not null)
                    {

                        taskList.Add(SafeKickAndLogAsync(member, $"Mass kick{(string.IsNullOrWhiteSpace(reason) ? "" : $": {reason}")}", ctx.Member));
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
            await loading.DeleteAsync();
        }


        public async static Task KickAndLogAsync(DiscordMember target, string reason, DiscordMember moderator)
        {
            await target.RemoveAsync(reason);
            await LogChannelHelper.LogMessageAsync("mod",
                new DiscordMessageBuilder()
                    .WithContent($"{Program.cfgjson.Emoji.Ejected} {target.Mention} was kicked by {moderator.Mention}.\nReason: **{reason}**")
                    .WithAllowedMentions(Mentions.None)
           );
        }

        public async static Task<bool> SafeKickAndLogAsync(DiscordMember target, string reason, DiscordMember moderator)
        {
            try
            {
                await target.RemoveAsync(reason);
                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Ejected} {target.Mention} was kicked by {moderator.Mention}.\nReason: **{reason}**")
                        .WithAllowedMentions(Mentions.None)
               );
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}