using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cliptok.Modules
{
    /// <summary>
    /// Partial class for holding all report types.
    /// </summary>
    partial class ReportTypes
    {
        static ReportTypes() { }
    }

    /// <summary>
    /// Exception called whenever an error occurs in any of the report command.
    /// </summary>
    class ReportCommandError : Exception
    {
        public ReportCommandError(DiscordMessageBuilder builder)
        {
            Builder = builder;
        }

        public ReportCommandError(string content)
        {
            Builder = new DiscordMessageBuilder().WithContent(content);
        }

        public DiscordMessageBuilder Builder { get; }
    }

    /// <summary>
    /// Used to interact with buttons on the generated report message.
    /// Discord has support for Components through their API, but unfortunately DSharpPlus doesn't provide that.
    /// </summary>
    class ReportInteraction
    {
        /// <summary>
        /// Initialize the report interaction.
        /// </summary>
        /// <param name="emojiNameValue">Name of the emoji to use for the interaction.</param>
        /// <param name="actionNameValue">Name of the action that will be executed.</param>
        /// <param name="interaction">Action to execute (delegate).</param>
        public ReportInteraction(string emojiNameValue, string actionNameValue, InteractDelegate interaction)
        {
            EmojiName = emojiNameValue;
            ActionName = actionNameValue;
            Interact = interaction;
        }

        /// <summary>
        /// Initialize the interaction's emoji.
        /// </summary>
        /// <param name="client">Discord client to use.</param>
        public void InitEmoji(DiscordClient client)
        {
            Emoji = DiscordEmoji.FromName(client, EmojiName);
        }

        /// <summary>
        /// Name of the emoji.
        /// </summary>
        public string EmojiName { get; }

        /// <summary>
        /// Discord emoji.
        /// </summary>
        public DiscordEmoji Emoji { get; private set; }

        /// <summary>
        /// Name of the action.
        /// </summary>
        public string ActionName { get; }

        public delegate void InteractDelegate(ReportObject report, DiscordUser user);
        /// <summary>
        /// Called on report interaction.
        /// </summary>
        public InteractDelegate Interact { get; }
    }

    /// <summary>
    /// The report module used to rapidly report a message, and process it afterwards.
    /// For possible report types, check the *ReportTypes* folder.
    /// </summary>
    /// <remarks>
    /// Flagged content are stored in the "reports_users_{report type}" key, each content ID point to a report ID.
    /// Pending reports are stored in the "reports_pending" key. Pending reports are sent for review to the #pending-reports channel
    /// Reviewed reports are stored in the "reports_reviewed" key. Reviewed reports are sent to #reviewed-reports channel (and their pending report deleted of course).
    /// 
    /// There is a *reports_pending* key so that if the bot restarts it will only process pending reports
    /// better than having to iterate through all reports to check for pending reports.
    ///
    /// The point of this module:
    /// To gain the time of both the user and moderators. This should really avoid wasting time with mod mail.
    /// With mod mail, it is like:
    /// 1. User sends a DM to mod mail to report something (sometimes without a link)
    /// 2. A moderator reads the mail from the mod mail channel
    /// 3. Once the moderator read the mail, the moderator switches to the appropriate channel
    /// 4. The moderator type the warn command against the reported user.
    /// 5. Then it goes back to mod mail and notify the user about the report.
    /// 6. Finally, the moderator closes the mail.
    /// Everything is done through commands!
    /// 
    /// With this module, it makes everything simpler.
    /// 1. The user will just type a single command for reporting a message.
    /// 2. A generated report message is then sent to the #pending-reports channel => A moderator will just have to click on one of the proposed reactions to take appropriate actions.
    /// 3. Users who reported the message are then notified, simple as that.
    /// </remarks>
    /// <seealso cref="ReportTypes"/>
    class Reports : BaseCommandModule
    {
        static Reports()
        {
            // have to do that in order to force static initialization of ReportTypes
            new ReportTypes();
        }

        public Reports()
        {
            ReportsInteractionConfig config = Program.cfgjson.reportsConfig.Interactions;

            reportInteractions = new List<ReportInteraction>();
            reportInteractions.Add(new ReportInteraction(config.AcceptWarnCleanEmoji, "Accept, warn and clean", ReportAcceptWarnClean));
            reportInteractions.Add(new ReportInteraction(config.AcceptWarnEmoji, "Accept and warn", ReportAcceptWarn));
            reportInteractions.Add(new ReportInteraction(config.AcceptNoWarnEmoji, "Accept without warning", ReportAcceptNoWarn));
            reportInteractions.Add(new ReportInteraction(config.RejectEmoji, "Reject", ReportReject));

            reportDatabase = new ReportDatabase(Program.db);

            if (Program.discord.Guilds.Count <= 0 || Program.discord.Guilds[0].IsUnavailable)
            {
                Program.discord.GuildAvailable += OnGuildAvailable;
            }
            else
            {
                _ = Init();
            }
        }

        /// <summary>
        /// Asynchronous function called by the class constructor
        /// so, channels, reports, etc can be initialized.
        /// </summary>
        private async Task OnGuildAvailable(DiscordClient client, GuildCreateEventArgs e)
        {
            Program.discord.GuildAvailable -= OnGuildAvailable;

            await Init();
        }

        private async Task Init()
        {

            // initialize all interactions
            foreach (ReportInteraction interaction in reportInteractions)
            {
                interaction.InitEmoji(Program.discord);
            }

            // initialize channels
            pendingReportsChannel = await Program.discord.GetChannelAsync(Program.cfgjson.PendingReportsChannel);
            reviewedReportsChannel = await Program.discord.GetChannelAsync(Program.cfgjson.ReviewedReportsChannel);
            defaultChannel = await Program.discord.GetChannelAsync(Program.cfgjson.HomeChannel);

            // monitor reaction and deletion event
            Program.discord.MessageReactionAdded += OnMessageReactionAdded;
            Program.discord.MessageDeleted += OnMessageDeleted;

            // gather all pending reports
            await reportDatabase.ForEachPendingReports(HandleReportCallback);
        }

        public async Task HandleReportCallback(string jsonData)
        {
            ReportObject report = await DeserializeReportObject(jsonData);
            if (report.ReportHandle != null)
            {
                // handle the report immediately
                HandleReaction(Program.discord, report);
            }
            else
            {
                // it looks like the generated report message was deleted
                // so create a new one
                await CreateReportMessageWithInteraction(report, pendingReportsChannel);
                _ = SetReportObjectPending(report);
            }
        }

        public async Task<bool> IsUserReviewer(CommandContext ctx, DiscordUser user)
        {
            // so here we have to check if the user being reported is a moderator
            DiscordMember member = await ctx.Guild.GetMemberAsync(user.Id);
            return IsUserReviewer(member);
        }

        public bool IsUserReviewer(DiscordMember member)
        {
            // assume that the member is a reviewer if he has access to reports channel
            return member.PermissionsIn(pendingReportsChannel).HasPermission(Permissions.AccessChannels);
        }

        /// <summary>
        /// Checks if the context user can make a report against the target user.
        /// </summary>
        public async Task CheckReport(CommandContext ctx, DiscordMember member, DiscordUser targetUser)
        {
#if DEBUG
            // small debug cheat, useful if you have no friends
            return;
#endif

            if (targetUser == ctx.Message.Author)
            {
                // prevent the user from reporting himself (attempt at trolling)
                throw new ReportCommandError("You cannot report yourself.");
            }

            if (targetUser == ctx.Client.CurrentUser)
            {
                throw new ReportCommandError("The bot cannot be reported, he never says anything bad.");
            }

            if (!Program.cfgjson.reportsConfig.AllowUserReportModerator)
            {
                // so here we have to check if the user being reported is a moderator
                if (await IsUserReviewer(ctx, targetUser))
                {
                    // assume that if the reported being reported has access to reports channel
                    // then it's a moderator
                    throw new ReportCommandError($"Can't report <@!{targetUser.Id}>.");
                }
            }
        }

        /// <summary>
        /// Generic function for creating and modifying reports.
        /// </summary>
        /// <param name="reportType">Type of the report to use.</param>
        /// <param name="ID">The content ID.</param>
        /// <param name="reason">Reason to give for the report.</param>
        /// <param name="createReport">Delegate called to create the specific report.</param>
        /// <returns></returns>
        private async Task<ReportObject> GenericManageReport(CommandContext ctx, DiscordGuild guild, DiscordMember signaler, string reportType, ulong ID, string reason, Func<ReportInfo> createReport)
        {
            ulong reportID = await reportDatabase.GetOrSetReportFromContentID(ID, reportType);

            ReportObject report = await GetReviewedReportObject(reportID);
            if (report != null)
            {
                if (!report.MessageData.CanReportAfterReview())
                {
                    // already reviewed/moderated
                    throw new ReportCommandError(GenerateReviewedMessage(report));
                }
                else
                {
                    // start over with a new report
                    reportID = await reportDatabase.RecreateNewReportFromContentID(ID, reportType);
                    report = new ReportObject(reportID, guild, createReport(), reason);
                }
            }
            else
            {
                // the report wasn't reviewed but check if there is a pending report
                report = await GetPendingReportObject(reportID);
                if (report == null)
                {
                    // create a brand new report
                    report = new ReportObject(reportID, guild, createReport(), reason);
                }
                else if (report.Signalers.Exists(x => x == signaler))
                {
                    // already reported
                    throw new ReportCommandError(GenerateAlreadyReportObject(report));
                }
            }

            // add the user to the report
            report.Signalers.Add(signaler);

            if (report.ReportHandle == null)
            {
                // create a message in the log channel about the report
                await CreateReportMessageWithInteraction(report, pendingReportsChannel);
            }
            else
            {
                // modify, will automatically add new users
                await ModifyReportMessage(report);
            }

            // add the new report
            _ = SetReportObjectPending(report);

            // notify the user about the report
            await signaler.SendMessageAsync(GenerateReportObject(report));

            return report;
        }

        [Command("report")]
        [Description("Reports a message to the moderation team.")]
        public async Task ReportCommand(
            CommandContext ctx,
            [Description("The message to report. Use *Copy link* to get the message link. *Copy ID* works as long as the command in executed in the same channel as the message to be reported.")] DiscordMessage message,
            [Description("The reason for the report. If you want to report multiple messages, don't use this parameter, make a new report instead"), RemainingText] string reason = ""
        )
        {
            // delete the callee message to avoid others from judging the signalman
            DeleteMessageContext(ctx);

            DiscordMember signaler = (DiscordMember)ctx.Member;
            try
            {
                signaler = await GetContextMember(ctx, message.Channel.Guild);
            }
            catch (Exception)
            {
                // thanks to DSharpPlus, can't even reply to the "user" who sent a DM
                return;
            }

            if (message.Channel.Guild.Id != pendingReportsChannel.Guild.Id)
            {
                _ = signaler.SendMessageAsync("Can't report from a different server.");
                return;
            }

            try
            {
                await CheckReport(ctx, signaler, message.Author);
                await GenericManageReport(ctx, message.Channel.Guild, signaler, "message", message.Id, reason, () => new MessageReportInfo(message));
            }
            catch(ReportCommandError e)
            {
                _ = signaler.SendMessageAsync(e.Builder);
                return;
            }
        }

        /// <summary>
        /// This command cannot be user in DMs, it must be used in the server that the target user is in.
        /// </summary>
        [Command("report_u")]
        [Description("Reports user to the moderation team. Make sure to give a valid reason. For example if the user is sending scams, spamming DMs, you can use this command."), HomeServer]
        public async Task ReportUserCommand(
            CommandContext ctx,
            [Description("The user to report (must be present in the server).")] DiscordMember member,
            [Description("The reason for the user report."), RemainingText] string reason = ""
        )
        {
            DeleteMessageContext(ctx);

            // Actually it's not really needed if the first command parameter is a discord member
            /*
            try
            {
                // make sure that the user to report is in the same server as the context user
                await ctx.Guild.GetMemberAsync(member.Id);
            }
            catch(Exception)
            {
                _ = ctx.Member.SendMessageAsync("The specified user is not present in the server you want to report in.");
                return;
            }
            */

            DiscordMember signaler = (DiscordMember)ctx.Member;
            try
            {
                signaler = await GetContextMember(ctx, ctx.Guild);
            }
            catch (Exception)
            {
                return;
            }

            try
            {
                await CheckReport(ctx, signaler, member);
                await GenericManageReport(ctx, ctx.Guild, signaler, "user", member.Id, reason, () => new UserReportInfo(member));
            }
            catch (ReportCommandError e)
            {
                _ = signaler.SendMessageAsync(e.Builder);
                return;
            }
        }

        [Command("reportf")]
        [Description("Fix a pending report.")]
        private async Task FixReport(
            CommandContext ctx,
            [Description("The report ID to fix (number), must not be reviewed.")] ulong reportID,
            [Description("New reason to specify.")] string newReason = ""
            )
        {
            DeleteMessageContext(ctx);

            ReportObject report = await GetPendingReportObject(reportID);
            if (report == null)
            {
                report = await GetReviewedReportObject(reportID);
                if (report == null)
                {
                    _ = ctx.Member.SendMessageAsync($"The report #{reportID} doesn't exist.");
                }
                else
                {
                    _ = ctx.Member.SendMessageAsync($"The report #{reportID} was already reviewed.");
                }
                return;
            }

            if (ctx.User != report.ReportOwner)
            {
                DiscordMember signaler = (DiscordMember)ctx.Member;
                try
                {
                    signaler = await GetContextMember(ctx, ctx.Guild);
                }
                catch (Exception)
                {
                    // it's ok to ignore, it means the user isn't in the server
                }

                if (signaler == null || !IsUserReviewer(signaler))
                {
                    // don't let anyone to edit a report, except their author and moderators
                    _ = ctx.Member.SendMessageAsync($"You must be the owner of the report (#{reportID}), or a moderator to be able to make changes.");
                    return;
                }
            }

            // set the new reason and store the report
            report.Reason = newReason;
            _ = SetReportObjectPending(report);

            // modify the generated report message as well
            _ = ModifyReportMessage(report);

            // send the edited report to the author
            // NOTE: should all signalers be notified as well?
            if (ctx.Member == report.ReportOwner)
            {
                await ctx.Member.SendMessageAsync(GenerateReportFixedObject(report));
            }
        }

        [Command("reports")]
        [Description("Get your report stats.")]
        private async Task GetMyReports(CommandContext ctx)
        {
            DeleteMessageContext(ctx);

            ulong numValidated = await reportDatabase.GetUserReportCount(ctx.User.Id, ReportStatus.Validated);
            ulong numRejected = await reportDatabase.GetUserReportCount(ctx.User.Id, ReportStatus.Rejected);
            ulong numTotal = numValidated + numRejected;

            foreach (DiscordGuild guild in ctx.Client.Guilds.Values)
            {
                DiscordMember member;
                try
                {
                    member = await GetContextMember(ctx, guild);

                    _ = member.SendMessageAsync(
                            $"Reports in {guild.Name}:\n"
                            + $"- Validated reports: {numValidated}\n"
                            + $"- Rejected reports: {numRejected}\n"
                            + $"- Total reports: {numTotal}\n"
                        );
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// Return the member from context (or the guild).
        /// </summary>
        /// <param name="ctx">Context.</param>
        /// <param name="guild">Guild to use if the context member is null.</param>
        /// <returns></returns>
        private async Task<DiscordMember> GetContextMember(CommandContext ctx, DiscordGuild guild)
        {
            DiscordMember member = ctx.Member;
            if (ctx.Member == null)
            {
                // can be null if the user DMed the bot
                // only allow members in the same guild
                member = await guild.GetMemberAsync(ctx.User.Id);
            }

            return member;
        }

        /// <summary>
        /// Delete the discord message in the context.
        /// </summary>
        /// <param name="ctx">Command context.</param>
        private void DeleteMessageContext(CommandContext ctx)
        {
            if (!ctx.Message.Channel.IsPrivate)
            {
                _ = ctx.Message.DeleteAsync();
            }
        }

        /// <summary>
        /// Return a report object from ID (pending).
        /// </summary>
        /// <param name="reportID">ID to check.</param>
        /// <returns>Report object.</returns>
        private async Task<ReportObject> GetPendingReportObject(ulong reportID)
        {
            string jsonReport = await reportDatabase.GetPendingReport(reportID);
            // sanity check
            if (jsonReport != RedisValue.Null)
            {
                return await DeserializeReportObject(jsonReport);
            }

            return null;
        }

        /// <summary>
        /// Return a report object from ID (reviewed).
        /// </summary>
        /// <param name="reportID">ID to check.</param>
        /// <returns>Report object.</returns>
        private async Task<ReportObject> GetReviewedReportObject(ulong reportID)
        {
            string jsonReport = await reportDatabase.GetReviewedReport(reportID);
            if (jsonReport != null)
            {
                // report has already been reviewed but return it
                return await DeserializeReportObject(jsonReport);
            }

            return null;
        }

        /// <summary>
        /// Deserialize a report object from JSON data.
        /// </summary>
        /// <param name="jsonReport">JSON data to deserialize.</param>
        /// <returns>Report object.</returns>
        private async Task<ReportObject> DeserializeReportObject(string jsonReport)
        {
            ReportObject message = JsonConvert.DeserializeObject<ReportObject>(jsonReport);
            await message.ReadVolatile();

            return message;
        }

        /// <summary>
        /// Add a report to all users.
        /// </summary>
        /// <param name="report">Report to add.</param>
        /// <param name="status">Where to store the report.</param>
        private async Task AddReportToUsers(ReportObject report, ReportStatus status)
        {
            foreach (DiscordMember signaler in report.Signalers)
            {
                await reportDatabase.AddReportToUser(signaler.Id, status);
            }
        }

        /// <summary>
        /// Store the report in the pending list.
        /// </summary>
        /// <param name="report">Report to store.</param>
        private async Task SetReportObjectPending(ReportObject report)
        {
            await reportDatabase.SetReportPending(report.Id, JsonConvert.SerializeObject(report));
        }

        /// <summary>
        /// Store the report in the reviewed list.
        /// </summary>
        /// <param name="report">Report to store.</param>
        private async Task SetReportObjectReviewed(ReportObject report, DiscordUser moderatedBy, ReportStatus status)
        {
            report.Status = status;
            report.HandledBy = moderatedBy;
            _ = report.ReportHandle.DeleteAsync();

            // tell about the report
            _ = CreateReportMessage(report, reviewedReportsChannel);

            // report has been reviewed to move it appropriately
            await reportDatabase.SetReportReviewed(report.Id, JsonConvert.SerializeObject(report));

            _ = AddReportToUsers(report, status);
        }

        /// <summary>
        /// Return the first moderator in user list.
        /// </summary>
        /// <param name="client">Discord client.</param>
        /// <param name="users">User list.</param>
        /// <returns></returns>
        private DiscordUser GetModeratorFromUsers(DiscordClient client, IReadOnlyList<DiscordUser> users)
        {
            foreach(DiscordUser user in users)
            {
                if (user != client.CurrentUser)
                {
                    return user;
                }
            }

            return null;
        }

        /// <summary>
        /// Process reactions from given report.
        /// </summary>
        /// <param name="Client">Discord client.</param>
        /// <param name="report">Report to process.</param>
        private async void HandleReaction(DiscordClient Client, ReportObject report)
        {
            ReportInteraction bestInteraction = null;
            int highestCount = 0;
            int smallestCount = report.ReportHandle.Reactions[0].Count;

            foreach (DiscordReaction reaction in report.ReportHandle.Reactions)
            {
                ReportInteraction interaction = GetInteraction(reaction.Emoji);
                if (interaction != null)
                {
                    if (reaction.Count > highestCount)
                    {
                        bestInteraction = interaction;
                        highestCount = reaction.Count;
                    }
                    else if (reaction.Count < smallestCount)
                    {
                        smallestCount = reaction.Count;
                    }
                }
            }

            if (bestInteraction != null && highestCount != smallestCount)
            {
                IReadOnlyList<DiscordUser> acceptedUsers = await report.ReportHandle.GetReactionsAsync(bestInteraction.Emoji);
                DiscordUser mod = GetModeratorFromUsers(Client, acceptedUsers);
                bestInteraction.Interact(report, mod);
            }
        }

        /// <summary>
        /// Return the report object by the generated discord message.
        /// </summary>
        /// <param name="message">Generated discord message.</param>
        /// <returns>The report object.</returns>
        private async Task<ReportObject> GetPendingReportFromMessage(DiscordMessage message)
        {
            if (message.Embeds != null && message.Embeds.Count() > 0)
            {
                // first embed contains report info
                DiscordEmbed embed = message.Embeds[0];
                DiscordEmbedField field = embed.Fields.Single(f => f.Name == "Report ID");
                if (field != null)
                {
                    ulong reportID = Convert.ToUInt64(field.Value);
                    return await GetPendingReportObject(reportID);
                }
            }

            return null;
        }

        /// <summary>
        /// Return an interaction by discord emoji.
        /// </summary>
        /// <param name="emoji">Discord emoji.</param>
        /// <returns>Interaction.</returns>
        private ReportInteraction GetInteraction(DiscordEmoji emoji)
        {
            return reportInteractions.Find(x => x.Emoji == emoji);
        }

        /// <summary>
        /// Event for monitoring reports interaction through reactions.
        /// </summary>
        private async Task OnMessageReactionAdded(DiscordClient client, MessageReactionAddEventArgs e)
        {
            if (e.Channel != pendingReportsChannel)
            {
                // only monitor messages in the report channel
                return;
            }

            if (e.User == client.CurrentUser)
            {
                // ignore reactions from self
                return;
            }

            DiscordMessage message = e.Message;
            if (message.Author == null)
            {
                // no idea why the author is null sometimes
                // have to fetch the message again
                message = await e.Channel.GetMessageAsync(e.Message.Id);
            }

            if (message.Author != client.CurrentUser)
            {
                // only monitor messages in the report channel
                return;
            }

            ReportInteraction interaction = GetInteraction(e.Emoji);
            if (interaction != null)
            {
                ReportObject report = await GetPendingReportFromMessage(message);
                if (report != null)
                {
                    // this report is pending so accept emojis
                    interaction.Interact(report, e.User);
                }
            }
        }

        /// <summary>
        /// Regenerate a report message in the report channels if the message was deleted.
        /// Otherwise the report would be awaited in the void.
        /// </summary>
        private async Task OnMessageDeleted(DiscordClient client, MessageDeleteEventArgs e)
        {
            if (e.Channel != pendingReportsChannel || e.Message.Author != client.CurrentUser)
            {
                // only monitor messages in the report channel
                return;
            }

            ReportObject report = await GetPendingReportFromMessage(e.Message);
            if (report != null)
            {
                // recreate the message if it was deleted
                await CreateReportMessageWithInteraction(report, pendingReportsChannel);
                _ = SetReportObjectPending(report);
            }
        }

        /// <summary>
        /// Create a report message with reactions.
        /// </summary>
        private async Task CreateReportMessage(ReportObject report, DiscordChannel channel)
        {
            if (channel != null)
            {
                report.ReportHandle = await channel.SendMessageAsync(GenerateReportMessage(report));
            }
        }

        /// <summary>
        /// Create a report message with reactions.
        /// </summary>
        private async Task CreateReportMessageWithInteraction(ReportObject report, DiscordChannel channel)
        {
            await CreateReportMessage(report, channel);
            // create reactions for interaction
            _ = CreateReportInteractions(report);
        }

        /// <summary>
        /// Modify an existing report message by report object.
        /// </summary>
        /// <param name="report">Report object to modify the message from.</param>
        private async Task ModifyReportMessage(ReportObject report)
        {
            report.ReportHandle = await report.ReportHandle.ModifyAsync(GenerateReportMessage(report));
        }

        /// <summary>
        /// Create interaction emojis for the report object's generated message.
        /// </summary>
        /// <param name="report">The report object.</param>
        private async Task CreateReportInteractions(ReportObject report)
        {
            // create reactions for interaction
            foreach (ReportInteraction interaction in reportInteractions)
            {
                await report.ReportHandle.CreateReactionAsync(interaction.Emoji);
            }
        }

        /// <summary>
        /// Call to validate a specific report.
        /// </summary>
        /// <param name="report">Report to validate.</param>
        /// <param name="moderatedBy">The moderator who reviewed the report.</param>
        private void ValidateReport(ReportObject report, DiscordUser moderatedBy)
        {
            _ = SetReportObjectReviewed(report, moderatedBy, ReportStatus.Validated);

            // notify all signalers about the report
            report.NotifyUserReport(NotifyUserReportAccepted);
        }

        private void WarnUserFromReport(ReportObject report, DiscordUser moderatedBy)
        {
            DiscordChannel channel = report.MessageData.Channel;
            if (channel == null)
            {
                channel = defaultChannel;
            }

            // give the reported user a warning
            _ = Warnings.GiveWarningAsync(
                report.MessageData.Author,
                moderatedBy,
                report.Reason,
                report.MessageData.ContextLink,
                report.MessageData.Channel
            );
        }

        /// <summary>
        /// Called to accept, validate a specific report, warn the reported user and delete his message.
        /// </summary>
        /// <param name="report">The report to validate.</param>
        /// <param name="moderatedBy">The moderator who reviewed the report.</param>
        private void ReportAcceptWarnClean(ReportObject report, DiscordUser moderatedBy)
        {
            report.ActionTaken = "Warn and clean";

            ValidateReport(report, moderatedBy);

            // give the reported user a warning
            WarnUserFromReport(report, moderatedBy);

            // delete his message
            IReportedContent content = report.MessageData.GetReportedContent();
            if (content != null)
            {
                // we've got a content so try to delete it
                content.TryDelete();
            }
        }

        /// <summary>
        /// Called to accept, validate a specific report, and warn the reported user.
        /// </summary>
        /// <param name="report">The report to validate.</param>
        /// <param name="moderatedBy">The moderator who reviewed the report.</param>
        private void ReportAcceptWarn(ReportObject report, DiscordUser moderatedBy)
        {
            report.ActionTaken = "Warn";

            ValidateReport(report, moderatedBy);

            // give the reported user a warning
            WarnUserFromReport(report, moderatedBy);

            // should the message be deleted?
            // maybe a third button to accept & delete?
        }

        /// <summary>
        /// Called to accept, validate a specific report, without warning the reported user.
        /// </summary>
        /// <param name="report">The report to validate.</param>
        /// <param name="moderatedBy">The moderator who reviewed the report.</param>
        private void ReportAcceptNoWarn(ReportObject report, DiscordUser moderatedBy)
        {
            report.ActionTaken = "No warn";

            ValidateReport(report, moderatedBy);
        }

        /// <summary>
        /// Call to reject a specific report.
        /// </summary>
        /// <param name="report">Report to validate.</param>
        /// <param name="moderatedBy">The moderator who reviewed the report.</param>
        private void ReportReject(ReportObject report, DiscordUser moderatedBy)
        {
            _ = SetReportObjectReviewed(report, moderatedBy, ReportStatus.Rejected);

            // notify all signalers about the report
            report.NotifyUserReport(NotifyUserReportRejected);
        }

        /// <summary>
        /// Called to notify an user about the report.
        /// </summary>
        /// <param name="report">The report.</param>
        /// <param name="member">User to notify.</param>
        private void NotifyUserReportAccepted(ReportObject report, DiscordMember member)
        {
            // notify the user that he is making the community a better place for people
            member.SendMessageAsync(GenerateReportValidatedMessage(report));
        }

        /// <summary>
        /// Called to notify an user about the report.
        /// </summary>
        /// <param name="report">The report.</param>
        /// <param name="member">User to notify.</param>
        private void NotifyUserReportRejected(ReportObject report, DiscordMember member)
        {
            member.SendMessageAsync(GenerateReportRejectedMessage(report));
        }

        /// <summary>
        /// Generate an embed based on the message.
        /// </summary>
        /// <param name="message">Message to generate embed from.</param>
        /// <param name="color">Embed color.</param>
        /// <returns>Built embeded message.</returns>
        public DiscordEmbedBuilder GenerateEmbedMessage(ReportObject report, DiscordColor color)
        {
            return report.MessageData.GenerateEmbed()
                   .WithColor(color)
                   .WithTitle($"Report #{report.Id:00000}")
                   .AddField("Type", report.MessageData.Type.Description)
                   .AddField("Reason", report.Reason);
        }

        /// <summary>
        /// Generate a report message for moderators.
        /// </summary>
        /// <param name="reported">The reported message information.</param>
        /// <returns>Built message</returns>
        public DiscordMessageBuilder GenerateReportMessage(ReportObject reported)
        {
            string reportedBy = "";
            foreach(DiscordMember signaler in reported.Signalers)
            {
                reportedBy += $"<@!{signaler.Id}> ";
            }

            string actions = "";
            foreach(ReportInteraction interaction in reportInteractions)
            {
                actions += $"{interaction.Emoji.ToString()} {interaction.ActionName}\n";
            }

            DiscordEmbedBuilder embededMessage = GenerateEmbedMessage(reported, new DiscordColor(255, 0, 255))
                .AddField("Report ID", reported.Id.ToString("D5"))
                .AddField("Status", reported.Status.ToString())
                .AddField("Reported by", reportedBy);

            if (reported.Status != ReportStatus.Pending)
            {
                // was reviewed
                if (reported.HandledBy != null)
                {
                    embededMessage = embededMessage.AddField("Reviewed by", $"<@!{reported.HandledBy.Id}>");
                }

                if (reported.ActionTaken != null && reported.ActionTaken.Length > 0)
                {
                    embededMessage = embededMessage.AddField("Action taken", reported.ActionTaken);
                }
            }
            else
            {
                embededMessage.AddField("Actions", actions);

                if (Program.cfgjson.reportsConfig.PingModeration)
                {
                    // ping moderator and admin roles
                    return new DiscordMessageBuilder()
                        .WithEmbed(embededMessage)
                        .WithContent("@here");
                }
            }

            return new DiscordMessageBuilder()
                    .WithEmbed(embededMessage);
        }

        /// <summary>
        /// Generate a message for thanking the user about his report.
        /// </summary>
        /// <param name="reported">The reported message information.</param>
        /// <returns>Built message</returns>
        public DiscordMessageBuilder GenerateReportObject(ReportObject reported)
        {
            return new DiscordMessageBuilder()
                   .WithContent($"Thanks for reporting! I will let you know after a moderator processed your report. Your report:")
                   .WithEmbed(GenerateEmbedMessage(reported, new DiscordColor(255, 0, 255)));
        }

        /// <summary>
        /// Generate a message for thanking the user about his report.
        /// </summary>
        /// <param name="reported">The reported message information.</param>
        /// <returns>Built message</returns>
        public DiscordMessageBuilder GenerateReportFixedObject(ReportObject reported)
        {
            return new DiscordMessageBuilder()
                   .WithContent($"You edited your report (ID #{reported.Id}). Your new report:")
                   .WithEmbed(GenerateEmbedMessage(reported, new DiscordColor(255, 0, 255)));
        }

        /// <summary>
        /// Generate a message telling the user he already reported.
        /// </summary>
        /// <param name="reported">The reported message information.</param>
        /// <returns>Built message</returns>
        public DiscordMessageBuilder GenerateAlreadyReportObject(ReportObject reported)
        {
            return new DiscordMessageBuilder()
                   .WithContent($"You already reported content. Please wait for the moderation team to review your report. Your report:")
                   .WithEmbed(GenerateEmbedMessage(reported, new DiscordColor(255, 0, 255)));
        }

        /// <summary>
        /// Generate telling the reported message was already reviewed by a moderator.
        /// </summary>
        /// <param name="reported">The reported message information.</param>
        /// <returns>Built message</returns>
        public DiscordMessageBuilder GenerateReviewedMessage(ReportObject reported)
        {
            return new DiscordMessageBuilder()
                   .WithContent("The message you tried to report was already reviewed by a moderator:")
                   .WithEmbed(GenerateEmbedMessage(reported, new DiscordColor(255, 0, 255)));
        }

        /// <summary>
        /// Generate a message to tell that the report was validated.
        /// </summary>
        /// <param name="reported">The reported message information.</param>
        /// <returns>Built message</returns>
        public DiscordMessageBuilder GenerateReportValidatedMessage(ReportObject reported)
        {
            return new DiscordMessageBuilder()
                   .WithContent("The moderation team has reviewed your report and took appropriate actions. The reported content was found to be against the server rules, we thank you for your report. Your report:")
                   .WithEmbed(GenerateEmbedMessage(reported, new DiscordColor(255, 0, 0)));
        }

        /// <summary>
        /// Generate a message to tell that the report was rejected.
        /// </summary>
        /// <param name="reported">The reported message information.</param>
        /// <returns>Built message</returns>
        public DiscordMessageBuilder GenerateReportRejectedMessage(ReportObject reported)
        {
            return new DiscordMessageBuilder()
                   .WithContent(
                        "Following your report, the moderation team has estimated that the message is not against the server rules.\n"
                        + "If you think this is an error, please contact a moderator through the moderation mail. Your report:"
                    )
                   .WithEmbed(GenerateEmbedMessage(reported, new DiscordColor(0, 255, 0)));
        }

        /// <summary>
        /// Channel for all reports that were reviewed.
        /// </summary>
        public DiscordChannel reviewedReportsChannel { get; private set; }
        /// <summary>
        /// Channel for all reports that are currently pending.
        /// </summary>
        public DiscordChannel pendingReportsChannel { get; private set; }
        /// <summary>
        /// Default channel to use, currently only used for warnings.
        /// </summary>
        public DiscordChannel defaultChannel { get; private set; }

        /// <summary>
        /// List of valid report interactions.
        /// </summary>
        private List<ReportInteraction> reportInteractions;

        /// <summary>
        /// Report database.
        /// </summary>
        private ReportDatabase reportDatabase;
    }
}
