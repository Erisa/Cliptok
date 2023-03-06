namespace Cliptok.Commands.InteractionCommands
{
    internal class AnnouncementInteractions : ApplicationCommandModule
    {
        [SlashCommand("announcebuild", "Announce a Windows Insider build in the current channel.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task AnnounceBuildSlashCommand(InteractionContext ctx,
            [Choice("Windows 10", 10)]
            [Choice("Windows 11", 11)]
            [Option("windows_version", "The Windows version to announce a build of. Must be either 10 or 11.")] long windowsVersion,

            [Option("build_number", "Windows build number, including any decimals (Decimals are optional). Do not include the word Build.")] string buildNumber,

            [Option("blog_link", "The link to the Windows blog entry relating to this build.")] string blogLink,

            [Choice("Canary Channel", "Canary")]
            [Choice("Dev Channel", "Dev")]
            [Choice("Beta Channel", "Beta")]
            [Choice("Release Preview Channel", "RP")]
            [Option("insider_role1", "The first insider role to ping.")] string insiderChannel1,

            [Choice("Canary Channel", "Canary")]
            [Choice("Dev Channel", "Dev")]
            [Choice("Beta Channel", "Beta")]
            [Choice("Release Preview Channel", "RP")]
            [Option("insider_role2", "The second insider role to ping.")] string insiderChannel2 = "",

            [Option("thread", "The thread to mention in the announcement.")] DiscordChannel threadChannel = default,
            [Option("flavour_text", "Extra text appended on the end of the main line, replacing :WindowsInsider: or :Windows10:")] string flavourText = "",
            [Option("autothread_name", "If no thread is given, create a thread with this name.")] string autothreadName = "Build {0} ({1})",

            [Option("lockdown", "Set 0 to not lock. Lock the channel for a certain period of time after announcing the build.")] string lockdownTime = "auto"
        )
        {
            if (Program.cfgjson.InsiderCommandLockedToChannel != 0 && ctx.Channel.Id != Program.cfgjson.InsiderCommandLockedToChannel)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command only works in <#{Program.cfgjson.InsiderCommandLockedToChannel}>!", ephemeral: true);
                return;
            }

            if (windowsVersion == 10 && insiderChannel1 != "RP")
            {
                await ctx.RespondAsync(text: $"{Program.cfgjson.Emoji.Error} Windows 10 only has a Release Preview Channel.", ephemeral: true);
                return;
            }

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
            else
            {
                roleKey1 = insiderChannel1.ToLower();
            }

            DiscordRole insiderRole1 = ctx.Guild.GetRole(Program.cfgjson.AnnouncementRoles[roleKey1]);
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
                else
                {
                    roleKey2 = insiderChannel2.ToLower();
                }

                insiderRole2 = ctx.Guild.GetRole(Program.cfgjson.AnnouncementRoles[roleKey2]);
            }

            string pingMsgString = $"{insiderRole1.Mention}{(insiderChannel2 != "" ? $" {insiderRole2.Mention}\n" : " - ")}Hi Insiders!\n\n" +
                $"Windows {windowsVersion} Build **{buildNumber}** has just been released to {channelString}! {flavourText}\n\n" +
                $"Check it out here: {blogLink}";

            string noPingMsgString = $"{(windowsVersion == 11 ? Program.cfgjson.Emoji.Windows11 : Program.cfgjson.Emoji.Windows10)} Windows {windowsVersion} Build **{buildNumber}** has just been released to {channelString}! {flavourText}\n\n" +
                $"Check it out here: <{blogLink}>";

            DiscordMessage messageSent;
            if (Program.cfgjson.InsiderAnnouncementChannel == 0)
            {
                if (threadChannel != default)
                {
                    pingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";
                }
                else
                    pingMsgString += "\n\nDiscuss it in the thread below:";

                await insiderRole1.ModifyAsync(mentionable: true);
                if (insiderChannel2 != "")
                    await insiderRole2.ModifyAsync(mentionable: true);

                await ctx.RespondAsync(pingMsgString);
                messageSent = await ctx.GetOriginalResponseAsync();

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
                else
                    noPingMsgString += "\n\nDiscuss it in the thread below:";

                await ctx.RespondAsync(noPingMsgString);
                messageSent = await ctx.GetOriginalResponseAsync();
            }

            if (threadChannel == default)
            {
                string threadBrackets = insiderChannel1;
                if (insiderChannel2 != "")
                    threadBrackets = $"{insiderChannel1} & {insiderChannel2}";

                if (insiderChannel1 == "RP" && insiderChannel2 == "" && windowsVersion == 10)
                    threadBrackets = "10 RP";

                string threadName = string.Format(autothreadName, buildNumber, threadBrackets);
                threadChannel = await messageSent.CreateThreadAsync(threadName, AutoArchiveDuration.Week, "Creating thread for Insider build.");

                var initialMsg = await threadChannel.SendMessageAsync($"{blogLink}");
                await initialMsg.PinAsync();
            }

            if (Program.cfgjson.InsiderAnnouncementChannel != 0)
            {
                pingMsgString += $"\n\nDiscuss it here: {threadChannel.Mention}";

                var announcementChannel = await ctx.Client.GetChannelAsync(Program.cfgjson.InsiderAnnouncementChannel);
                await insiderRole1.ModifyAsync(mentionable: true);
                if (insiderChannel2 != "")
                    await insiderRole2.ModifyAsync(mentionable: true);

                var msg = await announcementChannel.SendMessageAsync(pingMsgString);

                await insiderRole1.ModifyAsync(mentionable: false);
                if (insiderChannel2 != "")
                    await insiderRole2.ModifyAsync(mentionable: false);

                if (announcementChannel.Type is ChannelType.News)
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

    }
}
