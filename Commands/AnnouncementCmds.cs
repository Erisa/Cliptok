namespace Cliptok.Commands
{
    public class AnnouncementCmds
    {
        // used to pass context to modal handling for /editannounce
        // keyed by user ID
        public static Dictionary<ulong, (ulong msgId, ulong role1, ulong role2)> EditAnnounceCache = new();

        [Command("announcebuild")]
        [Description("Announce a Windows Insider build in the current channel.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task AnnounceBuildSlashCommand(SlashCommandContext ctx,
            [Parameter("build_number"), Description("Windows 11 build number, including decimals (Decimals are optional). Do not include the word Build.")] string buildNumber,

            [Parameter("blog_link"), Description("The link to the Windows blog entry relating to this build.")] string blogLink,

            [SlashAutoCompleteProvider(typeof(Providers.RolesAutocompleteProvider))]
            [Parameter("insider_role1"), Description("The first insider role to ping.")] string insiderChannel1,

            [SlashAutoCompleteProvider(typeof(Providers.RolesAutocompleteProvider))]
            [Parameter("insider_role2"), Description("The second insider role to ping.")] string insiderChannel2 = default,

            [Parameter("create_new_thread"), Description("Enable this option if you want to create a new thread for some reason")] bool createNewThread = false,
            [Parameter("thread1"), Description("The thread to mention in the announcement.")] DiscordChannel threadChannel = default,
            [Parameter("thread2"), Description("The second thread to mention in the announcement.")] DiscordChannel threadChannel2 = default,
            [Parameter("flavour_text"), Description("Extra text appended on the end of the main line, replacing :WindowsInsider:")] string flavourText = "",
            [Parameter("autothread_name"), Description("If no thread is given, create a thread with this name.")] string autothreadName = "Build {0} ({1})",

            [Parameter("lockdown"), Description("Set 0 to not lock. Lock the channel for a certain period of time after announcing the build.")] string lockdownTime = "auto",
            [Parameter("force_reannounce"), Description("Whether to ignore the check for duplicate announcements and send this one anyway.")] bool forceReannounce = false)
        {
            if (Program.cfgjson.InsidersChannel != 0 && ctx.Channel.Id != Program.cfgjson.InsidersChannel)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command only works in <#{Program.cfgjson.InsidersChannel}>!", ephemeral: true);
                return;
            }

            if (insiderChannel1 == insiderChannel2)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Both insider channels cannot be the same! Simply set one instead.", ephemeral: true);
                return;
            }

            if (threadChannel != default && threadChannel == threadChannel2)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Both threads cannot be the same! Simply set one instead.", ephemeral: true);
                return;
            }

            if (Program.cfgjson.InsiderRoles is null)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Insider roles are not set up in config.json! Unable to announce builds.", ephemeral: true);
                return;
            }

            if (threadChannel == default && threadChannel2 != default)
            {
                threadChannel = threadChannel2;
                threadChannel2 = default;
            }

            // Avoid duplicate announcements
            if (await Program.redis.SetContainsAsync("announcedInsiderBuilds", buildNumber) && !forceReannounce)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Build {buildNumber} has already been announced! If you are sure you want to announce it again, set `force_reannounce` to True.", ephemeral: true);
                return;
            }

            await Program.redis.SetAddAsync("announcedInsiderBuilds", buildNumber);

            if (flavourText == "")
            {
                flavourText = Program.cfgjson.Emoji.Insider;
            }

            // defer since we're going to do lots of rest calls now
            await ctx.DeferResponseAsync(ephemeral: false);

            DiscordRole insiderRole1;
            DiscordRole insiderRole2 = default;
            try
            {
                insiderRole1 = await ctx.Guild.GetRoleAsync(Convert.ToUInt64(insiderChannel1));
            }
            catch (Exception ex) when (ex is FormatException or DSharpPlus.Exceptions.NotFoundException)
            {
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} You entered an invalid role! Please choose from the list.");
                return;
            }

            StringBuilder channelString = new();

            string insiderChannel1Pretty = GetInsiderChannelNameFromRole(insiderRole1);

            if (string.IsNullOrWhiteSpace(insiderChannel1Pretty))
            {
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} The Insider roles in this server do not match the expected format! Unable to announce builds.");
                return;
            }

            channelString.Append("the ");

            channelString.Append($"**{insiderChannel1Pretty}");

            if (insiderChannel2 != default)
            {
                try
                {
                    insiderRole2 = await ctx.Guild.GetRoleAsync(Convert.ToUInt64(insiderChannel2));
                }
                catch (Exception ex) when (ex is FormatException or DSharpPlus.Exceptions.NotFoundException)
                {
                    await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} You entered an invalid role! Please choose from the list.");
                    return;
                }
                string insiderChannel2Pretty = GetInsiderChannelNameFromRole(insiderRole2);

                if (string.IsNullOrWhiteSpace(insiderChannel2Pretty))
                {
                    await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} The Insider roles in this server do not match the expected format! Unable to announce builds.");
                    return;
                }

                channelString.Append($" **and **{insiderChannel2Pretty}** Channels");
            }
            else
            {
                channelString.Append("** Channel");
            }

            string pingMsgBareString = $"{insiderRole1.Mention}{(insiderChannel2 != default ? $" {insiderRole2.Mention}\n" : " - ")}Hi Insiders!\n\n" +
                $"Windows 11 Build **{buildNumber}** has just been released to {channelString}! {flavourText}\n\n" +
                $"Check it out here: {blogLink}";

            string innerThreadMsgString = $"Hi Insiders!\n\n" +
                $"Windows 11 Build **{buildNumber}** has just been released to {channelString}! {flavourText}\n\n" +
                $"Check it out here: {blogLink}";

            string noPingMsgString = $"{Program.cfgjson.Emoji.Windows11} Windows 11 Build **{buildNumber}** has just been released to {channelString}! {flavourText}\n\n" +
                $"Check it out here: <{blogLink}>";

            string pingMsgString = pingMsgBareString;

            DiscordMessage messageSent;
            if (Program.cfgjson.InsiderAnnouncementChannel == 0)
            {
                if (threadChannel != default)
                {
                    pingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                    if (threadChannel2 != default)
                        pingMsgString += $" & {threadChannel2.Mention}";
                }
                else if (!createNewThread)
                {
                    var insidersChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsidersChannel);
                    threadChannel = insidersChannel.Threads.FirstOrDefault(t => t.Name.Contains(GetInsiderChannelNameFromRole(insiderRole1), StringComparison.OrdinalIgnoreCase));
                    threadChannel2 = insiderRole2 == default
                        ? default
                        : insidersChannel.Threads.FirstOrDefault(t => t.Name.Contains(GetInsiderChannelNameFromRole(insiderRole2), StringComparison.OrdinalIgnoreCase));

                    if (threadChannel == default)
                    {
                        await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Couldn't find an Insider thread for the {insiderRole1.Mention} channel! Please set it manually or check the thread names.");
                        return;
                    }
                    if (threadChannel2 == default)
                    {
                        await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Couldn't find an Insider thread for the {insiderRole2.Mention} channel! Please set it manually or check the thread names.");
                        return;
                    }

                    pingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                    if (threadChannel2 != default)
                        pingMsgString += $" & {threadChannel2.Mention}";
                    var msg = await threadChannel.SendMessageAsync(innerThreadMsgString);
                    await DiscordHelpers.UpdateInsiderThreadPinsAsync(threadChannel, msg);
                }
                else
                {
                    pingMsgString += "\n\nDiscuss it in the thread below:";
                }

                await insiderRole1.ModifyAsync(mentionable: true);
                if (insiderChannel2 != default)
                    await insiderRole2.ModifyAsync(mentionable: true);

                await ctx.RespondAsync(pingMsgString);
                messageSent = await ctx.GetResponseAsync();

                await insiderRole1.ModifyAsync(mentionable: false);
                if (insiderChannel2 != default)
                    await insiderRole2.ModifyAsync(mentionable: false);
            }
            else
            {
                if (threadChannel != default)
                {
                    noPingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                    if (threadChannel2 == default && insiderChannel2 != default)
                    {
                        await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Couldn't find an Insider thread for the {insiderRole2.Mention} channel! Please set it manually or check the thread names.");
                        return;
                    }
                    else if (threadChannel2 != default)
                    {
                        noPingMsgString += $" & {threadChannel2.Mention}";
                    }
                }
                else if (!createNewThread)
                {
                    var insidersChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsidersChannel);
                    threadChannel = insidersChannel.Threads.FirstOrDefault(t => t.Name.Contains(GetInsiderChannelNameFromRole(insiderRole1), StringComparison.OrdinalIgnoreCase));
                    threadChannel2 =  insiderRole2 == default
                        ? default
                        : insidersChannel.Threads.FirstOrDefault(t => t.Name.Contains(GetInsiderChannelNameFromRole(insiderRole2), StringComparison.OrdinalIgnoreCase));

                    if (threadChannel == default)
                    {
                        await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Couldn't find an Insider thread for the {insiderRole1.Mention} channel! Please set it manually or check the thread names.");
                        return;
                    }

                    noPingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                    if (threadChannel2 == default && insiderChannel2 != default)
                    {
                        await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Couldn't find an Insider thread for the {insiderRole2.Mention} channel! Please set it manually or check the thread names.");
                        return;
                    }
                    else if (threadChannel2 != default)
                    {
                        noPingMsgString += $" & {threadChannel2.Mention}";
                    }
                    var msg = await threadChannel.SendMessageAsync(innerThreadMsgString);
                    await DiscordHelpers.UpdateInsiderThreadPinsAsync(threadChannel, msg);
                    DiscordMessage msg2 = default;
                    if (threadChannel2 != default)
                    {
                        msg2 = await threadChannel2.SendMessageAsync(innerThreadMsgString);
                        await DiscordHelpers.UpdateInsiderThreadPinsAsync(threadChannel2, msg2);
                    }
                }
                else
                {
                    noPingMsgString += "\n\nDiscuss it in the thread below:";
                }

                await ctx.RespondAsync(noPingMsgString);
                messageSent = await ctx.GetResponseAsync();
            }

            if (threadChannel == default)
            {
                string threadBrackets = GetInsiderChannelNameFromRole(insiderRole1);
                if (insiderChannel2 != default)
                    threadBrackets = $"{GetInsiderChannelNameFromRole(insiderRole1)} & {GetInsiderChannelNameFromRole(insiderRole2)}";

                string threadName = string.Format(autothreadName, buildNumber, threadBrackets);
                threadChannel = await messageSent.CreateThreadAsync(threadName, DiscordAutoArchiveDuration.Week, "Creating thread for Insider build.");

                var initialMsg = await threadChannel.SendMessageAsync($"{blogLink}");
                await initialMsg.PinAsync();
            }

            if (Program.cfgjson.InsiderAnnouncementChannel != 0)
            {
                pingMsgString = pingMsgBareString + $"\n\nDiscuss it here: {threadChannel.Mention}";
                if (threadChannel2 != default)
                    pingMsgString += $" & {threadChannel2.Mention}";

                var announcementChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderAnnouncementChannel);
                await insiderRole1.ModifyAsync(mentionable: true);
                if (insiderChannel2 != default)
                    await insiderRole2.ModifyAsync(mentionable: true);

                var msg = await announcementChannel.SendMessageAsync(pingMsgString);

                await insiderRole1.ModifyAsync(mentionable: false);
                if (insiderChannel2 != default)
                    await insiderRole2.ModifyAsync(mentionable: false);

                if (announcementChannel.Type is DiscordChannelType.News)
                    await announcementChannel.CrosspostMessageAsync(msg);
            }

            if (lockdownTime == "auto")
            {
                if (Program.cfgjson.InsiderAnnouncementChannel == 0)
                    lockdownTime = "1h";
                else
                    lockdownTime = "0";
            }

            if (lockdownTime != "0")
            {
                TimeSpan lockDuration;
                try
                {
                    lockDuration = HumanDateParser.HumanDateParser.Parse(lockdownTime).ToUniversalTime().Subtract(DateTime.UtcNow);
                }
                catch
                {
                    lockDuration = TimeSpan.FromHours(2);
                }

                await LockdownHelpers.LockChannelAsync(user: ctx.User, channel: ctx.Channel, duration: lockDuration);
            }
        }

        [Command("editannounce")]
        [Description("Edit an announcement, preserving the ping highlight.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task EditAnnounce(
            SlashCommandContext ctx,
            [Parameter("message"), Description("The ID of the message to edit.")] string messageId,
            [SlashAutoCompleteProvider(typeof(Providers.RolesAutocompleteProvider))]
            [Parameter("role1"), Description("The first role to ping.")] ulong role1Id,
            [SlashAutoCompleteProvider(typeof(Providers.RolesAutocompleteProvider))]
            [Parameter("role2"), Description("The second role to ping. Optional.")] ulong role2Id = default
        )
        {
            // Validate msg ID
            DiscordMessage msg;
            try
            {
                msg = await ctx.Channel.GetMessageAsync(Convert.ToUInt64(messageId));
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That message ID wasn't recognised!", ephemeral: true);
                return;
            }

            if (msg.Author.Id != ctx.Client.CurrentUser.Id)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That message wasn't sent by me, so I can't edit it!", ephemeral: true);
                return;
            }

            // Validate roles
            if (Program.cfgjson.InsiderRoles is null || !Program.cfgjson.InsiderRoles.Contains(role1Id) || (role2Id != default && !Program.cfgjson.InsiderRoles.Contains(role2Id)))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} The role name(s) you entered aren't recognised!", ephemeral: true);
                return;
            }
            if (role1Id == role2Id)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} You provided the same role name twice! Did you mean to use two different roles?", ephemeral: true);
                return;
            }

            EditAnnounceCache[ctx.User.Id] = (Convert.ToUInt64(messageId), role1Id, role2Id);

            await ctx.RespondWithModalAsync(new DiscordModalBuilder().WithTitle("Edit Announcement").WithCustomId("editannounce-modal-callback").AddTextInput(new DiscordTextInputComponent("editannounce-modal-new-text", style: DiscordTextInputStyle.Paragraph, value: msg.Content), "New announcement text. Do not include roles!"));
        }

        [Command("announce")]
        [Description("Announces something in the current channel, pinging an Insider role in the process.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task AnounceSlashCmd(SlashCommandContext ctx,
            [SlashAutoCompleteProvider(typeof(Providers.RolesAutocompleteProvider))]
            [Parameter("role1"), Description("The first Insider role to ping.")] string role1,
            [Parameter("announcement_message"), Description("The message to announce.")] string announcementMessage,
            [SlashAutoCompleteProvider(typeof(Providers.RolesAutocompleteProvider))]
            [Parameter("role2"), Description("The second Insider role to ping.")] string role2 = default)
        {
            await ctx.DeferResponseAsync(ephemeral: true);

            ulong insiderChannel1;
            ulong insiderChannel2 = default;
            try
            {
                insiderChannel1 = Convert.ToUInt64(role1);
                if (role2 != default)
                    insiderChannel2 = Convert.ToUInt64(role2);
            }
            catch (FormatException)
            {
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Invalid role! Please choose from the list.", ephemeral: true);
                return;
            }

            if (Program.cfgjson.InsiderRoles is null || !Program.cfgjson.InsiderRoles.Contains(insiderChannel1) ||
                (insiderChannel2 != default && !Program.cfgjson.InsiderRoles.Contains(insiderChannel2)))
            {
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Invalid role! Please choose from the list.", ephemeral: true);
                return;
            }

            if (insiderChannel1 == insiderChannel2)
            {
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} Both insider channels cannot be the same! Simply set one instead.", ephemeral: true);
                return;
            }

            announcementMessage = announcementMessage.Replace("\\n", "\n");

            DiscordRole insiderRole1 = await ctx.Guild.GetRoleAsync(insiderChannel1);
            DiscordRole insiderRole2 = insiderChannel2 == default ? default : await ctx.Guild.GetRoleAsync(insiderChannel2);

            await insiderRole1.ModifyAsync(mentionable: true);
            if (insiderRole2 != default)
                await insiderRole2.ModifyAsync(mentionable: true);

            try
            {
                var msg = insiderRole1.Mention;
                if (insiderRole2 != default)
                    msg += $" {insiderRole2.Mention}";
                msg += $" {announcementMessage}";
                await ctx.Channel.SendMessageAsync(msg);
            }
            catch
            {
                // We still need to remember to make it unmentionable even if the msg fails.
            }

            await insiderRole1.ModifyAsync(mentionable: false);
            if (insiderRole2 != default)
                await insiderRole2.ModifyAsync(mentionable: false);

            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Announcement sent successfully!");
        }

        private static string GetInsiderChannelNameFromRole(DiscordRole insiderRole)
        {
            return Regex.Match(insiderRole.Name, @"\((.+)\)").Groups[1].Value;
        }
    }
}