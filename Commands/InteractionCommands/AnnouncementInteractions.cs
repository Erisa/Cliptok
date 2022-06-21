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

            [Choice("Dev Channel", "Dev")]
            [Choice("Beta Channel", "Beta")]
            [Choice("Release Preview Channel", "RP")]
            [Option("insider_role1", "The first insider role to ping.")] string insiderChannel1,

            [Choice("Dev Channel", "Dev")]
            [Choice("Beta Channel", "Beta")]
            [Choice("Release Preview Channel", "RP")]
            [Option("insider_role2", "The second insider role to ping.")] string insiderChannel2 = "",

            [Option("thread", "The thread to mention in the announcement.")] DiscordChannel threadChannel = default,
            [Option("flavour_text", "Extra text appended on the end of the main line, replacing :WindowsInsider: or :Windows10:")] string flavourText = "",
            [Option("autothread_name", "If no thread is given, create a thread with this name.")] string autothreadName = "Build {0} ({1})",

            [Option("lockdown", "If supplied, lock the channel for a certain period of time after announcing the build.")] string lockdownTime = ""
        )
        {
            if (windowsVersion == 10 && insiderChannel1 != "RP")
            {
                await ctx.RespondAsync(text: "Windows 10 only has a Release Preview Channel.", ephemeral: true);
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

            channelString.Append($"**{insiderChannel1Pretty} ");

            if (insiderChannel2 != "")
            {
                string insiderChannel2Pretty = insiderChannel2 == "RP" ? "Release Preview" : insiderChannel2;
                channelString.Append($"**and **{insiderChannel2Pretty}** Channels");
            }
            else
            {
                channelString.Append("Channel**");
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

            if (threadChannel == default)
            {
                string threadBrackets = insiderChannel1;
                if (insiderChannel2 != "")
                    threadBrackets = $"{insiderChannel1} & {insiderChannel2}";

                if (insiderChannel1 == "RP" && insiderChannel2 == "" && windowsVersion == 10)
                    threadBrackets = "10 RP";

                string threadName = string.Format(autothreadName, buildNumber, threadBrackets);
                threadChannel = await ctx.Channel.CreateThreadAsync(threadName, AutoArchiveDuration.Week, ChannelType.PublicThread, "Creating thread for Insider build.");
                var initialMsg = await threadChannel.SendMessageAsync(blogLink);
                await initialMsg.PinAsync();
            }

            await insiderRole1.ModifyAsync(mentionable: true);
            if (insiderChannel2 != "")
                await insiderRole2.ModifyAsync(mentionable: true);

            await ctx.RespondAsync($"{insiderRole1.Mention}{(insiderChannel2 != "" ? $" {insiderRole2.Mention}\n" : " - ")}Hi Insiders!\n\nWindows {windowsVersion} Build **{buildNumber}** has just been released to {channelString}! {flavourText}\n\nCheck it out here: {blogLink}\n\nDiscuss it here: {threadChannel.Mention}");

            await insiderRole1.ModifyAsync(mentionable: false);
            if (insiderChannel2 != "")
                await insiderRole2.ModifyAsync(mentionable: false);

            if (lockdownTime != "")
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
