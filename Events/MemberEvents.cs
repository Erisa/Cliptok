using static Cliptok.Program;

namespace Cliptok.Events
{
    public class MemberEvents
    {
        public static async Task GuildMemberAdded(DiscordClient client, GuildMemberAddEventArgs e)
        {
            Task.Run(async () =>
            {
                if (e.Guild.Id != cfgjson.ServerID)
                    return;

                var embed = new DiscordEmbedBuilder()
                   .WithColor(new DiscordColor(0x3E9D28))
                   .WithTimestamp(DateTimeOffset.Now)
                   .WithThumbnail(e.Member.AvatarUrl)
                   .WithAuthor(
                       name: $"{e.Member.Username}#{e.Member.Discriminator} has joined",
                       iconUrl: e.Member.AvatarUrl
                    )
                   .AddField("User", e.Member.Mention, false)
                   .AddField("User ID", e.Member.Id.ToString(), false)
                   .AddField("Action", "Joined the server", false)
                   .WithFooter($"{client.CurrentUser.Username}JoinEvent");

                LogChannelHelper.LogMessageAsync("users", $"{cfgjson.Emoji.UserJoin} **Member joined the server!** - {e.Member.Id}", embed);

                var joinWatchlist = await db.ListRangeAsync("joinWatchedUsers");

                if (joinWatchlist.Contains(e.Member.Id))
                {
                    LogChannelHelper.LogMessageAsync("investigations", $"{cfgjson.Emoji.Warning} Watched user {e.Member.Mention} just joined the server!", embed);
                }

                if (db.HashExists("raidmode", e.Guild.Id))
                {
                    if (!db.KeyExists("raidmode-accountage") || (TimeHelpers.ToUnixTimestamp(e.Member.CreationTimestamp.DateTime) > (long)db.StringGet("raidmode-accountage")))
                    {
                        try
                        {
                            await e.Member.SendMessageAsync($"Hi, you tried to join **{e.Guild.Name}** while it was in lockdown and your join was refused.\nPlease try to join again later.");
                        }
                        catch (DSharpPlus.Exceptions.UnauthorizedException)
                        {
                            // welp, their DMs are closed. not my problem.
                        }
                        await e.Member.RemoveAsync(reason: "Raidmode is enabled, join was rejected.");
                    }
                }

                if (await db.HashExistsAsync("mutes", e.Member.Id))
                {
                    // todo: store per-guild
                    DiscordRole mutedRole = e.Guild.GetRole(cfgjson.MutedRole);
                    await e.Member.GrantRoleAsync(mutedRole, "Reapplying mute on join: possible mute evasion.");
                }
                else if (e.Member.CommunicationDisabledUntil != null)
                {
                    await e.Member.TimeoutAsync(null, "Removing timeout since member was presumably unmuted while left");
                }

                if (db.HashExists("unbanned", e.Member.Id))
                    return;

                if (avatars.Contains(e.Member.AvatarHash))
                {
                    var _ = BanHelpers.BanSilently(e.Guild, e.Member.Id, "Secret sauce");
                    await LogChannelHelper.LogMessageAsync("investigations", $"{cfgjson.Emoji.Banned} Raid-banned {e.Member.Mention} for matching avatar: {e.Member.AvatarUrl.Replace("1024", "128")}");
                }

                string banDM = $"You have been automatically banned from **{e.Guild.Name}** for matching patterns of known raiders.\n" +
                        $"Please send an appeal and you will be unbanned as soon as possible: {cfgjson.AppealLink}\n" +
                        $"The requirements for appeal can be ignored in this case. Sorry for any inconvenience caused.";

                foreach (var IdAutoBanSet in cfgjson.AutoBanIds)
                {
                    if (db.HashExists(IdAutoBanSet.Name, e.Member.Id))
                    {
                        return;
                    }

                    if (e.Member.Id > IdAutoBanSet.LowerBound && e.Member.Id < IdAutoBanSet.UpperBound)
                    {
                        await e.Member.SendMessageAsync(banDM);

                        await e.Member.BanAsync(7, "Matching patterns of known raiders, please unban if appealed.");

                        await LogChannelHelper.LogMessageAsync("investigations", $"{cfgjson.Emoji.Banned} Automatically appeal-banned {e.Member.Mention} for matching the creation date of the {IdAutoBanSet.Name} DM scam raiders.");
                    }

                    db.HashSet(IdAutoBanSet.Name, e.Member.Id, true);
                }

            });
        }

        public static async Task GuildMemberRemoved(DiscordClient client, GuildMemberRemoveEventArgs e)
        {
            Task.Run(async () =>
            {
                if (e.Guild.Id != cfgjson.ServerID)
                    return;

                var muteRole = e.Guild.GetRole(cfgjson.MutedRole);
                var userMute = await db.HashGetAsync("mutes", e.Member.Id);

                if (!userMute.IsNull && !e.Member.Roles.Contains(muteRole))
                    db.HashDeleteAsync("mutes", e.Member.Id);

                if (e.Member.Roles.Contains(muteRole) && userMute.IsNull)
                {
                    MemberPunishment newMute = new()
                    {
                        MemberId = e.Member.Id,
                        ModId = discord.CurrentUser.Id,
                        ServerId = e.Guild.Id,
                        ExpireTime = null,
                        ActionTime = DateTime.Now
                    };

                    db.HashSetAsync("mutes", e.Member.Id, JsonConvert.SerializeObject(newMute));
                }

                if (!userMute.IsNull && !e.Member.Roles.Contains(muteRole))
                    db.HashDeleteAsync("mutes", e.Member.Id);

                string rolesStr = "None";

                if (e.Member.Roles.Any())
                {
                    rolesStr = "";

                    foreach (DiscordRole role in e.Member.Roles.OrderBy(x => x.Position).Reverse())
                    {
                        rolesStr += role.Mention + " ";
                    }
                }

                var embed = new DiscordEmbedBuilder()
                    .WithColor(new DiscordColor(0xBA4119))
                    .WithTimestamp(DateTimeOffset.Now)
                    .WithThumbnail(e.Member.AvatarUrl)
                    .WithAuthor(
                        name: $"{e.Member.Username}#{e.Member.Discriminator} has left",
                        iconUrl: e.Member.AvatarUrl
                     )
                    .AddField("User", e.Member.Mention, false)
                    .AddField("User ID", e.Member.Id.ToString(), false)
                    .AddField("Action", "Left the server", false)
                    .AddField("Roles", rolesStr)
                    .WithFooter($"{client.CurrentUser.Username}LeaveEvent");

                LogChannelHelper.LogMessageAsync("users", $"{cfgjson.Emoji.UserLeave} **Member left the server!** - {e.Member.Id}", embed);

                var joinWatchlist = await db.ListRangeAsync("joinWatchedUsers");

                if (joinWatchlist.Contains(e.Member.Id))
                {
                    LogChannelHelper.LogMessageAsync("investigations", $"{cfgjson.Emoji.Warning} Watched user {e.Member.Mention} just left the server!", embed);
                }
            });
        }

        public static async Task GuildMemberUpdated(DiscordClient _, GuildMemberUpdateEventArgs e)
        {
            Task.Run(async () =>
            {
                // dont check bots
                if (e.Member.IsBot)
                    return;

                // in case we end up in random guilds
                if (e.Guild.Id != cfgjson.ServerID)
                    return;

                // if they are auto banned, don't progress any further
                if (await ScamHelpers.CheckAvatarsAsync(e.Member))
                    return;

                if (await ScamHelpers.UsernameCheckAsync(e.Member))
                    return;

                var muteRole = e.Guild.GetRole(cfgjson.MutedRole);
                var userMute = await db.HashGetAsync("mutes", e.Member.Id);

                // If they're externally unmuted, untrack it?
                // But not if they just joined.
                var currentTime = DateTime.Now;
                var joinTime = e.Member.JoinedAt.DateTime;
                var differrence = currentTime.Subtract(joinTime).TotalSeconds;
                if (differrence > 10 && !userMute.IsNull && !e.Member.Roles.Contains(muteRole))
                    db.HashDeleteAsync("mutes", e.Member.Id);

                DehoistHelpers.CheckAndDehoistMemberAsync(e.Member);
            }
            );
        }

        public static async Task UserUpdated(DiscordClient _, UserUpdateEventArgs e)
        {
            await Task.Run(async () =>
            {
                // dont check bots
                if (e.UserAfter.IsBot)
                    return;

                var member = await homeGuild.GetMemberAsync(e.UserAfter.Id);

                DehoistHelpers.CheckAndDehoistMemberAsync(member);
                ScamHelpers.UsernameCheckAsync(member);
            });
        }

    }
}
