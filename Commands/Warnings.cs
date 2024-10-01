using static Cliptok.Helpers.WarningHelpers;

namespace Cliptok.Commands
{

    public class Warnings
    {
        [
            Command("6158e255-e8b3-4467-8d1a-79f89829"),
            Description("Issues a formal warning to a user."),
            TextAlias("warn", "wam", "warm"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task WarnCmd(
            TextCommandContext ctx,
            [Description("The user you are warning. Accepts many formats.")] DiscordUser targetUser,
            [RemainingText, Description("The reason for giving this warning.")] string reason = null
        )
        {
            DiscordMember targetMember;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
                if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && ((await GetPermLevelAsync(targetMember)) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
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

            if (reply is not null)
                messageBuild.WithReply(reply.Id, true, false);

            var msg = await ctx.Channel.SendMessageAsync(messageBuild);
            _ = await GiveWarningAsync(targetUser, ctx.User, reason, msg, ctx.Channel);
        }

        [
            Command("anonwarn"),
            Description("Issues a formal warning to a user from a private channel."),
            TextAlias("anonwam", "anonwarm"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task AnonWarnCmd(
            TextCommandContext ctx,
            [Description("The channel you wish for the warning message to appear in.")] DiscordChannel targetChannel,
            [Description("The user you are warning. Accepts many formats.")] DiscordUser targetUser,
            [RemainingText, Description("The reason for giving this warning.")] string reason = null
        )
        {
            DiscordMember targetMember;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
                if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && ((await GetPermLevelAsync(targetMember)) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
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
            Command("6158e255-e8b3-4467-8d1a-79f89810"),
            Description("Shows a list of warnings that a user has been given. For more in-depth information, use the 'warnlookup' command."),
            TextAlias("warnings", "infractions", "warnfractions", "wammings", "wamfractions"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task WarningCmd(
            TextCommandContext ctx,
            [Description("The user you want to look up warnings for. Accepts many formats.")] DiscordUser targetUser = null
        )
        {
            if (targetUser is null)
                targetUser = ctx.User;

            await ctx.RespondAsync(null, await GenerateWarningsEmbedAsync(targetUser));
        }

        [
            Command("6158e255-e8b3-4467-8d1a-79f89811"),
            Description("Delete a warning that was issued by mistake or later became invalid."),
            TextAlias("delwarn", "delwarm", "delwam", "deletewarn", "delwarning", "deletewarning"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task DelwarnCmd(
            TextCommandContext ctx,
            [Description("The user you're removing a warning from. Accepts many formats.")] DiscordUser targetUser,
            [Description("The ID of the warning you want to delete.")] long warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);
            if (warning is null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else if (warning.Type == WarningType.Note)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That's a note, not a warning! Try using `/note delete` instead, or make sure you've got the right warning ID.");
            }
            else if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && warning.ModUserId != ctx.User.Id && warning.ModUserId != ctx.Client.CurrentUser.Id)
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
                            .AddEmbed(await FancyWarnEmbedAsync(warning, true, 0xf03916, true, targetUser.Id))
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
            TextAlias("warning", "warming", "waming", "wamming", "lookup", "lookylooky", "peek", "investigate", "what-did-i-do-wrong-there", "incident"),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer
        ]
        public async Task WarnlookupCmd(
            TextCommandContext ctx,
            [Description("The user you're looking at a warning for. Accepts many formats.")] DiscordUser targetUser,
            [Description("The ID of the warning you want to see")] long warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);
            if (warning is null || warning.Type == WarningType.Note)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else
                await ctx.RespondAsync(null, await FancyWarnEmbedAsync(warning, userID: targetUser.Id));
        }

        [
            Command("6158e255-e8b3-4467-8d1a-79f89822"),
            TextAlias("warndetails", "warninfo", "waminfo", "wamdetails", "warndetail", "wamdetail"),
            Description("Check the details of a warning in depth. Shows extra information (Such as responsible Mod) that may not be wanted to be public."),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task WarnDetailsCmd(
            TextCommandContext ctx,
            [Description("The user you're looking up detailed warn information for. Accepts many formats.")] DiscordUser targetUser,
            [Description("The ID of the warning you're looking at in detail.")] long warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);

            if (warning is null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else if (warning.Type == WarningType.Note)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That's a note, not a warning! Try using `/note details` instead, or make sure you've got the right warning ID.");
            }
            else
                await ctx.RespondAsync(null, await FancyWarnEmbedAsync(warning, true, userID: targetUser.Id));

        }

        [
            Command("6158e255-e8b3-4467-8d1a-79f89812"),
            TextAlias("editwarn", "warnedit", "editwarning"),
            Description("Edit the reason of an existing warning.\n" +
                "The Moderator who is editing the reason will become responsible for the case."),
            AllowedProcessors(typeof(TextCommandProcessor)),
            HomeServer,
            RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task EditwarnCmd(
            TextCommandContext ctx,
            [Description("The user you're editing a warning for. Accepts many formats.")] DiscordUser targetUser,
            [Description("The ID of the warning you want to edit.")] long warnId,
            [RemainingText, Description("The new reason for the warning.")] string newReason)
        {
            if (string.IsNullOrWhiteSpace(newReason))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You haven't given a new reason to set for the warning!");
                return;
            }

            await ctx.RespondAsync("Processing your request...");
            var msg = await ctx.GetResponseAsync();
            var warning = GetWarning(targetUser.Id, warnId);
            if (warning is null)
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else if (warning.Type == WarningType.Note)
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} That's a note, not a warning! Try using `/note edit` instead, or make sure you've got the right warning ID.");
            }
            else if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && warning.ModUserId != ctx.User.Id && warning.ModUserId != ctx.Client.CurrentUser.Id)
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
                        .AddEmbed(await FancyWarnEmbedAsync(GetWarning(targetUser.Id, warnId), true, userID: targetUser.Id))
                    );
            }
        }

        [Command("mostwarnings"), Description("Who has the most warnings???")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task MostWarningsCmd(TextCommandContext ctx)
        {
            await DiscordHelpers.SafeTyping(ctx.Channel);

            var server = Program.redis.GetServer(Program.redis.GetEndPoints()[0]);
            var keys = server.Keys();

            Dictionary<string, int> counts = new();
            foreach (var key in keys)
            {
                if (ulong.TryParse(key.ToString(), out ulong number))
                {
                    counts[key.ToString()] = Program.db.HashGetAll(key).Count(x => JsonConvert.DeserializeObject<UserWarning>(x.Value.ToString()).Type == WarningType.Warning);
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
            await ctx.RespondAsync($":thinking: The user with the most warnings is **{DiscordHelpers.UniqueUsername(user)}** with a total of **{myList.Last().Value} warnings!**\nThis includes users who have left or been banned.");
        }

        [Command("mostwarningsday"), Description("Which day has the most warnings???")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task MostWarningsDayCmd(TextCommandContext ctx)
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
                        if (warning.Value.Type != WarningType.Warning) continue;

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
