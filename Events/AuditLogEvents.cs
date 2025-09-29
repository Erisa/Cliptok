using DSharpPlus.Entities.AuditLogs;

namespace Cliptok.Events
{
    public class AuditLogEvents
    {
        public static async Task GuildAuditLogCreated(DiscordClient client, GuildAuditLogCreatedEventArgs e)
        {
            // If we end up in a random guild somehow, ignore it
            if (e.Guild.Id != Program.cfgjson.ServerID)
                return;

            var entry = e.AuditLogEntry;

            if (entry is null)
                return;

            // Log member role add/remove to user log
            if (entry.ActionType is DiscordAuditLogActionType.MemberRoleUpdate)
            {
                var memberUpdateEntry = entry as DiscordAuditLogMemberUpdateEntry;
                var roleUpdateType = memberUpdateEntry.ActionCategory;
                var member = memberUpdateEntry.Target;

                if (memberUpdateEntry.AddedRoles is not null)
                {
                    // role(s) added
                    var addedRoles = memberUpdateEntry.AddedRoles;
                    await LogChannelHelper.LogMessageAsync("users", new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.UserUpdate} **Member role{(addedRoles.Count > 1 ? "s" : "")} added!** - {member.Mention}")
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithColor(new DiscordColor(0x3E9D28))
                            .WithTimestamp(DateTimeOffset.Now)
                            .WithThumbnail(member.AvatarUrl)
                            .WithAuthor(
                                name: $"{DiscordHelpers.UniqueUsername(member)} was given {(addedRoles.Count > 1 ? "roles" : "a role")}",
                                iconUrl: member.AvatarUrl
                            )
                            .AddField($"Role{(addedRoles.Count > 1 ? "s" : "")} added", String.Join(", ", addedRoles.Select(x => x.Name)), false)
                            .WithFooter($"User ID: {member.Id}\n{client.CurrentUser.Username}RoleAddEvent")));
                }

                if (memberUpdateEntry.RemovedRoles is not null)
                {
                    // role(s) removed
                    var removedRoles = memberUpdateEntry.RemovedRoles;
                    await LogChannelHelper.LogMessageAsync("users", new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.UserUpdate} **Member role{(removedRoles.Count > 1 ? "s" : "")} removed!** - {member.Mention}")
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithColor(new DiscordColor(0x3E9D28))
                            .WithTimestamp(DateTimeOffset.Now)
                            .WithThumbnail(member.AvatarUrl)
                            .WithAuthor(
                                name: $"{DiscordHelpers.UniqueUsername(member)} was removed from {(removedRoles.Count > 1 ? "roles" : "a role")}",
                                iconUrl: member.AvatarUrl
                            )
                            .AddField($"Role{(removedRoles.Count > 1 ? "s" : "")} removed", String.Join(", ", removedRoles.Select(x => x.Name)), false)
                            .WithFooter($"User ID: {member.Id}\n{client.CurrentUser.Username}RoleRemoveEvent")));
                }
            }
            // Log member nickname changes to user log
            else if (entry.ActionType is DiscordAuditLogActionType.MemberUpdate)
            {
                var memberUpdateEntry = entry as DiscordAuditLogMemberUpdateEntry;
                var member = memberUpdateEntry.Target;

                // Ignore member update events that aren't nickname changes
                if (memberUpdateEntry.NicknameChange.Before is null && memberUpdateEntry.NicknameChange.After is null)
                    return;

                await LogChannelHelper.LogMessageAsync("users", new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.UserUpdate} **Member nickname changed!** - {member.Mention}")
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithColor(new DiscordColor(0x3E9D28))
                        .WithTimestamp(DateTimeOffset.Now)
                        .WithThumbnail(member.AvatarUrl)
                        .WithAuthor(
                            name: $"{DiscordHelpers.UniqueUsername(member)}'s nickname was changed",
                            iconUrl: member.AvatarUrl
                        )
                        .AddField("Before", memberUpdateEntry.NicknameChange.Before ?? "[no nickname]")
                        .AddField("After", memberUpdateEntry.NicknameChange.After ?? "[no nickname]")
                        .WithFooter($"User ID: {member.Id}\n{client.CurrentUser.Username}NicknameChangeEvent")));
            }
            // Log kicks to mod log
            else if (entry.ActionType is DiscordAuditLogActionType.Kick)
            {
                var kickEntry = entry as DiscordAuditLogKickEntry;
                var member = kickEntry.Target;

                // Ignore kicks performed by the bot. These are already logged
                if (kickEntry.UserResponsible.Id == client.CurrentUser.Id)
                    return;

                await LogChannelHelper.LogMessageAsync("mod", $"{Program.cfgjson.Emoji.Ejected} {member.Mention} was kicked by {kickEntry.UserResponsible.Mention}.\nReason: **{kickEntry.Reason}**");
            }
            // Log bans to mod log
            else if (entry.ActionType is DiscordAuditLogActionType.Ban)
            {
                var banEntry = entry as DiscordAuditLogBanEntry;
                var member = banEntry.Target;

                // Ignore bans performed by the bot. These are already logged
                if (banEntry.UserResponsible.Id == client.CurrentUser.Id)
                    return;

                await LogChannelHelper.LogMessageAsync("mod", $"{Program.cfgjson.Emoji.Banned} {member.Mention} was banned by {banEntry.UserResponsible.Mention}.\nReason: **{banEntry.Reason ?? "No reason specified."}**");
            }
            // Log unbans to mod log
            else if (entry.ActionType is DiscordAuditLogActionType.Unban)
            {
                var unbanEntry = entry as DiscordAuditLogBanEntry;
                var member = unbanEntry.Target;

                // Ignore unbans performed by the bot. These are already logged
                if (unbanEntry.UserResponsible.Id == client.CurrentUser.Id)
                    return;

                await LogChannelHelper.LogMessageAsync("mod", $"{Program.cfgjson.Emoji.Unbanned} {member.Mention} was unbanned by {unbanEntry.UserResponsible.Mention}!\nReason: **{unbanEntry.Reason ?? "No reason specified."}**");
            }
        }
    }
}