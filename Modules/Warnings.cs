using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicrosoftBot.Modules
{

    public enum ServerPermLevel { nothing, TrialMod, Mod, Admin, Owner = int.MaxValue }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RequireHomeserverPermAttribute : CheckBaseAttribute
    {
        public ServerPermLevel TargetLvl { get; set; }

        public RequireHomeserverPermAttribute(ServerPermLevel targetlvl)
        {
            TargetLvl = targetlvl;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            if (ctx.Guild.Id != Program.cfgjson.ServerID)
                return false;

            var level = Warnings.GetPermLevel(ctx.Member);
            if (level >= this.TargetLvl)
                return true;
            else
                if (!help)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.NoPermissions} Invalid permissions to use command **{ctx.Command.Name}**!");
                return false;
        }
    }

    public class HomeServerAttribute : CheckBaseAttribute
    {
        public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            return ctx.Guild.Id == Program.cfgjson.ServerID;
        }
    }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    public class Warnings : BaseCommandModule
    {

        public static ServerPermLevel GetPermLevel(DiscordMember target)
        {
            if (target.Guild.Id != Program.cfgjson.ServerID)
                return ServerPermLevel.nothing;

            DiscordRole TrialModRole = target.Guild.GetRole(Program.cfgjson.TrialModRole);
            DiscordRole ModRole = target.Guild.GetRole(Program.cfgjson.ModRole);
            DiscordRole Adminrole = target.Guild.GetRole(Program.cfgjson.AdminRole);

            if (target.IsOwner)
                return ServerPermLevel.Owner;
            else if (target.Roles.Contains(Adminrole))
                return ServerPermLevel.Admin;
            else if (target.Roles.Contains(ModRole))
                return ServerPermLevel.Mod;
            else if (target.Roles.Contains(TrialModRole))
                return ServerPermLevel.TrialMod;
            else
                return ServerPermLevel.nothing;
        }

        public async Task<DiscordEmbed> FancyWarnEmbedAsync(UserWarning warning, bool detailed = false, int colour = 0xFEC13D, bool showTime = true)
        {
            DiscordUser targetUser = await Program.discord.GetUserAsync(warning.TargetUserId);
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
            .WithDescription($"**Reason**\n{warning.WarnReason}")
            .WithColor(new DiscordColor(colour))
            .WithTimestamp(DateTime.Now)
            .WithFooter(
                $"User ID: {warning.TargetUserId}",
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
                embed.AddField("Time", detailed ? $"{warning.WarnTimestamp.ToUniversalTime()} UTC" : TimeToPrettyFormat((DateTime.Now - warning.WarnTimestamp)), true);

            return embed;
        }

        public static async Task<UserWarning> GiveWarningAsync(DiscordUser targetUser, DiscordUser modUser, string reason, string contextLink)
        {
            ulong warningId = (ulong)Program.db.StringGet("totalWarnings");
            // TODO: fix this hell
            if (warningId == 0)
            {
                Program.db.StringSet("totalWarnings", "1");
                warningId = 1;
            }
            else
            {
                warningId += 1;
            }

            UserWarning warning = new UserWarning()
            {
                TargetUserId = targetUser.Id,
                ModUserId = modUser.Id,
                WarnReason = reason,
                WarnTimestamp = DateTime.Now,
                WarningId = warningId,
                ContextLink = contextLink
            };
            Program.db.StringSet("totalWarnings", warningId);
            Program.db.HashSet(targetUser.Id.ToString(), warning.WarningId, JsonConvert.SerializeObject(warning));
            try {
                DiscordGuild guild = await Program.discord.GetGuildAsync(Program.cfgjson.ServerID);
                DiscordMember member = await guild.GetMemberAsync(targetUser.Id);
                await member.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} You were warned in **{guild.Name}**, reason: **{reason}**");
            } catch
            {
                // We failed to DM the user, this isn't important to note.
            }

            return warning;
        }

        public static bool EditWarning(DiscordUser targetUser, ulong warnId, DiscordUser modUser, string reason, string contextLink)
        {

            if (Program.db.HashExists(targetUser.Id.ToString(), warnId))
            {
                UserWarning oldWarn = GetWarning(targetUser.Id, warnId);
                UserWarning warning = new UserWarning()
                {
                    TargetUserId = targetUser.Id,
                    ModUserId = modUser.Id,
                    WarnReason = reason,
                    WarnTimestamp = oldWarn.WarnTimestamp,
                    WarningId = warnId,
                    ContextLink = contextLink
                };
                Program.db.StringSet("totalWarnings", warnId);
                Program.db.HashSet(targetUser.Id.ToString(), warning.WarningId, JsonConvert.SerializeObject(warning));
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool DelWarning(UserWarning warning)
        {
            if (Program.db.HashExists(warning.TargetUserId.ToString(), warning.WarningId))
            {
                Program.db.HashDelete(warning.TargetUserId.ToString(), warning.WarningId);
                return true;
            }
            else
            {
                return false;
            }
        }

        // https://stackoverflow.com/a/2776689
        public static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        public static string TimeToPrettyFormat(TimeSpan span)
        {

            if (span == TimeSpan.Zero) return "0 seconds";

            if (span.Days > 3649)
                return "A long time";

            var sb = new StringBuilder();
            if (span.Days > 365)
            {
                int years = (int)(span.Days / 365);
                sb.AppendFormat("{0} year{1}", years, years > 1 ? "s" : String.Empty);
                int remDays = (int)(span.Days - (365 * years));
                int months = remDays / 30;
                if (months > 0)
                    sb.AppendFormat(", {0} month{1}", months, months > 1 ? "s" : String.Empty);
                sb.AppendFormat(" ago");
            }
            else if (span.Days > 0)
                sb.AppendFormat("{0} day{1}", span.Days, span.Days > 1 ? "s" : String.Empty);
            else if (span.Hours > 0)
                sb.AppendFormat("{0} hour{1}", span.Hours, span.Hours > 1 ? "s" : String.Empty);
            else if (span.Minutes > 0)
                sb.AppendFormat("{0} minute{1}", span.Minutes, span.Minutes > 1 ? "s" : String.Empty);
            else
                sb.AppendFormat("{0} second{1}", span.Seconds, span.Seconds > 1 ? "s" : String.Empty);

            return sb.ToString();
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

        public string MessageLink(DiscordMessage msg)
        {
            return $"https://discord.com/channels/{msg.Channel.Guild.Id}/{msg.Channel.Id}/{msg.Id}";
        }

        [
            Command("warn"),
            Description("Issues a formal warning to a user."),
            Aliases("wam", "warm"),
            HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialMod)
        ]
        public async Task WarnCmd(
            CommandContext ctx,
            [Description("The user you are warning. Accepts many formats.")] DiscordUser targetUser,
            [RemainingText, Description("The reason for giving this warning.")] string reason = null
        )
        {
            await ctx.Message.DeleteAsync();
            if (reason == null)
            {
                await ctx.Member.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} Reason must be included for the warning command to work.");
                return;
            }
            DiscordMessage msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} {targetUser.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
            UserWarning warning = await GiveWarningAsync(targetUser, ctx.User, reason, MessageLink(msg));
            await Program.logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Warning} New warning for {targetUser.Mention}!", false, await FancyWarnEmbedAsync(warning, true, 0xFEC13D, false));

            // automute handling
            var warningsOutput = Program.db.HashGetAll(targetUser.Id.ToString()).ToDictionary(
                x => x.Name.ToString(),
                x => JsonConvert.DeserializeObject<UserWarning>(x.Value)
            );

            // Realistically this wouldn't ever be 0, but we'll set it below.
            int warnsSinceThreshold = 0;
            foreach (KeyValuePair<string, UserWarning> entry in warningsOutput)
            {
                UserWarning entryWarning = entry.Value;
                TimeSpan span = DateTime.Now - entryWarning.WarnTimestamp;
                if (span.Days <= Program.cfgjson.WarningDaysThreshold)
                    warnsSinceThreshold += 1;
            }

            int toMuteHours = 0;

            var keys = Program.cfgjson.AutoMuteThresholds.Keys.OrderBy(key => Convert.ToUInt64(key));
            int chosenKey = 0;
            foreach (string key in keys)
            {
                int keyInt = int.Parse(key);
                if (keyInt <= warnsSinceThreshold && keyInt > chosenKey)
                {
                    toMuteHours = Program.cfgjson.AutoMuteThresholds[key];
                    chosenKey = keyInt;
                }
            }

            if (toMuteHours > 0)
            {
                DiscordMember member = await ctx.Guild.GetMemberAsync(targetUser.Id);
                await Mutes.MuteUserAsync(member, TimeSpan.FromHours(toMuteHours), $"Automute after {warnsSinceThreshold} warnings in the past {Program.cfgjson.WarningDaysThreshold} hours.", ctx.User.Id, ctx.Guild, ctx.Channel );

            }
        }

        [
            Command("warnings"),
            Description("Shows a list of warnings that a user has been given. For more in-depth information, use the 'warnlookup' command."),
            Aliases("infractions", "warnfractions", "wammings", "wamfractions"),
            HomeServer
        ]
        public async Task WarningCmd(
            CommandContext ctx,
            [Description("The user you want to lookup warnings for. Accepts many formats.")] DiscordUser targetUser = null
        )
        {
            if (targetUser == null)
                targetUser = ctx.User;
            
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
                if (count == 30)
                {
                    str += $"+ {keys.Count() - 30} more…";
                    count += 1;
                }
                else if (count < 30)
                {
                    var reason = warning.WarnReason.Replace("`", "\\`").Replace("*", "\\*");
                    if (reason.Length > 29)
                    {
                        reason = Truncate(reason, 29) + "…";
                    }
                    str += $"`{Pad(warning.WarningId)}` **{reason}** • {TimeToPrettyFormat(span)} ago\n";
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
                )
                .AddField("Last 30 days", recentCount.ToString(), true)
                .AddField("Total", keys.Count().ToString(), true);

            await ctx.RespondAsync(null, false, embed);
        }

        [
            Command("delwarn"),
            Description("Delete a warning that was issued by mistake or later became invalid.\n" +
            "You can only delete warnings issued by you, unless you are an Admin/Lead Moderator."),
            Aliases("delwarm", "delwam"),
            HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod)
        ]
        public async Task DelwarnCmd(
            CommandContext ctx,
            [Description("The user you're removing a warn from. Accepts many formats.")] DiscordUser targetUser,
            [Description("The ID for the warning you want to delete.")] ulong warnId
        )
        {
            UserWarning warning = GetWarning(targetUser.Id, warnId);
            if (warning == null)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else if (GetPermLevel(ctx.Member) < ServerPermLevel.Admin && warning.ModUserId != ctx.User.Id)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.NoPermissions} You can only delete warnings that were issued by you!");
            else
            {
                DelWarning(warning);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Deleted} Successfully deleted warning `{Pad(warnId)}` (belonging to {targetUser.Mention})");

                await Program.logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Deleted} Warning deleted:" +
                    $"`{Pad(warnId)}` (belonging to {targetUser.Mention}, deleted by {ctx.Member.Username}#{ctx.Member.Discriminator})", false, await FancyWarnEmbedAsync(warning, true, 0xf03916));
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
                await ctx.RespondAsync(null, false, await FancyWarnEmbedAsync(warning));
        }

        [
            Command("warndetails"),
            Description("Check the details of a warning in depth. Shows extra information (Such as responsible Mod) that may not be wanted to be public."),
            HomeServer,
            RequireHomeserverPerm(ServerPermLevel.TrialMod)
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
                await ctx.RespondAsync(null, false, await FancyWarnEmbedAsync(warning, true));

        }

        [
            Command("editwarn"),
            Description("Edit the reason of an existing warning. Can only be used on warnings issued by you, unless you are an Admin/Lead Moderator.\n" +
                "The Moderator who is editing the reason will become responsible for the case."),
            HomeServer,
            RequireHomeserverPerm(ServerPermLevel.Mod)
        ]
        public async Task EditwarnCmd(
            CommandContext ctx,
            [Description("The user you're editing a warning of. Accept many formats.")] DiscordUser targetUser,
            [Description("The ID of the warning you want to edit.")] ulong warnId,
            [RemainingText, Description("The new reason for the warning.")] string newReason)
        {
            var msg = await ctx.RespondAsync("Processing your request...");
            var warning = GetWarning(targetUser.Id, warnId);
            if (warning == null)
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} I couldn't find a warning for that user with that ID! Please check again.");
            else if (GetPermLevel(ctx.Member) < ServerPermLevel.Admin && warning.ModUserId != ctx.User.Id)
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.NoPermissions} You can only edit warnings that were issued by you!");
            else
            {
                EditWarning(targetUser, warnId, ctx.User, newReason, MessageLink(msg));
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Information} Successfully edited warning `{Pad(warnId)}` (belonging to {targetUser.Mention})",
                    await FancyWarnEmbedAsync(GetWarning(targetUser.Id, warnId)));
                await Program.logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Information} Warning edited:" +
                    $"`{Pad(warnId)}` (belonging to {targetUser.Mention})", false, await FancyWarnEmbedAsync(GetWarning(targetUser.Id, warnId), true));
            }
        }

    }
}
