namespace Cliptok.Commands
{
    internal class Kick : BaseCommandModule
    {
        [Command("kick")]
        [Aliases("yeet", "shoo", "goaway", "defenestrate")]
        [Description("Kicks a user, removing them from the server until they rejoin. Generally not very useful.")]
        [RequirePermissions(Permissions.KickMembers), HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task KickCmd(CommandContext ctx, DiscordUser target, [RemainingText] string reason = "No reason specified.")
        {
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
                    await ctx.Message.DeleteAsync();
                    await KickAndLogAsync(member, reason, ctx.Member);
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Ejected} {target.Mention} has been kicked: **{reason}**");
                    return;
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to kick **{DiscordHelpers.UniqueUsername(target)}**!");
                    return;
                }
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You aren't allowed to kick **{DiscordHelpers.UniqueUsername(target)}**!");
                return;
            }
        }

        [Command("masskick")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task MassKickCmd(CommandContext ctx, [RemainingText] string input)
        {

            List<string> usersString = input.Replace("\n", " ").Replace("\r", "").Split(' ').ToList();
            List<ulong> users = usersString.Select(x => Convert.ToUInt64(x)).ToList();
            if (users.Count == 1 || users.Count == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Not accepting a masskick with a single user. Please use `!ban`.");
                return;
            }

            List<Task> taskList = new();
            int successes = 0;

            var loading = await ctx.RespondAsync("Processing, please wait.");

            foreach (ulong user in users)
            {
                var member = await ctx.Guild.GetMemberAsync(user);
                if (member is not null)
                {
                    successes += 1;
                    taskList.Add(KickAndLogAsync(member, "Mass kick", ctx.Member));
                }
            }

            await Task.WhenAll(taskList);

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

    }
}
