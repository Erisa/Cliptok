namespace Cliptok.Commands
{
    public class AnnouncementCmds
    {
        [Command("announcebuild")]
        [Description("Announce a Windows Insider build in the current channel.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task AnnounceBuildSlashCommand(SlashCommandContext ctx,
            [SlashChoiceProvider(typeof(WindowsVersionChoiceProvider))]
            [Parameter("windows_version"), Description("The Windows version to announce a build of. Must be either 10 or 11.")] long windowsVersion,

            [Parameter("build_number"), Description("Windows build number, including any decimals (Decimals are optional). Do not include the word Build.")] string buildNumber,

            [Parameter("blog_link"), Description("The link to the Windows blog entry relating to this build.")] string blogLink,

            [SlashChoiceProvider(typeof(WindowsInsiderChannelChoiceProvider))]
            [Parameter("insider_role1"), Description("The first insider role to ping.")] string insiderChannel1,

            [SlashChoiceProvider(typeof(WindowsInsiderChannelChoiceProvider))]
            [Parameter("insider_role2"), Description("The second insider role to ping.")] string insiderChannel2 = "",

            [Parameter("canary_create_new_thread"), Description("Enable this option if you want to create a new Canary thread for some reason")] bool canaryCreateNewThread = false,
            [Parameter("thread"), Description("The thread to mention in the announcement.")] DiscordChannel threadChannel = default,
            [Parameter("flavour_text"), Description("Extra text appended on the end of the main line, replacing :WindowsInsider: or :Windows10:")] string flavourText = "",
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
            }

            if (windowsVersion == 10 && insiderChannel1 != "RP")
            {
                await ctx.RespondAsync(text: $"{Program.cfgjson.Emoji.Error} Windows 10 only has a Release Preview Channel.", ephemeral: true);
                return;
            }

            // Avoid duplicate announcements
            if (await Program.db.SetContainsAsync("announcedInsiderBuilds", buildNumber) && !forceReannounce)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Build {buildNumber} has already been announced! If you are sure you want to announce it again, set `force_reannounce` to True.", ephemeral: true);
                return;
            }
            await Program.db.SetAddAsync("announcedInsiderBuilds", buildNumber);

            if (flavourText == "" && windowsVersion == 10)
            {
                flavourText = Program.cfgjson.Emoji.Windows10;
            }
            else if (flavourText == "" && windowsVersion == 11)
            {
                flavourText = Program.cfgjson.Emoji.Insider;
            }

            string roleKey1;
            if (windowsVersion == 10 && insiderChannel1 == "RP")
            {
                roleKey1 = "rp10";
            }
            else if (windowsVersion == 10 && insiderChannel1 == "Beta")
            {
                roleKey1 = "beta10";
            }
            else
            {
                roleKey1 = insiderChannel1.ToLower();
            }

            DiscordRole insiderRole1 = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles[roleKey1]);
            DiscordRole insiderRole2 = default;

            StringBuilder channelString = new();

            string insiderChannel1Pretty = insiderChannel1 == "RP" ? "Release Preview" : insiderChannel1;

            if (insiderChannel1 == "RP" || insiderChannel2 == "RP")
            {
                channelString.Append($"the Windows {windowsVersion} ");
            }
            else
            {
                channelString.Append("the ");
            }

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
                string roleKey2;
                if (windowsVersion == 10 && insiderChannel2 == "RP")
                {
                    roleKey2 = "rp10";
                }
                else if (windowsVersion == 10 && insiderChannel2 == "Beta")
                {
                    roleKey2 = "beta10";
                }
                else
                {
                    roleKey2 = insiderChannel2.ToLower();
                }

                insiderRole2 = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles[roleKey2]);
            }

            string pingMsgBareString = $"{insiderRole1.Mention}{(insiderChannel2 != "" ? $" {insiderRole2.Mention}\n" : " - ")}Hi Insiders!\n\n" +
                $"Windows {windowsVersion} Build **{buildNumber}** has just been released to {channelString}! {flavourText}\n\n" +
                $"Check it out here: {blogLink}";

            string innerThreadMsgString = $"Hi Insiders!\n\n" +
                $"Windows {windowsVersion} Build **{buildNumber}** has just been released to {channelString}! {flavourText}\n\n" +
                $"Check it out here: {blogLink}";

            string noPingMsgString = $"{(windowsVersion == 11 ? Program.cfgjson.Emoji.Windows11 : Program.cfgjson.Emoji.Windows10)} Windows {windowsVersion} Build **{buildNumber}** has just been released to {channelString}! {flavourText}\n\n" +
                $"Check it out here: <{blogLink}>";

            string pingMsgString = pingMsgBareString;

            DiscordMessage messageSent;
            if (Program.cfgjson.InsiderAnnouncementChannel == 0)
            {
                if (threadChannel != default)
                {
                    pingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                }
                else if (insiderChannel1 == "Canary" && insiderChannel2 == "" && Program.cfgjson.InsiderCanaryThread != 0 && autothreadName == "Build {0} ({1})" && !canaryCreateNewThread)
                {
                    threadChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderCanaryThread);
                    pingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                    var msg = await threadChannel.SendMessageAsync(innerThreadMsgString);
                    try
                    {
                        await msg.PinAsync();
                    }
                    catch
                    {
                        // most likely we hit max pins, we can handle this later
                        // either way, lets ignore for now
                    }
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
                }
                else if (insiderChannel1 == "Canary" && insiderChannel2 == "" && Program.cfgjson.InsiderCanaryThread != 0 && autothreadName == "Build {0} ({1})" && !canaryCreateNewThread)
                {
                    threadChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderCanaryThread);
                    noPingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                    var msg = await threadChannel.SendMessageAsync(innerThreadMsgString);
                    try
                    {
                        await msg.PinAsync();
                    }
                    catch
                    {
                        // most likely we hit max pins, we can handle this later
                        // either way, lets ignore for now
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

                if (insiderChannel1 == "RP" && insiderChannel2 == "" && windowsVersion == 10)
                    threadBrackets = "10 RP";

                string threadName = string.Format(autothreadName, buildNumber, threadBrackets);
                threadChannel = await messageSent.CreateThreadAsync(threadName, DiscordAutoArchiveDuration.Week, "Creating thread for Insider build.");

                var initialMsg = await threadChannel.SendMessageAsync($"{blogLink}");
                await initialMsg.PinAsync();
            }

            if (Program.cfgjson.InsiderAnnouncementChannel != 0)
            {
                pingMsgString = pingMsgBareString + $"\n\nDiscuss it here: {threadChannel.Mention}";

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
                    lockDuration = HumanDateParser.HumanDateParser.Parse(lockdownTime).Subtract(DateTime.Now);
                }
                catch
                {
                    lockDuration = TimeSpan.FromHours(2);
                }

                await LockdownHelpers.LockChannelAsync(user: ctx.User, channel: ctx.Channel, duration: lockDuration);
            }
        }

        [Command("editannouncetextcmd")]
        [TextAlias("editannounce")]
        [Description("Edit an announcement, preserving the ping highlight.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task EditAnnounce(
            TextCommandContext ctx,
            [Description("The ID of the message to edit.")] ulong messageId,
            [Description("The short name for the role to ping.")] string roleName,
            [RemainingText, Description("The new message content, excluding the ping.")] string content
        )
        {
            DiscordRole discordRole;

            if (Program.cfgjson.AnnouncementRoles.ContainsKey(roleName))
            {
                discordRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles[roleName]);
                await discordRole.ModifyAsync(mentionable: true);
                try
                {
                    await ctx.Message.DeleteAsync();
                    var msg = await ctx.Channel.GetMessageAsync(messageId);
                    await msg.ModifyAsync($"{discordRole.Mention} {content}");
                }
                catch
                {
                    // We still need to remember to make it unmentionable even if the msg fails.
                }
                await discordRole.ModifyAsync(mentionable: false);
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That role name isn't recognised!");
                return;
            }
        }

        [Command("announcetextcmd")]
        [TextAlias("announce")]
        [Description("Announces something in the current channel, pinging an Insider role in the process.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task AnnounceCmd(TextCommandContext ctx, [Description("'canary', 'dev', 'beta', 'beta10', 'rp', 'rp10', 'patch', 'rpbeta', 'rpbeta10', 'betadev', 'candev'")] string roleName, [RemainingText, Description("The announcement message to send.")] string announcementMessage)
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
            // this is rushed pending an actual solution
            else if (roleName == "rpbeta10")
            {
                var rpRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["rp10"]);
                var betaRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.AnnouncementRoles["beta10"]);

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

        internal class WindowsVersionChoiceProvider : IChoiceProvider
        {
            public async ValueTask<IEnumerable<DiscordApplicationCommandOptionChoice>> ProvideAsync(CommandParameter _)
            {
                return new List<DiscordApplicationCommandOptionChoice>
                {
                    new("Windows 10", "10"),
                    new("Windows 11", "11")
                };
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
    }
}