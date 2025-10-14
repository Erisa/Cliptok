namespace Cliptok.Commands
{
    public class AnnouncementCmds
    {
        // used to pass context to modal handling for /editannounce
        // keyed by user ID
        public static Dictionary<ulong, (ulong msgId, string role1, string role2)> EditAnnounceCache = new();

        [Command("announcebuild")]
        [Description("Announce a Windows Insider build in the current channel.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task AnnounceBuildSlashCommand(SlashCommandContext ctx,
            [Parameter("build_number"), Description("Windows 11 build number, including decimals (Decimals are optional). Do not include the word Build.")] string buildNumber,

            [Parameter("blog_link"), Description("The link to the Windows blog entry relating to this build.")] string blogLink,

            [SlashChoiceProvider(typeof(WindowsInsiderChannelChoiceProvider))]
            [Parameter("insider_role1"), Description("The first insider role to ping.")] string insiderChannel1,

            [SlashChoiceProvider(typeof(WindowsInsiderChannelChoiceProvider))]
            [Parameter("insider_role2"), Description("The second insider role to ping.")] string insiderChannel2 = "",

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

            string roleKey1= insiderChannel1.ToLower();

            // defer since we're going to do lots of rest calls now
            await ctx.DeferResponseAsync(ephemeral: false);

            DiscordRole insiderRole1 = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles[roleKey1]);
            DiscordRole insiderRole2 = default;

            StringBuilder channelString = new();

            string insiderChannel1Pretty = insiderChannel1 == "RP" ? "Release Preview" : insiderChannel1;

            channelString.Append("the ");

            channelString.Append($"**{insiderChannel1Pretty}");

            if (insiderChannel2 != "")
            {
                string insiderChannel2Pretty = insiderChannel2 == "RP" ? "Release Preview" : insiderChannel2;
                channelString.Append($" **and **{insiderChannel2Pretty}** Channels");
            }
            else
            {
                channelString.Append("** Channel");
            }

            if (insiderChannel2 != "")
            {
                string roleKey2 = insiderChannel2.ToLower();

                insiderRole2 = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles[roleKey2]);
            }

            string pingMsgBareString = $"{insiderRole1.Mention}{(insiderChannel2 != "" ? $" {insiderRole2.Mention}\n" : " - ")}Hi Insiders!\n\n" +
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
                    switch (insiderChannel1)
                    {
                        case "Canary":
                            threadChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["canary"]);
                            break;
                        case "Dev":
                            threadChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["dev"]);
                            break;
                        case "Beta":
                            threadChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["beta"]);
                            break;
                        case "RP":
                            threadChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["rp"]);
                            break;
                    }

                    switch (insiderChannel2)
                    {
                        case "Canary":
                            threadChannel2 = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["canary"]);
                            break;
                        case "Dev":
                            threadChannel2 = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["dev"]);
                            break;
                        case "Beta":
                            threadChannel2 = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["beta"]);
                            break;
                        case "RP":
                            threadChannel2 = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["rp"]);
                            break;
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
                if (insiderChannel2 != "")
                    await insiderRole2.ModifyAsync(mentionable: true);

                await ctx.RespondAsync(pingMsgString);
                messageSent = await ctx.GetResponseAsync();

                await insiderRole1.ModifyAsync(mentionable: false);
                if (insiderChannel2 != "")
                    await insiderRole2.ModifyAsync(mentionable: false);
            }
            else
            {
                if (threadChannel != default)
                {
                    noPingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                    if (threadChannel2 != default)
                        noPingMsgString += $" & {threadChannel2.Mention}";
                }
                else if (!createNewThread)
                {
                    switch (insiderChannel1)
                    {
                        case "Canary":
                            threadChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["canary"]);
                            break;
                        case "Dev":
                            threadChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["dev"]);
                            break;
                        case "Beta":
                            threadChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["beta"]);
                            break;
                        case "RP":
                            threadChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["rp"]);
                            break;
                    }

                    switch (insiderChannel2)
                    {
                        case "Canary":
                            threadChannel2 = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["canary"]);
                            break;
                        case "Dev":
                            threadChannel2 = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["dev"]);
                            break;
                        case "Beta":
                            threadChannel2 = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["beta"]);
                            break;
                        case "RP":
                            threadChannel2 = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderThreads["rp"]);
                            break;
                    }

                    noPingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                    if (threadChannel2 != default)
                        noPingMsgString += $" & {threadChannel2.Mention}";
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
                string threadBrackets = insiderChannel1;
                if (insiderChannel2 != "")
                    threadBrackets = $"{insiderChannel1} & {insiderChannel2}";

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
                if (insiderChannel2 != "")
                    await insiderRole2.ModifyAsync(mentionable: true);

                var msg = await announcementChannel.SendMessageAsync(pingMsgString);

                await insiderRole1.ModifyAsync(mentionable: false);
                if (insiderChannel2 != "")
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
            [SlashChoiceProvider(typeof(AnnouncementRoleChoiceProvider))]
            [Parameter("role1"), Description("The first role to ping.")] string role1Name,
            [SlashChoiceProvider(typeof(AnnouncementRoleChoiceProvider))]
            [Parameter("role2"), Description("The second role to ping. Optional.")] string role2Name = null
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
            if (!Program.cfgjson.AnnouncementRoles.ContainsKey(role1Name) || (role2Name is not null && !Program.cfgjson.AnnouncementRoles.ContainsKey(role2Name)))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} The role name(s) you entered aren't recognised!", ephemeral: true);
                return;
            }
            if (role1Name == role2Name)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} You provided the same role name twice! Did you mean to use two different roles?", ephemeral: true);
                return;
            }

            EditAnnounceCache[ctx.User.Id] = (Convert.ToUInt64(messageId), role1Name, role2Name);

            await ctx.RespondWithModalAsync(new DiscordModalBuilder().WithTitle("Edit Announcement").WithCustomId("editannounce-modal-callback").AddTextInput(new DiscordTextInputComponent("editannounce-modal-new-text", style: DiscordTextInputStyle.Paragraph, value: msg.Content), "New announcement text. Do not include roles!"));
        }

        [Command("announce")]
        [Description("Announces something in the current channel, pinging an Insider role in the process.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task AnounceSlashCmd(SlashCommandContext ctx,
            [SlashChoiceProvider(typeof(WindowsInsiderChannelChoiceProvider))]
            [Parameter("role1"), Description("The first Insider role to ping.")] string insiderChannel1,
            [Parameter("announcement_message"), Description("The message to announce.")] string announcementMessage,
            [SlashChoiceProvider(typeof(WindowsInsiderChannelChoiceProvider))]
            [Parameter("role2"), Description("The second Insider role to ping.")] string insiderChannel2 = "")
        {
            if (insiderChannel1 == insiderChannel2)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Both insider channels cannot be the same! Simply set one instead.", ephemeral: true);
                return;
            }

            string roleKey1 = insiderChannel1.ToLower();

            await ctx.DeferResponseAsync(ephemeral: true);

            DiscordRole insiderRole1 = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles[roleKey1]);
            DiscordRole insiderRole2 = default;

            if (insiderChannel2 != "")
            {
                string roleKey2= insiderChannel2.ToLower();

                insiderRole2 = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles[roleKey2]);
            }

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

        [Command("announcetextcmd")]
        [TextAlias("announce")]
        [Description("Announces something in the current channel, pinging an Insider role in the process.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task AnnounceCmd(TextCommandContext ctx, [Description("'canary', 'dev', 'beta', 'rp', 'patch', 'rpbeta', 'betadev', 'candev'")] string roleName, [RemainingText, Description("The announcement message to send.")] string announcementMessage)
        {
            DiscordRole discordRole;

            if (Program.cfgjson.AnnouncementRoles.ContainsKey(roleName))
            {
                discordRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles[roleName]);
                await discordRole.ModifyAsync(mentionable: true);
                try
                {
                    await ctx.Message.DeleteAsync();
                    await ctx.Channel.SendMessageAsync($"{discordRole.Mention} {announcementMessage}");
                }
                catch
                {
                    // We still need to remember to make it unmentionable even if the msg fails.
                }
                await discordRole.ModifyAsync(mentionable: false);
            }
            else if (roleName == "rpbeta")
            {
                var rpRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["rp"]);
                var betaRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["beta"]);

                await rpRole.ModifyAsync(mentionable: true);
                await betaRole.ModifyAsync(mentionable: true);

                try
                {
                    await ctx.Message.DeleteAsync();
                    await ctx.Channel.SendMessageAsync($"{rpRole.Mention} {betaRole.Mention}\n{announcementMessage}");
                }
                catch
                {
                    // We still need to remember to make it unmentionable even if the msg fails.
                }

                await rpRole.ModifyAsync(mentionable: false);
                await betaRole.ModifyAsync(mentionable: false);
            }
            else if (roleName == "betadev")
            {
                var betaRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["beta"]);
                var devRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["dev"]);

                await betaRole.ModifyAsync(mentionable: true);
                await devRole.ModifyAsync(mentionable: true);

                try
                {
                    await ctx.Message.DeleteAsync();
                    await ctx.Channel.SendMessageAsync($"{betaRole.Mention} {devRole.Mention}\n{announcementMessage}");
                }
                catch
                {
                    // We still need to remember to make it unmentionable even if the msg fails.
                }

                await betaRole.ModifyAsync(mentionable: false);
                await devRole.ModifyAsync(mentionable: false);
            }
            else if (roleName == "candev")
            {
                var canaryRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["canary"]);
                var devRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["dev"]);

                await canaryRole.ModifyAsync(mentionable: true);
                await devRole.ModifyAsync(mentionable: true);

                try
                {
                    await ctx.Message.DeleteAsync();
                    await ctx.Channel.SendMessageAsync($"{canaryRole.Mention} {devRole.Mention}\n{announcementMessage}");
                }
                catch
                {
                    // We still need to remember to make it unmentionable even if the msg fails.
                }

                await canaryRole.ModifyAsync(mentionable: false);
                await devRole.ModifyAsync(mentionable: false);
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That role name isn't recognised!");
                return;
            }

        }

        internal class WindowsInsiderChannelChoiceProvider : IChoiceProvider
        {
            public async ValueTask<IEnumerable<DiscordApplicationCommandOptionChoice>> ProvideAsync(CommandParameter _)
            {
                return new List<DiscordApplicationCommandOptionChoice>
                {
                    new("Canary Channel", "Canary"),
                    new("Dev Channel", "Dev"),
                    new("Beta Channel", "Beta"),
                    new("Release Preview Channel", "RP")
                };
            }
        }

        internal class AnnouncementRoleChoiceProvider : IChoiceProvider
        {
            public async ValueTask<IEnumerable<DiscordApplicationCommandOptionChoice>> ProvideAsync(CommandParameter _)
            {
                List<DiscordApplicationCommandOptionChoice> list = new();
                foreach (var role in Program.cfgjson.AnnouncementRoles)
                {
                    if (Program.cfgjson.AnnouncementRolesFriendlyNames is not null && Program.cfgjson.AnnouncementRolesFriendlyNames.ContainsKey(role.Key))
                        list.Add(new DiscordApplicationCommandOptionChoice(Program.cfgjson.AnnouncementRolesFriendlyNames[role.Key], role.Key));
                    else
                        list.Add(new DiscordApplicationCommandOptionChoice(role.Key, role.Key));
                }
                return list;
            }
        }
    }
}