using static Cliptok.Program;

namespace Cliptok.Events
{
    public class MemberEvents
    {
        public static async Task GuildMemberAdded(DiscordClient client, GuildMemberAddedEventArgs e)
        {
            client.Logger.LogDebug("Got a member added event for {member}", e.Member.Id);

            if (e.Guild.Id != cfgjson.ServerID)
                return;

            var embed = new DiscordEmbedBuilder()
               .WithColor(new DiscordColor(0x3E9D28))
               .WithTimestamp(DateTimeOffset.Now)
               .WithThumbnail(e.Member.AvatarUrl)
               .WithAuthor(
                   name: $"{DiscordHelpers.UniqueUsername(e.Member)} has joined",
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
                if (await db.HashExistsAsync("joinWatchedUsersNotes", e.Member.Id))
                {
                    embed.AddField($"Joinwatch Note", await db.HashGetAsync("joinWatchedUsersNotes", e.Member.Id));
                }

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
                DiscordRole mutedRole = await e.Guild.GetRoleAsync(cfgjson.MutedRole);
                await e.Member.GrantRoleAsync(mutedRole, "Reapplying mute on join: possible mute evasion.");
            }
            else if (e.Member.CommunicationDisabledUntil is not null)
            {
                await e.Member.TimeoutAsync(null, "Removing timeout since member was presumably unmuted while left");
            }

            if (!db.HashExists("unbanned", e.Member.Id))
            {
                if (avatars.Contains(e.Member.AvatarHash))
                {
                    var _ = BanHelpers.BanSilently(e.Guild, e.Member.Id, "Secret sauce");
                    await LogChannelHelper.LogMessageAsync("investigations", $"{cfgjson.Emoji.Banned} Raid-banned {e.Member.Mention} for matching avatar: {e.Member.AvatarUrl.Replace("1024", "128")}");
                }
            }

            // Restore user overrides stored in db (if there are any)

            var userOverwrites = await db.HashGetAsync("overrides", e.Member.Id.ToString());
            if (string.IsNullOrWhiteSpace(userOverwrites)) return; // user has no overrides saved
            var dictionary = JsonConvert.DeserializeObject<Dictionary<ulong, DiscordOverwrite>>(userOverwrites);
            if (dictionary is null) return;

            foreach (var overwrite in dictionary)
            {
                DiscordChannel channel;
                try
                {
                    channel = await client.GetChannelAsync(overwrite.Key);
                }
                catch
                {
                    continue;
                }

                await channel.AddOverwriteAsync(e.Member, overwrite.Value.Allowed, overwrite.Value.Denied,
                    "Restoring saved overrides for member.");
            }
        }

        public static async Task GuildMemberRemoved(DiscordClient client, GuildMemberRemovedEventArgs e)
        {
            client.Logger.LogDebug("Got a member removed event for {member}", e.Member.Id);

            if (e.Guild.Id != cfgjson.ServerID)
                return;

            var muteRole = await e.Guild.GetRoleAsync(cfgjson.MutedRole);
            var tqsMuteRole = await e.Guild.GetRoleAsync(cfgjson.TqsMutedRole);
            var userMute = await db.HashGetAsync("mutes", e.Member.Id);

            if (!userMute.IsNull && !e.Member.Roles.Contains(muteRole) & !e.Member.Roles.Contains(tqsMuteRole))
                db.HashDeleteAsync("mutes", e.Member.Id);

            if ((e.Member.Roles.Contains(muteRole) || e.Member.Roles.Contains(tqsMuteRole)) && userMute.IsNull)
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

            if (!userMute.IsNull && !e.Member.Roles.Contains(muteRole) && !e.Member.Roles.Contains(tqsMuteRole))
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
                    name: $"{DiscordHelpers.UniqueUsername(e.Member)} has left",
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
                if (await db.HashExistsAsync("joinWatchedUsersNotes", e.Member.Id))
                {
                    embed.AddField($"Joinwatch Note", await db.HashGetAsync("joinWatchedUsersNotes", e.Member.Id));
                }

                LogChannelHelper.LogMessageAsync("investigations", $"{cfgjson.Emoji.Warning} Watched user {e.Member.Mention} just left the server!", embed);
            }
        }

        public static async Task GuildMemberUpdated(DiscordClient client, GuildMemberUpdatedEventArgs e)
        {
            client.Logger.LogDebug("Got a member updated event for {member}", e.Member.Id);

            // dont check bots
            if (e.Member.IsBot)
                return;

            // in case we end up in random guilds
            if (e.Guild.Id != cfgjson.ServerID)
                return;

            // if they are auto banned, don't progress any further
            if (await ScamHelpers.UsernameCheckAsync(e.Member))
                return;

            var muteRole = await e.Guild.GetRoleAsync(cfgjson.MutedRole);
            var userMute = await db.HashGetAsync("mutes", e.Member.Id);

            // If they're externally unmuted, untrack it?
            // But not if they just joined.
            var currentTime = DateTime.Now;
            var joinTime = e.Member.JoinedAt.DateTime;
            var differrence = currentTime.Subtract(joinTime).TotalSeconds;
            if (differrence > 10 && !userMute.IsNull && !e.Member.Roles.Contains(muteRole))
                db.HashDeleteAsync("mutes", e.Member.Id);

            // Nickname lock check
            var nicknamelock = await db.HashGetAsync("nicknamelock", e.Member.Id);

            if (nicknamelock.HasValue)
            {
                if (e.Member.DisplayName != nicknamelock)
                {
                    var oldName = e.Member.DisplayName;
                    await e.Member.ModifyAsync(a =>
                    {
                        a.Nickname = nicknamelock.ToString();
                        a.AuditLogReason = "Nickname lock applied, reverting nickname change. Nice try though.";
                    });
                    await LogChannelHelper.LogMessageAsync("nicknames", $"{cfgjson.Emoji.MessageEdit} Reverted nickname change from {e.Member.Mention}: `{oldName}`");
                }
                // We don't want to run the dehoist checks on locked nicknames, else it may cause a fight between the two.
                return;
            }

            DehoistHelpers.CheckAndDehoistMemberAsync(e.Member);

            // Persist permadehoists
            if (await db.SetContainsAsync("permadehoists", e.Member.Id))
                if (e.Member.DisplayName[0] != DehoistHelpers.dehoistCharacter)
                    // Member is in permadehoist list. Dehoist.
                    e.Member.ModifyAsync(a =>
                    {
                        a.Nickname = DehoistHelpers.DehoistName(e.Member.DisplayName);
                        a.AuditLogReason = "[Automatic dehoist; user is permadehoisted]";
                    });
        }

        public static async Task UserUpdated(DiscordClient client, UserUpdatedEventArgs e)
        {
            client.Logger.LogDebug("Got a user updated event for {member}", e.UserAfter.Id);

            // dont check bots
            if (e.UserAfter.IsBot)
                return;

            var member = await homeGuild.GetMemberAsync(e.UserAfter.Id);

            // Nickname lock check
            var nicknamelock = await db.HashGetAsync("nicknamelock", member.Id);

            if (nicknamelock.HasValue)
            {
                if (member.DisplayName != nicknamelock)
                {
                    var oldName = member.DisplayName;
                    await member.ModifyAsync(async a =>
                    {
                        a.Nickname = nicknamelock.ToString();
                        a.AuditLogReason = "Nickname lock applied, reverting nickname change. Nice try though.";
                    });
                    await LogChannelHelper.LogMessageAsync("nicknames", $"{cfgjson.Emoji.MessageEdit} Reverted nickname change from {member.Mention}: `{oldName}`");
                }
                // We don't want to run the dehoist checks on locked nicknames, else it may cause a fight between the two.
                return;
            }

            DehoistHelpers.CheckAndDehoistMemberAsync(member);
            ScamHelpers.UsernameCheckAsync(member); ;
        }

    }
}
