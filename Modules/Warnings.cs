using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cliptok.Modules
{

    public enum ServerPermLevel
    {
        Muted = -1,
        Nothing = 0,
        Tier1,
        Tier2,
        Tier3,
        Tier4,
        Tier5,
        Tier6,
        Tier7,
        Tier8,
        TierS,
        TierX,
        TrialModerator,
        Moderator,
        Admin,
        Owner = int.MaxValue
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RequireHomeserverPermAttribute : CheckBaseAttribute
    {
        public ServerPermLevel TargetLvl { get; set; }
        public bool WorkOutside { get; set; }

        public RequireHomeserverPermAttribute(ServerPermLevel targetlvl, bool workOutside = false)
        {
            WorkOutside = workOutside;
            TargetLvl = targetlvl;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            // If the command is supposed to stay within the server and its being used outside, fail silently
            if (!WorkOutside && (ctx.Channel.IsPrivate || ctx.Guild.Id != Program.cfgjson.ServerID))
                return false;

            DiscordMember member;
            if (ctx.Channel.IsPrivate || ctx.Guild.Id != Program.cfgjson.ServerID)
            {
                var guild = await ctx.Client.GetGuildAsync(Program.cfgjson.ServerID);
                try
                {
                    member = await guild.GetMemberAsync(ctx.User.Id);
                }
                catch (DSharpPlus.Exceptions.NotFoundException)
                {
                    return false;
                }
            }
            else
            {
                member = ctx.Member;
            }

            var level = Warnings.GetPermLevel(member);
            if (level >= TargetLvl)
                return true;

            else if (!help && ctx.Command.QualifiedName != "edit")
            {
                var levelText = level.ToString();
                if (level == ServerPermLevel.Nothing && Program.rand.Next(1, 100) == 69)
                    levelText = $"naught but a thing, my dear human. Congratulations, you win {Program.rand.Next(1, 10)} bonus points.";

                await ctx.RespondAsync(
                    $"{Program.cfgjson.Emoji.NoPermissions} Invalid permissions to use command **{ctx.Command.Name}**!\n" +
                    $"Required: `{TargetLvl}`\nYou have: `{levelText}`");
            }
            return false;
        }
    }

    public class HomeServerAttribute : CheckBaseAttribute
    {
        public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            return !ctx.Channel.IsPrivate && ctx.Guild.Id == Program.cfgjson.ServerID;
        }
    }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    public class SlashRequireHomeserverPermAttribute : SlashCheckBaseAttribute
    {
        public ServerPermLevel TargetLvl;

        public SlashRequireHomeserverPermAttribute(ServerPermLevel targetlvl)
            => TargetLvl = targetlvl;

        public override async Task<bool> ExecuteChecksAsync(InteractionContext ctx)
        {
            if (ctx.Guild.Id != Program.cfgjson.ServerID)
                return false;

            var level = Warnings.GetPermLevel(ctx.Member);
            if (level >= this.TargetLvl)
                return true;
            else
                return false;
        }
    }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    public class Warnings : BaseCommandModule
    {

        public static DiscordRole GetRole(DiscordGuild guild, ulong roleID)
        {
            return guild.GetRole(roleID);
        }

        public static ServerPermLevel GetPermLevel(DiscordMember target)
        {
            if (target.Guild.Id != Program.cfgjson.ServerID)
                return ServerPermLevel.Nothing;

            // Torch approved of this.
            if (target.IsOwner)
                return ServerPermLevel.Owner;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.AdminRole)))
                return ServerPermLevel.Admin;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.ModRole)))
                return ServerPermLevel.Moderator;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.MutedRole)))
                return ServerPermLevel.Muted;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TrialModRole)))
                return ServerPermLevel.TrialModerator;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[9])))
                return ServerPermLevel.TierX;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[8])))
                return ServerPermLevel.TierS;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[7])))
                return ServerPermLevel.Tier8;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[6])))
                return ServerPermLevel.Tier7;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[5])))
                return ServerPermLevel.Tier6;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[4])))
                return ServerPermLevel.Tier5;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[3])))
                return ServerPermLevel.Tier4;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[2])))
                return ServerPermLevel.Tier3;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[1])))
                return ServerPermLevel.Tier2;
            else if (target.Roles.Contains(target.Guild.GetRole(Program.cfgjson.TierRoles[0])))
                return ServerPermLevel.Tier1;
            else
                return ServerPermLevel.Nothing;
        }

        internal static string MessageLink(Task<DiscordMessage> msg)
        {
            throw new NotImplementedException();
        }

        public static async Task<DiscordEmbed> FancyWarnEmbedAsync(UserWarning warning, bool detailed = false, int colour = 0xFEC13D, bool showTime = true, ulong userID = default)
        {
            if (userID == default)
                userID = warning.TargetUserId;

            string reason = warning.WarnReason;
            if (string.IsNullOrWhiteSpace(reason))
                reason = "No reason provided.";

            DiscordUser targetUser = await Program.discord.GetUserAsync(userID);
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
            .WithDescription($"**Reason**\n{reason}")
            .WithColor(new DiscordColor(colour))
            .WithTimestamp(DateTime.Now)
            .WithFooter(
                $"User ID: {userID}",
                null
            )
            .WithAuthor(
                $"Warning for {targetUser.Username}#{targetUser.Discriminator}",
                null,
                targetUser.AvatarUrl
            )
            .AddField("Warning ID", Pad(warning.WarningId), true);
            if (detailed)
            {
                embed.AddField("Responsible moderator", $"<@{warning.ModUserId}>")
                .AddField("Message link", warning.ContextLink == null ? "N/A" : $"[`Jump to warning`]({warning.ContextLink})");
            }
            if (showTime)
                embed.AddField("Time", detailed ? $"<t:{ModCmds.ToUnixTimestamp(warning.WarnTimestamp)}:f>" : $"<t:{ModCmds.ToUnixTimestamp(warning.WarnTimestamp)}:R>", true);

            return embed;
        }

        public static async Task<UserWarning> GiveWarningAsync(DiscordUser targetUser, DiscordUser modUser, string reason, string contextLink, DiscordChannel channel, string extraWord = " ")
        {
            DiscordGuild guild = channel.Guild;
            ulong warningId = (ulong)Program.db.StringIncrement("totalWarnings");

            UserWarning warning = new()
            {
                TargetUserId = targetUser.Id,
                ModUserId = modUser.Id,
                WarnReason = reason,
                WarnTimestamp = DateTime.Now,
                WarningId = warningId,
                ContextLink = contextLink
            };

            Program.db.HashSet(targetUser.Id.ToString(), warning.WarningId, JsonConvert.SerializeObject(warning));
            try
            {
                DiscordMember member = await guild.GetMemberAsync(targetUser.Id);
                await member.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} You were{extraWord}warned in **{guild.Name}**, reason: **{reason}**");
            }
            catch
            {
                // We failed to DM the user, this isn't important to note.
            }

            await Program.logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} New warning for {targetUser.Mention}!", await FancyWarnEmbedAsync(warning, true, 0xFEC13D, false, targetUser.Id));

            // automute handling
            var warningsOutput = Program.db.HashGetAll(targetUser.Id.ToString()).ToDictionary(
                x => x.Name.ToString(),
                x => JsonConvert.DeserializeObject<UserWarning>(x.Value)
            );

            var autoMuteResult = GetHoursToMuteFor(warningDictionary: warningsOutput, timeToCheck: TimeSpan.FromDays(Program.cfgjson.WarningDaysThreshold), autoMuteThresholds: Program.cfgjson.AutoMuteThresholds);

            var acceptedThreshold = Program.cfgjson.WarningDaysThreshold;
            int toMuteHours = autoMuteResult.MuteHours;
            int warnsSinceThreshold = autoMuteResult.WarnsSinceThreshold;
            string thresholdSpan = "days";

            if (toMuteHours != -1 && Program.cfgjson.RecentWarningsPeriodHours != 0)
            {
                var recentAutoMuteResult = GetHoursToMuteFor(warningDictionary: warningsOutput, timeToCheck: TimeSpan.FromHours(Program.cfgjson.RecentWarningsPeriodHours), autoMuteThresholds: Program.cfgjson.RecentWarningsAutoMuteThresholds);
                if (recentAutoMuteResult.MuteHours == -1 || recentAutoMuteResult.MuteHours >= toMuteHours)
                {
                    toMuteHours = recentAutoMuteResult.MuteHours;
                    warnsSinceThreshold = recentAutoMuteResult.WarnsSinceThreshold;
                    thresholdSpan = "hours";
                    acceptedThreshold = Program.cfgjson.RecentWarningsPeriodHours;
                }
            }

            if (toMuteHours > 0)
            {
                await Mutes.MuteUserAsync(targetUser, $"Automatic mute after {warnsSinceThreshold} warnings in the past {acceptedThreshold} {thresholdSpan}.", modUser.Id, guild, channel, TimeSpan.FromHours(toMuteHours));
            }
            else if (toMuteHours <= -1)
            {
                await Mutes.MuteUserAsync(targetUser, $"Automatic permanent mute after {warnsSinceThreshold} warnings in the past {acceptedThreshold} {thresholdSpan}.", modUser.Id, guild, channel);
            }
            return warning;
        }

        public static (int MuteHours, int WarnsSinceThreshold) GetHoursToMuteFor(Dictionary<string, UserWarning> warningDictionary, TimeSpan timeToCheck, Dictionary<string, int> autoMuteThresholds)
        {
            // Realistically this wouldn't ever be 0, but we'll set it below.
            int warnsSinceThreshold = 0;
            foreach (KeyValuePair<string, UserWarning> entry in warningDictionary)
            {
                UserWarning entryWarning = entry.Value;
                TimeSpan span = DateTime.Now - entryWarning.WarnTimestamp;
                if (span <= timeToCheck)
                    warnsSinceThreshold += 1;
            }

            int toMuteHours = 0;

            var keys = autoMuteThresholds.Keys.OrderBy(key => Convert.ToUInt64(key));
            int chosenKey = 0;
            foreach (string key in keys)
            {
                int keyInt = int.Parse(key);
                if (keyInt <= warnsSinceThreshold && keyInt > chosenKey)
                {
                    toMuteHours = autoMuteThresholds[key];
                    chosenKey = keyInt;
                }
            }

            return (toMuteHours, warnsSinceThreshold);
        }

        public static bool EditWarning(DiscordUser targetUser, ulong warnId, DiscordUser modUser, string reason)
        {

            if (Program.db.HashExists(targetUser.Id.ToString(), warnId))
            {
                UserWarning oldWarn = GetWarning(targetUser.Id, warnId);
                UserWarning warning = new()
                {
                    TargetUserId = targetUser.Id,
                    ModUserId = modUser.Id,
                    WarnReason = reason,
                    WarnTimestamp = oldWarn.WarnTimestamp,
                    WarningId = warnId,
                    ContextLink = oldWarn.ContextLink
                };
                Program.db.HashSet(targetUser.Id.ToString(), warning.WarningId, JsonConvert.SerializeObject(warning));
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool DelWarning(UserWarning warning, ulong userID = default)
        {
            if (userID == default)
                userID = warning.TargetUserId;

            if (Program.db.HashExists(userID.ToString(), warning.WarningId))
            {
                Program.db.HashDelete(userID.ToString(), warning.WarningId);
                return true;
            }
            else
            {
                return false;
            }
        }

        // https://stackoverflow.com/a/2776689
        public static string Truncate(string value, int maxLength, bool elipsis = false)
        {
            if (string.IsNullOrEmpty(value)) return value;
            var strOut = value.Length <= maxLength ? value : value.Substring(0, maxLength);
            if (elipsis && value.Length > maxLength)
                return strOut + '…';
            else
                return strOut;
        }

        public static string TimeToPrettyFormat(TimeSpan span, bool ago = true)
        {

            if (span == TimeSpan.Zero) return "0 seconds";

            if (span.Days > 3649)
                return "a long time";

            var sb = new StringBuilder();
            if (span.Days > 365)
            {
                int years = (int)(span.Days / 365);
                sb.AppendFormat("{0} year{1}", years, years > 1 ? "s" : String.Empty);
                int remDays = (int)(span.Days - (365 * years));
                int months = remDays / 30;
                if (months > 0)
                    sb.AppendFormat(", {0} month{1}", months, months > 1 ? "s" : String.Empty);
                // sb.AppendFormat(" ago");
            }
            else if (span.Days > 0)
                sb.AppendFormat("{0} day{1}", span.Days, span.Days > 1 ? "s" : String.Empty);
            else if (span.Hours > 0)
                sb.AppendFormat("{0} hour{1}", span.Hours, span.Hours > 1 ? "s" : String.Empty);
            else if (span.Minutes > 0)
                sb.AppendFormat("{0} minute{1}", span.Minutes, span.Minutes > 1 ? "s" : String.Empty);
            else
                sb.AppendFormat("{0} second{1}", span.Seconds, (span.Seconds > 1 || span.Seconds == 0) ? "s" : String.Empty);

            string output = sb.ToString();
            if (ago)
                output += " ago";
            return output;
        }

        public static string Pad(ulong id)
        {
            return id.ToString("D5");
        }

        public static UserWarning GetWarning(ulong targetUserId, ulong warnId)
        {
            try
            {
                return JsonConvert.DeserializeObject<UserWarning>(Program.db.HashGet(targetUserId.ToString(), warnId));
            }
            catch (System.ArgumentNullException)
            {
                return null;
            }
        }

        public static string MessageLink(DiscordMessage msg)
        {
            return $"https://discord.com/channels/{msg.Channel.Guild.Id}/{msg.Channel.Id}/{msg.Id}";
        }

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
                if (Warnings.GetPermLevel(ctx.Member) == ServerPermLevel.TrialModerator && (Warnings.GetPermLevel(targetMember) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
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
            if (reason == null)
            {
                await ctx.Member.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} Reason must be included for the warning command to work.");
                return;
            }

            var messageBuild = new DiscordMessageBuilder()
                .WithContent($"{Program.cfgjson.Emoji.Warning} {targetUser.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");

            if (reply != null)
                messageBuild.WithReply(reply.Id, true, false);

            var tmp = ctx.Channel.Type;

            var msg = await ctx.Channel.SendMessageAsync(messageBuild);
            _ = await GiveWarningAsync(targetUser, ctx.User, reason, MessageLink(msg), ctx.Channel);
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
                if (GetPermLevel(ctx.Member) == ServerPermLevel.TrialModerator && (Warnings.GetPermLevel(targetMember) >= ServerPermLevel.TrialModerator || targetMember.IsBot))
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
            if (reason == null)
            {
                await ctx.Member.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} Reason must be included for the warning command to work.");
                return;
            }
            DiscordMessage msg = await targetChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} {targetUser.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
            await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} {targetUser.Mention} was warned in {targetChannel.Mention}: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
            _ = await GiveWarningAsync(targetUser, ctx.User, reason, MessageLink(msg), ctx.Channel);
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
            if (targetUser == null)
                targetUser = ctx.User;

            await ctx.RespondAsync(null, GenerateWarningsEmbed(targetUser));
        }

        public static DiscordEmbed GenerateWarningsEmbed(DiscordUser targetUser)
        {
            var warningsOutput = Program.db.HashGetAll(targetUser.Id.ToString()).ToDictionary(
                x => x.Name.ToString(),
                x => JsonConvert.DeserializeObject<UserWarning>(x.Value)
            );

            var keys = warningsOutput.Keys.OrderByDescending(warn => Convert.ToUInt64(warn));
            string str = "";
            int count = 1;
            int recentCount = 0;

            foreach (string key in keys)
            {
                UserWarning warning = warningsOutput[key];
                TimeSpan span = DateTime.Now - warning.WarnTimestamp;
                if (span.Days < 31)
                {
                    recentCount += 1;
                }
                if (count == 66)
                {
                    str += $"+ {keys.Count() - 30} more…";
                    count += 1;
                }
                else if (count < 66)
                {
                    var reason = warning.WarnReason;
                    if (string.IsNullOrWhiteSpace(reason))
                    {
                        reason = "No reason provided.";
                    }
                    reason = reason.Replace("`", "\\`").Replace("*", "\\*");

                    if (reason.Length > 29)
                    {
                        reason = Truncate(reason, 29) + "…";
                    }
                    str += $"`{Pad(warning.WarningId)}` **{reason}** • <t:{ModCmds.ToUnixTimestamp(warning.WarnTimestamp)}:R>\n";
                    count += 1;
                }

            }

            var embed = new DiscordEmbedBuilder()
                .WithDescription(str)
                .WithColor(new DiscordColor(0xFEC13D))
                .WithTimestamp(DateTime.Now)
                .WithFooter(
                    $"User ID: {targetUser.Id}",
                    null
                )
                .WithAuthor(
                    $"Warnings for {targetUser.Username}#{targetUser.Discriminator}",
                    null,
                    targetUser.AvatarUrl
                );

            if (Program.cfgjson.RecentWarningsPeriodHours != 0)
            {
                var hourRecentMatches = keys.Where(key =>
                {
                    TimeSpan span = DateTime.Now - warningsOutput[key].WarnTimestamp;
                    return (span.TotalHours < Program.cfgjson.RecentWarningsPeriodHours);
                }
                );

                embed.AddField($"Last {Program.cfgjson.RecentWarningsPeriodHours} hours", hourRecentMatches.Count().ToString(), true);
            }

            embed.AddField("Last 30 days", recentCount.ToString(), true)
                .AddField("Total", keys.Count().ToString(), true);

            return embed;
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
            [Description("The ID of the warning you want to delete.")] ulong warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);
            if (warning == null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else if (GetPermLevel(ctx.Member) == ServerPermLevel.TrialModerator && warning.ModUserId != ctx.User.Id && warning.ModUserId != ctx.Client.CurrentUser.Id)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot edit or delete warnings that aren't issued by you or the bot!");
            }
            else
            {
                bool success = DelWarning(warning, targetUser.Id);
                if (success)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Deleted} Successfully deleted warning `{Pad(warnId)}` (belonging to {targetUser.Mention})");

                    await Program.logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Deleted} Warning deleted:" +
                        $"`{Pad(warnId)}` (belonging to {targetUser.Mention}, deleted by {ctx.Member.Username}#{ctx.Member.Discriminator})", await FancyWarnEmbedAsync(warning, true, 0xf03916, true, targetUser.Id));
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to delete warning `{Pad(warnId)}` from {targetUser.Mention}!\nPlease contact the bot author.");
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
            [Description("The ID of the warning you want to see")] ulong warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);
            if (warning == null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else
                await ctx.RespondAsync(null, await FancyWarnEmbedAsync(warning, userID: targetUser.Id));
        }

        [
            Command("warndetails"),
            Description("Check the details of a warning in depth. Shows extra information (Such as responsible Mod) that may not be wanted to be public."),
            HomeServer,
            RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task WarnDetailsCmd(
            CommandContext ctx,
            [Description("The user you're looking up detailed warn information for. Accepts many formats.")] DiscordUser targetUser,
            [Description("The ID of the warning you're looking at in detail.")] ulong warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);

            if (warning == null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else
                await ctx.RespondAsync(null, await FancyWarnEmbedAsync(warning, true, userID: targetUser.Id));

        }

        [
            Command("editwarn"),
            Description("Edit the reason of an existing warning.\n" +
                "The Moderator who is editing the reason will become responsible for the case."),
            HomeServer,
            RequireHomeserverPerm(ServerPermLevel.TrialModerator)
        ]
        public async Task EditwarnCmd(
            CommandContext ctx,
            [Description("The user you're editing a warning for. Accepts many formats.")] DiscordUser targetUser,
            [Description("The ID of the warning you want to edit.")] ulong warnId,
            [RemainingText, Description("The new reason for the warning.")] string newReason)
        {
            if (string.IsNullOrWhiteSpace(newReason))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You haven't given a new reason to set for the warning!");
                return;
            }

            var msg = await ctx.RespondAsync("Processing your request...");
            var warning = GetWarning(targetUser.Id, warnId);
            if (warning == null)
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else if (GetPermLevel(ctx.Member) == ServerPermLevel.TrialModerator && warning.ModUserId != ctx.User.Id && warning.ModUserId != ctx.Client.CurrentUser.Id)
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, as a Trial Moderator you cannot edit or delete warnings that aren't issued by you or the bot!");
            }
            else
            {
                EditWarning(targetUser, warnId, ctx.User, newReason);
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Information} Successfully edited warning `{Pad(warnId)}` (belonging to {targetUser.Mention})",
                    await FancyWarnEmbedAsync(GetWarning(targetUser.Id, warnId), userID: targetUser.Id));
                await Program.logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Information} Warning edited:" +
                    $"`{Pad(warnId)}` (belonging to {targetUser.Mention})", await FancyWarnEmbedAsync(GetWarning(targetUser.Id, warnId), true, userID: targetUser.Id));
            }
        }

        [Command("mostwarnings"), Description("Who has the most warnings???")]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task MostWarningsCmd(CommandContext ctx)
        {
            try
            {
                await ctx.TriggerTypingAsync();
            }
            catch
            {
                // typing failing is unimportant, move on
            }

            var server = Program.redis.GetServer(Program.redis.GetEndPoints()[0]);
            var keys = server.Keys();

            Dictionary<string, int> counts = new();
            foreach (var key in keys)
            {
                ulong number;
                if (ulong.TryParse(key.ToString(), out number))
                {
                    counts[key.ToString()] = Program.db.HashGetAll(key).Count();
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
    }
}
