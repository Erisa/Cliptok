using static Cliptok.Helpers.BanHelpers;

namespace Cliptok.Commands
{
    public class BanCmds
    {
        [Command("ban")]
        [Description("Bans a user from the server, either permanently or temporarily.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(DiscordPermission.BanMembers)]
        public async Task BanSlashCommand(SlashCommandContext ctx,
            [Parameter("user"), Description("The user to ban")] DiscordUser user,
            [Parameter("reason"), Description("The reason the user is being banned")] string reason,
            [Parameter("keep_messages"), Description("Whether to keep the users messages when banning")] bool keepMessages = false,
            [Parameter("time"), Description("The length of time the user is banned for")] string time = null,
            [Parameter("appeal_link"), Description("Whether to show the user an appeal URL in the DM")] bool appealable = false,
            [Parameter("compromised_account"), Description("Whether to include special instructions for compromised accounts")] bool compromisedAccount = false
        )
        {
            // collision detection
            if (MostRecentBan is not null && MostRecentBan.MemberId == user.Id)
            {
                var timeSinceLastBan = DateTime.UtcNow.Subtract((DateTime)MostRecentBan.ActionTime);
                if (timeSinceLastBan <= TimeSpan.FromSeconds(5))
                {
                    var response = new DiscordInteractionResponseBuilder()
        .WithContent($"{Program.cfgjson.Emoji.Error} {user.Mention} was already banned a few seconds ago, refusing yours to prevent collisions. If you meant to ban them again, try again in a few seconds.")
        .AsEphemeral(true);
                    if (!MostRecentBan.Stub)
                        response.AddEmbed(await BanStatusEmbed(user, ctx.Guild));

                    await ctx.RespondAsync(response);
                    return;
                }
            }

            MostRecentBan = new()
            {
                MemberId = user.Id,
                ActionTime = ctx.Interaction.CreationTimestamp.DateTime,
                ModId = ctx.User.Id,
                ServerId = ctx.Guild.Id,
                Reason = reason,
                Stub = true
            };

            // Initial response to avoid the 3 second timeout, will edit later.
            var eout = new DiscordInteractionResponseBuilder().AsEphemeral(true);
            await ctx.DeferResponseAsync(true);

            // Edits need a webhook rather than interaction..?
            DiscordWebhookBuilder webhookOut = new();
            int messageDeleteDays = 7;
            if (keepMessages)
                messageDeleteDays = 0;

            if (user.IsBot)
            {
                webhookOut.Content = $"{Program.cfgjson.Emoji.Error} To prevent accidents, I won't ban bots. If you really need to do this, do it manually in Discord.";
                await ctx.EditResponseAsync(webhookOut);
                return;
            }

            DiscordMember targetMember;

            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(user.Id);
                if ((await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TrialModerator && ((await GetPermLevelAsync(targetMember)) >= ServerPermLevel.TrialModerator))
                {
                    webhookOut.Content = $"{Program.cfgjson.Emoji.Error} As a Trial Moderator you cannot perform moderation actions on other staff members.";
                    await ctx.EditResponseAsync(webhookOut);
                    return;
                }
            }
            catch
            {
                // do nothing :/
            }

            TimeSpan banDuration;
            if (time is null)
                banDuration = default;
            else
            {
                try
                {
                    banDuration = HumanDateParser.HumanDateParser.Parse(time).ToUniversalTime().Subtract(ctx.Interaction.CreationTimestamp.DateTime);
                }
                catch
                {
                    webhookOut.Content = $"{Program.cfgjson.Emoji.Error} There was an error parsing your supplied ban length!";
                    await ctx.EditResponseAsync(webhookOut);
                    return;
                }

            }

            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(user.Id);
            }
            catch
            {
                member = null;
            }

            if (member is null)
            {
                await BanHelpers.BanFromServerAsync(user.Id, reason, ctx.User.Id, ctx.Guild, messageDeleteDays, ctx.Channel, banDuration, appealable, compromisedAccount);
            }
            else
            {
                if (DiscordHelpers.AllowedToMod(ctx.Member, member))
                {
                    if (DiscordHelpers.AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
                    {
                        await BanHelpers.BanFromServerAsync(user.Id, reason, ctx.User.Id, ctx.Guild, messageDeleteDays, ctx.Channel, banDuration, appealable, compromisedAccount);
                    }
                    else
                    {
                        webhookOut.Content = $"{Program.cfgjson.Emoji.Error} I don't have permission to ban **{DiscordHelpers.UniqueUsername(user)}**!";
                        await ctx.EditResponseAsync(webhookOut);
                        return;
                    }
                }
                else
                {
                    webhookOut.Content = $"{Program.cfgjson.Emoji.Error} You don't have permission to ban **{DiscordHelpers.UniqueUsername(user)}**!";
                    await ctx.EditResponseAsync(webhookOut);
                    return;
                }
            }

            webhookOut.Content = $"{Program.cfgjson.Emoji.Success} User was successfully bonked.";
            await ctx.EditResponseAsync(webhookOut);
        }

        [Command("unban")]
        [Description("Unbans a user who has been previously banned.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(permissions: DiscordPermission.BanMembers)]
        public async Task UnbanCmd(CommandContext ctx, [Description("The user to unban, usually a mention or ID")] DiscordUser targetUser, [RemainingText, Description("Used in audit log only currently")] string reason = "No reason specified.")
        {
            var unbanMsg = $"{Program.cfgjson.Emoji.Unbanned} Successfully unbanned **{DiscordHelpers.UniqueUsername(targetUser)}**";
            if (reason != "No reason specified.")
                unbanMsg += $": **{reason}**";

            if (await Program.redis.HashExistsAsync("bans", targetUser.Id))
            {
                await UnbanUserAsync(ctx.Guild, targetUser, $"[Unban by {DiscordHelpers.UniqueUsername(ctx.User)}]: {reason}");
                await ctx.RespondAsync(unbanMsg);
            }
            else
            {
                bool banSuccess = await UnbanUserAsync(ctx.Guild, targetUser);
                if (banSuccess)
                    await ctx.RespondAsync(unbanMsg);
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.Member.Mention}, that user doesn't appear to be banned, *and* an error occurred while attempting to unban them anyway.\nPlease contact the bot owner if this wasn't expected, the error has been logged.");
                }
            }
        }

        [Command("baninfo")]
        [Description("Show information about the ban for a user.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task BanInfoSlashCommand(
            SlashCommandContext ctx,
            [Parameter("user"), Description("The user whose ban information to show.")] DiscordUser targetUser,
            [Parameter("public"), Description("Whether to show the output publicly. Default: false")] bool isPublic = false)
        {
            await ctx.RespondAsync(embed: await BanHelpers.BanStatusEmbed(targetUser, ctx.Guild), ephemeral: !isPublic);
        }

        [Command("massbantextcmd")]
        [TextAlias("massban", "bigbonk")]
        [Description("Ban multiple users from the server at once.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task MassBanCmd(TextCommandContext ctx, [Description("The list of users to ban, separated by newlines or spaces, optionally followed by a reason."), RemainingText] string input)
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
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Not accepting a massban with a single user. Please use `!ban`.");
                return;
            }

            List<Task<bool>> taskList = new();
            int successes = 0;

            await ctx.RespondAsync("Processing, please wait.");
            var loading = await ctx.GetResponseAsync();

            foreach (ulong user in users)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    taskList.Add(BanSilently(ctx.Guild, user));
                else
                    taskList.Add(BanSilently(ctx.Guild, user, $"Mass ban: {reason}"));
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

        [Command("bantextcmd")]
        [TextAlias("ban", "tempban", "bonk", "isekaitruck")]
        [Description("Bans a user that you have permission to ban, deleting all their messages in the process. See also: bankeep.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(permissions: DiscordPermission.BanMembers)]
        public async Task BanCmd(TextCommandContext ctx,
         [Description("The user you wish to ban. Should be a mention or ID.")] DiscordUser targetMember,
         [RemainingText, Description("The time and reason for the ban. e.g. '14d trolling' NOTE: Add 'appeal' to the start of the reason to include an appeal link")] string timeAndReason = "No reason specified.")
        {
            // collision detection
            if (MostRecentBan is not null && targetMember.Id == MostRecentBan.MemberId)
            {
                var timeSinceLastWarning = DateTime.UtcNow.Subtract((DateTime)MostRecentBan.ActionTime);
                if (timeSinceLastWarning <= TimeSpan.FromSeconds(5))
                {
                    await ctx.Message.DeleteAsync();
                    var resp = await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.BSOD} I was asked to ban someone twice within a few seconds, but I'm not going to. If I'm wrong, try again in a few seconds.");
                    await Task.Delay(5000);
                    await resp.DeleteAsync();
                    return;
                }
            }

            MostRecentBan = new()
            {
                MemberId = targetMember.Id,
                ActionTime = DateTime.UtcNow,
                ModId = ctx.User.Id,
                ServerId = ctx.Guild.Id,
                Reason = timeAndReason,
                Stub = true
            };

            if (targetMember.IsBot)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} To prevent accidents, I won't ban bots. If you really need to do this, do it manually in Discord.");
                return;
            }

            bool appealable = false;
            bool timeParsed = false;

            TimeSpan banDuration = default;
            string possibleTime = timeAndReason.Split(' ').First();
            try
            {
                banDuration = HumanDateParser.HumanDateParser.Parse(possibleTime).ToUniversalTime().Subtract(ctx.Message.Timestamp.DateTime);
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
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to ban **{DiscordHelpers.UniqueUsername(targetMember)}**!");
                        return;
                    }
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You don't have permission to ban **{DiscordHelpers.UniqueUsername(targetMember)}**!");
                    return;
                }
            }
        }

        /// I CANNOT find a way to do this as alias so I made it a separate copy of the command.
        /// Sue me, I beg you.
        [Command("bankeeptextcmd")]
        [TextAlias("bankeep", "bansave")]
        [Description("Bans a user but keeps their messages around."), HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(permissions: DiscordPermission.BanMembers)]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        public async Task BankeepCmd(TextCommandContext ctx,
        [Description("The user you wish to ban. Should be a mention or ID.")] DiscordUser targetMember,
        [RemainingText, Description("The time and reason for the ban. e.g. '14d trolling' NOTE: Add 'appeal' to the start of the reason to include an appeal link")] string timeAndReason = "No reason specified.")
        {
            // collision detection
            if (MostRecentBan is not null && targetMember.Id == MostRecentBan.MemberId)
            {
                var timeSinceLastWarning = DateTime.UtcNow.Subtract((DateTime)MostRecentBan.ActionTime);
                if (timeSinceLastWarning <= TimeSpan.FromSeconds(5))
                {
                    await ctx.Message.DeleteAsync();
                    var resp = await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.BSOD} I was asked to ban someone twice within a few seconds, but I'm not going to. If I'm wrong, try again in a few seconds.");
                    await Task.Delay(5000);
                    await resp.DeleteAsync();
                    return;
                }
            }


            MostRecentBan = new()
            {
                MemberId = targetMember.Id,
                ActionTime = DateTime.UtcNow,
                ModId = ctx.User.Id,
                ServerId = ctx.Guild.Id,
                Reason = timeAndReason,
                Stub = true
            };

            bool appealable = false;
            bool timeParsed = false;

            TimeSpan banDuration = default;
            string possibleTime = timeAndReason.Split(' ').First();
            try
            {
                banDuration = HumanDateParser.HumanDateParser.Parse(possibleTime).ToUniversalTime().Subtract(ctx.Message.Timestamp.DateTime);
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
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to ban **{DiscordHelpers.UniqueUsername(targetMember)}**!");
                        return;
                    }
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You don't have permission to ban **{DiscordHelpers.UniqueUsername(targetMember)}**!");
                    return;
                }
            }
        }

        [Command("editbantextcmd")]
        [TextAlias("editban")]
        [Description("Edit the details of a ban. Updates the DM to the user, among other things.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        public async Task EditBanCmd(TextCommandContext ctx,
            [Description("The user you wish to edit the ban of. Accepts many formats")] DiscordUser targetUser,
            [RemainingText, Description("The time and reason for the ban. e.g. '14d trolling' NOTE: Add 'appeal' to the start of the reason to include an appeal link")] string timeAndReason = "No reason specified."
        )
        {
            if (!await Program.redis.HashExistsAsync("bans", targetUser.Id))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} There's no record of a ban for that user! Please make sure they're banned or you got the right user.");
                return;
            }

            (TimeSpan banDuration, string reason, bool appealable) = PunishmentHelpers.UnpackTimeAndReason(timeAndReason, ctx.Message.Timestamp.DateTime);

            var ban = JsonConvert.DeserializeObject<MemberPunishment>(await Program.redis.HashGetAsync("bans", targetUser.Id));

            ban.ModId = ctx.User.Id;
            if (banDuration == default)
                ban.ExpireTime = null;
            else
                ban.ExpireTime = ban.ActionTime + banDuration;
            ban.Reason = reason;
            
            // If ban is for a compromised account, add to list so the context message can be more-easily deleted later
            // and pardon any automatic warnings issued within the last 12 hours
            if (ban.Reason.ToLower().Contains("compromised"))
            {
                Program.redis.HashSet("compromisedAccountBans", targetUser.Id, JsonConvert.SerializeObject(ban));
                
                var warnings = (await Program.redis.HashGetAllAsync(targetUser.Id.ToString())).Select(x => JsonConvert.DeserializeObject<UserWarning>(x.Value)).ToList();
                foreach (var warning in warnings)
                {
                    if (warning.Type == WarningType.Warning
                        && (warning.ModUserId == Program.discord.CurrentUser.Id || (await Program.discord.GetUserAsync(warning.ModUserId)).IsBot)
                        && (DateTime.Now - warning.WarnTimestamp).TotalHours < Program.cfgjson.CompromisedAccountBanAutoPardonHours)
                    {
                        warning.IsPardoned = true;
                        await Program.redis.HashSetAsync(warning.TargetUserId.ToString(), warning.WarningId, JsonConvert.SerializeObject(warning));
                    }
                }
            }
            
            var guild = await Program.discord.GetGuildAsync(ban.ServerId);

            var contextMessage = await DiscordHelpers.GetMessageFromReferenceAsync(ban.ContextMessageReference);
            var dmMessage = await DiscordHelpers.GetMessageFromReferenceAsync(ban.DmMessageReference);

            reason = reason.Replace("`", "\\`").Replace("*", "\\*");

            if (contextMessage is not null)
            {
                string newCtxMsg;
                if (banDuration == default)
                    newCtxMsg = $"{Program.cfgjson.Emoji.Banned} {targetUser.Mention} has been banned: **{reason}**";
                else
                    newCtxMsg = $"{Program.cfgjson.Emoji.Banned} {targetUser.Mention} has been banned for **{TimeHelpers.TimeToPrettyFormat(banDuration, false)}**: **{reason}**";

                if (contextMessage.Content.Contains("-# This user's messages have been kept."))
                    newCtxMsg += "\n-# This user's messages have been kept.";

                await contextMessage.ModifyAsync(newCtxMsg);
            }

            if (dmMessage is not null)
            {
                if (ban.ExpireTime == null)
                {
                    if (appealable)
                    {
                        if (reason.ToLower().Contains("compromised"))
                            await dmMessage.ModifyAsync($"{Program.cfgjson.Emoji.Banned} You have been banned from **{guild.Name}**!\nReason: **{reason}**\nYou can appeal the ban here: <{Program.cfgjson.AppealLink}>\nBefore appealing, please follow these steps to protect your account:\n1. Reset your Discord account password. Even if you use MFA, this will reset all session tokens.\n2. Review active sessions and authorised app connections.\n3. Ensure your PC is free of malware.\n4. [Enable MFA](https://support.discord.com/hc/en-us/articles/219576828-Setting-up-Multi-Factor-Authentication) if not already.");
                        else
                            await dmMessage.ModifyAsync($"{Program.cfgjson.Emoji.Banned} You have been banned from **{guild.Name}**!\nReason: **{reason}**\nYou can appeal the ban here: <{Program.cfgjson.AppealLink}>");
                    }
                    else
                    {
                        await dmMessage.ModifyAsync($"{Program.cfgjson.Emoji.Banned} You have been permanently banned from **{guild.Name}**!\nReason: **{reason}**");
                    }
                }
                else
                {
                    await dmMessage.ModifyAsync($"{Program.cfgjson.Emoji.Banned} You have been banned from **{guild.Name}** for {TimeHelpers.TimeToPrettyFormat(banDuration, false)}!\nReason: **{reason}**\nBan expires: <t:{TimeHelpers.ToUnixTimestamp(ban.ExpireTime)}:R>");
                }
            }

            await Program.redis.HashSetAsync("bans", targetUser.Id.ToString(), JsonConvert.SerializeObject(ban));

            // Construct log message
            string logOut = $"{Program.cfgjson.Emoji.MessageEdit} The ban for {targetUser.Mention} was edited by {ctx.User.Mention}!\nReason: **{reason}**";

            if (ban.ExpireTime == null)
            {
                logOut += "\nBan expires: **Never**"
                    + $"\nAppealable: **{(appealable ? "Yes" : "No")}**";
            }
            else
            {
                logOut += $"\nBan expires: <t:{TimeHelpers.ToUnixTimestamp(ban.ExpireTime)}:R>";
            }

            // Log to mod log
            await LogChannelHelper.LogMessageAsync("mod", logOut);

            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully edited the ban for {targetUser.Mention}!");
        }
    }
}