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
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(userPermissions: [DiscordPermission.ModerateMembers], botPermissions: [])]
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
                await ctx.RespondAsync($"This command only works in <#{Program.cfgjson.InsidersChannel}>!", ephemeral: true);
                return;
            }

            await ctx.DeferResponseAsync(ephemeral: true);

            try
            {
                await InsidersHelpers.AnnounceInsiderBuildAsync(buildNumber, blogLink, insiderChannel1, insiderChannel2, createNewThread, threadChannel, threadChannel2, flavourText, autothreadName, lockdownTime, forceReannounce, responsibleModerator: ctx.User);
            }
            catch (InsiderAnnouncementException ex)
            {
                await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Error} {ex.Message}", ephemeral: true);
                return;
            }

            await ctx.FollowupAsync($"{Program.cfgjson.Emoji.Success} Successfully announced build!");
        }

        [Command("editannounce")]
        [Description("Edit an announcement, preserving the ping highlight.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(userPermissions: [DiscordPermission.ModerateMembers], botPermissions: [])]
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
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(userPermissions: [DiscordPermission.ModerateMembers], botPermissions: [])]
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

            DiscordRole insiderRole1 = await ctx.Guild.GetRoleAsync(insiderChannel1);
            DiscordRole insiderRole2 = insiderChannel2 == default ? default : await ctx.Guild.GetRoleAsync(insiderChannel2);

            await insiderRole1.ModifyAsync(mentionable: true, reason: "Making role mentionable for Insider announcement.");
            if (insiderRole2 != default)
                await insiderRole2.ModifyAsync(mentionable: true, reason: "Making role mentionable for Insider announcement.");

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

            await insiderRole1.ModifyAsync(mentionable: false, reason: "Restoring role state after Insider announcement.");
            if (insiderRole2 != default)
                await insiderRole2.ModifyAsync(mentionable: false, reason: "Restoring role state after Insider announcement.");

            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Announcement sent successfully!");
        }
    }
}