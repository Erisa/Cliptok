using static Cliptok.Helpers.BanHelpers;

namespace Cliptok.Commands
{
    class Bans : BaseCommandModule
    {
        [Command("massban")]
        [Aliases("bigbonk")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task MassBanCmd(CommandContext ctx, [RemainingText] string input)
        {

            List<string> usersString = input.Replace("\n", " ").Replace("\r", "").Split(' ').ToList();
            List<ulong> users = usersString.Select(x => Convert.ToUInt64(x)).ToList();
            if (users.Count == 1 || users.Count == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Not accepting a massban with a single user. Please use `!ban`.");
                return;
            }

            List<Task<bool>> taskList = new();
            int successes = 0;

            var loading = await ctx.RespondAsync("Processing, please wait.");

            foreach (ulong user in users)
            {
                taskList.Add(BanSilently(ctx.Guild, user));
            }

            var tasks = await Task.WhenAll(taskList);

            foreach (var task in taskList)
            {
                if (task.Result)
                    successes += 1;
            }

            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Banned} **{successes}**/{users.Count} users were banned successfully.");
            await loading.DeleteAsync();
        }

        [Command("ban")]
        [Aliases("tempban", "bonk")]
        [Description("Bans a user that you have permission to ban, deleting all their messages in the process. See also: bankeep.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(Permissions.BanMembers)]
        public async Task BanCmd(CommandContext ctx,
         [Description("The user you wish to ban. Accepts many formats")] DiscordUser targetMember,
         [RemainingText, Description("The time and reason for the ban. e.g. '14d trolling' NOTE: Add 'appeal' to the start of the reason to include an appeal link")] string timeAndReason = "No reason specified.")
        {
            bool appealable = false;
            bool timeParsed = false;

            TimeSpan banDuration = default;
            string possibleTime = timeAndReason.Split(' ').First();
            try
            {
                banDuration = HumanDateParser.HumanDateParser.Parse(possibleTime).Subtract(ctx.Message.Timestamp.DateTime);
                timeParsed = true;
            }
            catch
            {
                // keep default
            }

            string reason = timeAndReason;

            if (timeParsed)
            {
                int i = reason.IndexOf(" ") + 1;
                reason = reason[i..];
            }

            if (timeParsed && possibleTime == reason)
                reason = "No reason specified.";

            if (reason.Length > 6 && reason[..7].ToLower() == "appeal ")
            {
                appealable = true;
                reason = reason[7..^0];
            }

            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(targetMember.Id);
            }
            catch
            {
                member = null;
            }

            if (member is null)
            {
                await ctx.Message.DeleteAsync();
                await BanFromServerAsync(targetMember.Id, reason, ctx.User.Id, ctx.Guild, 7, ctx.Channel, banDuration, appealable);
            }
            else
            {
                if (DiscordHelpers.AllowedToMod(ctx.Member, member))
                {
                    if (DiscordHelpers.AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
                    {
                        await ctx.Message.DeleteAsync();
                        await BanFromServerAsync(targetMember.Id, reason, ctx.User.Id, ctx.Guild, 7, ctx.Channel, banDuration, appealable);
                    }
                    else
                    {
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to ban **{targetMember.Username}#{targetMember.Discriminator}**!");
                        return;
                    }
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You don't have permission to ban **{targetMember.Username}#{targetMember.Discriminator}**!");
                    return;
                }
            }
            reason = reason.Replace("`", "\\`").Replace("*", "\\*");
            if (banDuration == default)
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {targetMember.Mention} has been banned: **{reason}**");
            else
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {targetMember.Mention} has been banned for **{TimeHelpers.TimeToPrettyFormat(banDuration, false)}**: **{reason}**");
        }

        /// I CANNOT find a way to do this as alias so I made it a separate copy of the command.
        /// Sue me, I beg you.
        [Command("bankeep")]
        [Aliases("bansave")]
        [Description("Bans a user but keeps their messages around."), HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(Permissions.BanMembers)]
        public async Task BankeepCmd(CommandContext ctx,
        [Description("The user you wish to ban. Accepts many formats")] DiscordUser targetMember,
        [RemainingText, Description("The time and reason for the ban. e.g. '14d trolling' NOTE: Add 'appeal' to the start of the reason to include an appeal link")] string timeAndReason = "No reason specified.")
        {
            bool appealable = false;
            bool timeParsed = false;

            TimeSpan banDuration = default;
            string possibleTime = timeAndReason.Split(' ').First();
            try
            {
                banDuration = HumanDateParser.HumanDateParser.Parse(possibleTime).Subtract(ctx.Message.Timestamp.DateTime);
                timeParsed = true;
            }
            catch
            {
                // keep default
            }

            string reason = timeAndReason;

            if (timeParsed)
            {
                int i = reason.IndexOf(" ") + 1;
                reason = reason[i..];
            }

            if (timeParsed && possibleTime == reason)
                reason = "No reason specified.";

            if (reason.Length > 6 && reason[..7].ToLower() == "appeal ")
            {
                appealable = true;
                reason = reason[7..^0];
            }

            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(targetMember.Id);
            }
            catch
            {
                member = null;
            }

            if (member is null)
            {
                await ctx.Message.DeleteAsync();
                await BanFromServerAsync(targetMember.Id, reason, ctx.User.Id, ctx.Guild, 0, ctx.Channel, banDuration, appealable);
            }
            else
            {
                if (DiscordHelpers.AllowedToMod(ctx.Member, member))
                {
                    if (DiscordHelpers.AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
                    {
                        await ctx.Message.DeleteAsync();
                        await BanFromServerAsync(targetMember.Id, reason, ctx.User.Id, ctx.Guild, 0, ctx.Channel, banDuration, appealable);
                    }
                    else
                    {
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to ban **{targetMember.Username}#{targetMember.Discriminator}**!");
                        return;
                    }
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You don't have permission to ban **{targetMember.Username}#{targetMember.Discriminator}**!");
                    return;
                }
            }
            reason = reason.Replace("`", "\\`").Replace("*", "\\*");
            if (banDuration == default)
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {targetMember.Mention} has been banned: **{reason}**");
            else
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {targetMember.Mention} has been banned for **{TimeHelpers.TimeToPrettyFormat(banDuration, false)}**: **{reason}**");
        }

        [Command("unban")]
        [Description("Unbans a user who has been previously banned.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(Permissions.BanMembers)]
        public async Task UnbanCmd(CommandContext ctx, [Description("The user to unban, usually a mention or ID")] DiscordUser targetUser, [Description("Used in audit log only currently")] string reason = "No reason specified.")
        {
            if ((await Program.db.HashExistsAsync("bans", targetUser.Id)))
            {
                await UnbanUserAsync(ctx.Guild, targetUser, $"[Unban by {ctx.User.Username}#{ctx.User.Discriminator}]: {reason}");
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Unbanned} Successfully unbanned **{targetUser.Username}#{targetUser.Discriminator}**.");
            }
            else
            {
                bool banSuccess = await UnbanUserAsync(ctx.Guild, targetUser);
                if (banSuccess)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Unbanned} Successfully unbanned **{targetUser.Username}#{targetUser.Discriminator}**.");
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.Member.Mention}, that user doesn't appear to be banned, *and* an error occurred while attempting to unban them anyway.\nPlease contact the bot owner if this wasn't expected, the error has been logged.");
                }
            }
        }

    }
}
