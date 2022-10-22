using static Cliptok.Helpers.WarningHelpers;

namespace Cliptok.Commands
{

    public class Warnings : BaseCommandModule
    {
        [
            Command("warn"),
            Description("Issues a formal warning to a user."),
            Aliases("wam", "warm"),
            HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task WarnCmd(
            CommandContext ctx,
            [Description("The user you are warning. Accepts many formats.")] DiscordUser targetUser,
            [RemainingText, Description("The reason for giving this warning.")] string reason = null
        )
        {
            DiscordMember targetMember;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
                if (GetPermLevel(ctx.Member) == ServerPermLevel.TrialModerator && (GetPermLevel(targetMember) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
                {
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
                    return;
                }
            }
            catch
            {
                // do nothing :/
            }

            var reply = ctx.Message.ReferencedMessage;

            await ctx.Message.DeleteAsync();
            if (reason is null)
            {
                await ctx.Member.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} Reason must be included for the warning command to work.");
                return;
            }

            var messageBuild = new DiscordMessageBuilder()
                .WithContent($"{Program.cfgjson.Emoji.Warning} <@{targetUser.Id}> was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");

            if (reply != null)
                messageBuild.WithReply(reply.Id, true, false);

            var msg = await ctx.Channel.SendMessageAsync(messageBuild);
            _ = await GiveWarningAsync(targetUser, ctx.User, reason, msg, ctx.Channel);
        }

        [
            Command("anonwarn"),
            Description("Issues a formal warning to a user from a private channel."),
            Aliases("anonwam", "anonwarm"),
            HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task AnonWarnCmd(
            CommandContext ctx,
            [Description("The channel you wish for the warning message to appear in.")] DiscordChannel targetChannel,
            [Description("The user you are warning. Accepts many formats.")] DiscordUser targetUser,
            [RemainingText, Description("The reason for giving this warning.")] string reason = null
        )
        {
            DiscordMember targetMember;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
                if (GetPermLevel(ctx.Member) == ServerPermLevel.TrialModerator && (GetPermLevel(targetMember) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
                {
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
                    return;
                }
            }
            catch
            {
                // do nothing :/
            }

            await ctx.Message.DeleteAsync();
            if (reason is null)
            {
                await ctx.Member.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} Reason must be included for the warning command to work.");
                return;
            }
            DiscordMessage msg = await targetChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} {targetUser.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
            await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} {targetUser.Mention} was warned in {targetChannel.Mention}: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
            _ = await GiveWarningAsync(targetUser, ctx.User, reason, msg, ctx.Channel);
        }

        [
            Command("warnings"),
            Description("Shows a list of warnings that a user has been given. For more in-depth information, use the 'warnlookup' command."),
            Aliases("infractions", "warnfractions", "wammings", "wamfractions"),
            HomeServer
        ]
        public async Task WarningCmd(
            CommandContext ctx,
            [Description("The user you want to look up warnings for. Accepts many formats.")] DiscordUser targetUser = null
        )
        {
            if (targetUser is null)
                targetUser = ctx.User;

            await ctx.RespondAsync(null, await GenerateWarningsEmbedAsync(targetUser));
        }

        [
            Command("delwarn"),
            Description("Delete a warning that was issued by mistake or later became invalid."),
            Aliases("delwarm", "delwam", "deletewarn"),
            HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task DelwarnCmd(
            CommandContext ctx,
            [Description("The user you're removing a warning from. Accepts many formats.")] DiscordUser targetUser,
            [Description("The ID of the warning you want to delete.")] long warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);
            if (warning is null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else if (GetPermLevel(ctx.Member) == ServerPermLevel.TrialModerator && warning.ModUserId != ctx.User.Id && warning.ModUserId != ctx.Client.CurrentUser.Id)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot edit or delete warnings that aren't issued by you or the bot!");
            }
            else
            {
                bool success = await DelWarningAsync(warning, targetUser.Id);
                if (success)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Deleted} Successfully deleted warning `{StringHelpers.Pad(warnId)}` (belonging to {targetUser.Mention})");

                    await LogChannelHelper.LogMessageAsync("mod",
                        new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Deleted} Warning deleted:" +
                            $"`{StringHelpers.Pad(warnId)}` (belonging to {targetUser.Mention}, deleted by {ctx.Member.Mention})")
                            .WithEmbed(await FancyWarnEmbedAsync(warning, true, 0xf03916, true, targetUser.Id))
                            .WithAllowedMentions(Mentions.None)
                        );
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to delete warning `{StringHelpers.Pad(warnId)}` from {targetUser.Mention}!\nPlease contact the bot author.");
                }
            }
        }

        [
            Command("warnlookup"),
            Description("Looks up information about a warning. Shows only publicly available information."),
            Aliases("warning", "warming", "waming", "wamming", "lookup", "lookylooky", "peek", "investigate", "what-did-i-do-wrong-there", "incident"),
            HomeServer
        ]
        public async Task WarnlookupCmd(
            CommandContext ctx,
            [Description("The user you're looking at a warning for. Accepts many formats.")] DiscordUser targetUser,
            [Description("The ID of the warning you want to see")] long warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);
            if (warning is null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else
                await ctx.RespondAsync(null, await FancyWarnEmbedAsync(warning, userID: targetUser.Id));
        }

        [
            Command("warndetails"),
            Aliases("warninfo", "waminfo", "wamdetails", "warndetail", "wamdetail"),
            Description("Check the details of a warning in depth. Shows extra information (Such as responsible Mod) that may not be wanted to be public."),
            HomeServer,
            RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task WarnDetailsCmd(
            CommandContext ctx,
            [Description("The user you're looking up detailed warn information for. Accepts many formats.")] DiscordUser targetUser,
            [Description("The ID of the warning you're looking at in detail.")] long warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);

            if (warning is null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else
                await ctx.RespondAsync(null, await FancyWarnEmbedAsync(warning, true, userID: targetUser.Id));

        }

        [
            Command("editwarn"),
            Aliases("warnedit"),
            Description("Edit the reason of an existing warning.\n" +
                "The Moderator who is editing the reason will become responsible for the case."),
            HomeServer,
            RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task EditwarnCmd(
            CommandContext ctx,
            [Description("The user you're editing a warning for. Accepts many formats.")] DiscordUser targetUser,
            [Description("The ID of the warning you want to edit.")] long warnId,
            [RemainingText, Description("The new reason for the warning.")] string newReason)
        {
            if (string.IsNullOrWhiteSpace(newReason))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You haven't given a new reason to set for the warning!");
                return;
            }

            var msg = await ctx.RespondAsync("Processing your request...");
            var warning = GetWarning(targetUser.Id, warnId);
            if (warning is null)
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else if (GetPermLevel(ctx.Member) == ServerPermLevel.TrialModerator && warning.ModUserId != ctx.User.Id && warning.ModUserId != ctx.Client.CurrentUser.Id)
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot edit or delete warnings that aren't issued by you or the bot!");
            }
            else
            {
                await EditWarning(targetUser, warnId, ctx.User, newReason);
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Information} Successfully edited warning `{StringHelpers.Pad(warnId)}` (belonging to {targetUser.Mention})",
                    await FancyWarnEmbedAsync(GetWarning(targetUser.Id, warnId), userID: targetUser.Id));

                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Information} Warning edited:" +
                        $"`{StringHelpers.Pad(warnId)}` (belonging to {targetUser.Mention})")
                        .WithEmbed(await FancyWarnEmbedAsync(GetWarning(targetUser.Id, warnId), true, userID: targetUser.Id))
                    );
            }
        }

        [Command("mostwarnings"), Description("Who has the most warnings???")]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task MostWarningsCmd(CommandContext ctx)
        {
            await DiscordHelpers.SafeTyping(ctx.Channel);

            var server = Program.redis.GetServer(Program.redis.GetEndPoints()[0]);
            var keys = server.Keys();

            Dictionary<string, int> counts = new();
            foreach (var key in keys)
            {
                if (ulong.TryParse(key.ToString(), out ulong number))
                {
                    counts[key.ToString()] = Program.db.HashGetAll(key).Length;
                }
            }

            List<KeyValuePair<string, int>> myList = counts.ToList();
            myList.Sort(
                delegate (KeyValuePair<string, int> pair1,
                KeyValuePair<string, int> pair2)
                {
                    return pair1.Value.CompareTo(pair2.Value);
                }
            );

            var user = await ctx.Client.GetUserAsync(Convert.ToUInt64(myList.Last().Key));
            await ctx.RespondAsync($":thinking: The user with the most warnings is **{user.Username}#{user.Discriminator}** with a total of **{myList.Last().Value} warnings!**\nThis includes users who have left or been banned.");
        }

        [Command("mostwarningsday"), Description("Which day has the most warnings???")]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task MostWarningsDayCmd(CommandContext ctx)
        {
            await DiscordHelpers.SafeTyping(ctx.Channel);

            var server = Program.redis.GetServer(Program.redis.GetEndPoints()[0]);
            var keys = server.Keys();

            Dictionary<string, int> counts = new();
            Dictionary<string, int> noAutoCounts = new();

            foreach (var key in keys)
            {
                if (ulong.TryParse(key.ToString(), out ulong number))
                {
                    var warningsOutput = Program.db.HashGetAll(key.ToString()).ToDictionary(
                        x => x.Name.ToString(),
                        x => JsonConvert.DeserializeObject<UserWarning>(x.Value)
                    );

                    foreach (var warning in warningsOutput)
                    {
                        var day = warning.Value.WarnTimestamp.ToString("yyyy-MM-dd");
                        if (!counts.ContainsKey(day))
                        {
                            counts[day] = 1;
                        }
                        else
                        {
                            counts[day] += 1;
                        }
                        if (warning.Value.ModUserId != 159985870458322944 && warning.Value.ModUserId != Program.discord.CurrentUser.Id)
                        {
                            if (!noAutoCounts.ContainsKey(day))
                            {
                                noAutoCounts[day] = 1;
                            }
                            else
                            {
                                noAutoCounts[day] += 1;
                            }
                        }
                    }
                }
            }

            List<KeyValuePair<string, int>> countList = counts.ToList();
            countList.Sort(
                delegate (KeyValuePair<string, int> pair1,
                KeyValuePair<string, int> pair2)
                {
                    return pair1.Value.CompareTo(pair2.Value);
                }
            );

            List<KeyValuePair<string, int>> noAutoCountList = noAutoCounts.ToList();
            noAutoCountList.Sort(
                delegate (KeyValuePair<string, int> pair1,
                KeyValuePair<string, int> pair2)
                {
                    return pair1.Value.CompareTo(pair2.Value);
                }
            );

            await ctx.RespondAsync($":thinking: As far as I can tell, the day with the most warnings issued was **{countList.Last().Key}** with a total of **{countList.Last().Value} warnings!**" +
                $"\nExcluding automatic warnings, the most was on **{noAutoCountList.Last().Key}** with a total of **{noAutoCountList.Last().Value}** warnings!");
        }
    }
}
