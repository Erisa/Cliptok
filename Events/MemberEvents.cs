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

            var userLogEmbed = new DiscordEmbedBuilder()
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

            LogChannelHelper.LogMessageAsync("users", $"{cfgjson.Emoji.UserJoin} **Member joined the server!** - {e.Member.Id}", userLogEmbed);

            // Get this user's notes that are set to show on join/leave
            var userNotes = (await redis.HashGetAllAsync(e.Member.Id.ToString()))
                .Where(x => JsonConvert.DeserializeObject<UserNote>(x.Value).Type == WarningType.Note
                        && JsonConvert.DeserializeObject<UserNote>(x.Value).ShowOnJoinAndLeave).ToDictionary(
                    x => x.Name.ToString(),
                    x => JsonConvert.DeserializeObject<UserNote>(x.Value)
                );

            if (userNotes.Count > 0)
            {
                var notesEmbed = await UserNoteHelpers.GenerateUserNotesEmbedAsync(e.Member, false, userNotes, colorOverride: new DiscordColor(0x3E9D28));
                LogChannelHelper.LogMessageAsync("investigations", $"{cfgjson.Emoji.UserJoin} {e.Member.Mention} just joined the server with {(userNotes.Count == 1 ? "a note" : "notes")} set to show on join!", notesEmbed);
            }

            if (redis.HashExists("raidmode", e.Guild.Id))
            {
                if (!redis.KeyExists("raidmode-accountage") || (TimeHelpers.ToUnixTimestamp(e.Member.CreationTimestamp.DateTime) > (long)redis.StringGet("raidmode-accountage")))
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

            if (await redis.HashExistsAsync("mutes", e.Member.Id))
            {
                // todo: store per-guild
                DiscordRole mutedRole = await e.Guild.GetRoleAsync(cfgjson.MutedRole);
                await e.Member.GrantRoleAsync(mutedRole, "Reapplying mute on join: possible mute evasion.");
            }
            else if (e.Member.CommunicationDisabledUntil is not null)
            {
                await e.Member.TimeoutAsync(null, "Removing timeout since member was presumably unmuted while left");
            }

            if (!redis.HashExists("unbanned", e.Member.Id))
            {
                if (avatars.Contains(e.Member.AvatarHash))
                {
                    var _ = BanHelpers.BanSilently(e.Guild, e.Member.Id, "Secret sauce");
                    await LogChannelHelper.LogMessageAsync("investigations", $"{cfgjson.Emoji.Banned} Raid-banned {e.Member.Mention} for matching avatar: {e.Member.AvatarUrl.Replace("1024", "128")}");
                }
            }

            // Restore user overrides stored in db (if there are any)

            var userOverwrites = await redis.HashGetAsync("overrides", e.Member.Id.ToString());
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

            // Attempt to check if member is cached
            bool isMemberCached = client.Guilds[e.Guild.Id].Members.ContainsKey(e.Member.Id);

            if (isMemberCached)
            {
                // Only check mute role against db entry if we know the member's roles are accurate.
                // If the member is not cached, we will think they have no roles when they might actually be muted!
                // Then we would be falsely removing their mute entry.

                var muteRole = await e.Guild.GetRoleAsync(cfgjson.MutedRole);

                DiscordRole tqsMuteRole = default;
                if (cfgjson.TqsMutedRole != 0)
                    tqsMuteRole = await e.Guild.GetRoleAsync(cfgjson.TqsMutedRole);

                var userMute = await redis.HashGetAsync("mutes", e.Member.Id);

                if (!userMute.IsNull && !e.Member.Roles.Contains(muteRole) & !e.Member.Roles.Contains(tqsMuteRole))
                    redis.HashDeleteAsync("mutes", e.Member.Id);

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

                    redis.HashSetAsync("mutes", e.Member.Id, JsonConvert.SerializeObject(newMute));
                }

                if (!userMute.IsNull && !e.Member.Roles.Contains(muteRole) && !e.Member.Roles.Contains(tqsMuteRole))
                    redis.HashDeleteAsync("mutes", e.Member.Id);
            }

            string rolesStr = "None";

            if (e.Member.Roles.Any())
            {
                rolesStr = "";

                foreach (DiscordRole role in e.Member.Roles.OrderBy(x => x.Position).Reverse())
                {
                    rolesStr += role.Mention + " ";
                }
            }

            var userLogEmbed = new DiscordEmbedBuilder()
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

            LogChannelHelper.LogMessageAsync("users", $"{cfgjson.Emoji.UserLeave} **Member left the server!** - {e.Member.Id}", userLogEmbed);

            // Get this user's notes that are set to show on join/leave
            var userNotes = (await redis.HashGetAllAsync(e.Member.Id.ToString()))
                .Where(x => JsonConvert.DeserializeObject<UserNote>(x.Value).Type == WarningType.Note
                            && JsonConvert.DeserializeObject<UserNote>(x.Value).ShowOnJoinAndLeave).ToDictionary(
                    x => x.Name.ToString(),
                    x => JsonConvert.DeserializeObject<UserNote>(x.Value)
                );

            if (userNotes.Count > 0)
            {
                var notesEmbed = await UserNoteHelpers.GenerateUserNotesEmbedAsync(e.Member, false, userNotes, colorOverride: new DiscordColor(0xBA4119));
                LogChannelHelper.LogMessageAsync("investigations", $"{cfgjson.Emoji.UserLeave} {e.Member.Mention} just left the server with {(userNotes.Count == 1 ? "a note" : "notes")} set to show on leave!", notesEmbed);
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
            var userMute = await redis.HashGetAsync("mutes", e.Member.Id);

            // If they're externally unmuted, untrack it?
            // But not if they just joined.
            var currentTime = DateTime.Now;
            var joinTime = e.Member.JoinedAt.DateTime;
            var differrence = currentTime.Subtract(joinTime).TotalSeconds;
            if (differrence > 10 && !userMute.IsNull && !e.Member.Roles.Contains(muteRole))
                redis.HashDeleteAsync("mutes", e.Member.Id);

            // Nickname lock check
            var nicknamelock = await redis.HashGetAsync("nicknamelock", e.Member.Id);

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
            if (await redis.SetContainsAsync("permadehoists", e.Member.Id))
                if (e.Member.DisplayName[0] != DehoistHelpers.dehoistCharacter && !e.Member.MemberFlags.Value.HasFlag(DiscordMemberFlags.AutomodQuarantinedUsername))
                    // Member is in permadehoist list. Dehoist.
                    e.Member.ModifyAsync(a =>
                    {
                        a.Nickname = DehoistHelpers.DehoistName(e.Member.DisplayName);
                        a.AuditLogReason = "[Automatic dehoist; user is permadehoisted]";
                    });

            // cache user
            await LogAndCacheUserUpdateAsync(client, e.Member);
        }

        public static async Task LogAndCacheUserUpdateAsync(DiscordClient client, DiscordUser user)
        {
            var dbContext = new CliptokDbContext();
            var cachedUser = await dbContext.Users.FindAsync(user.Id);
            if (cachedUser is null)
            {
                var newUser = new Models.CachedDiscordUser
                {
                    Id = user.Id,
                    Username = user.Username,
                    DisplayName = user.GlobalName ?? user.Username,
                    AvatarUrl = user.AvatarUrl ?? user.DefaultAvatarUrl,
                    IsBot = user.IsBot
                };
                await dbContext.Users.AddAsync(newUser);
            }
            else
            {
                if (cachedUser.Username != user.Username)
                {
                    await LogChannelHelper.LogMessageAsync("users", new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.UserUpdate} **Member username updated!** - {user.Mention}")
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithColor(new DiscordColor(0x3E9D28))
                            .WithTimestamp(DateTimeOffset.Now)
                            .WithThumbnail(user.AvatarUrl)
                            .AddField("Old username", cachedUser.Username)
                            .AddField("New username", user.Username)
                            .WithFooter($"User ID: {user.Id}\n{client.CurrentUser.Username}UserUpdate")));
                }

                if (cachedUser.Username != user.Username || cachedUser.DisplayName != (user.GlobalName ?? user.Username) || cachedUser.AvatarUrl != (user.AvatarUrl ?? user.DefaultAvatarUrl))
                {
                    cachedUser.Username = user.Username;
                    cachedUser.DisplayName = user.GlobalName ?? user.Username;
                    cachedUser.AvatarUrl = user.AvatarUrl ?? user.DefaultAvatarUrl;
                    dbContext.Update(cachedUser);
                    await dbContext.SaveChangesAsync();
                }
            }
            await dbContext.DisposeAsync();
        }

        public static async Task UserUpdated(DiscordClient client, UserUpdatedEventArgs e)
        {
            client.Logger.LogDebug("Got a user updated event for {member}", e.UserAfter.Id);

            // dont check bots
            if (e.UserAfter.IsBot)
                return;

            var member = await homeGuild.GetMemberAsync(e.UserAfter.Id);

            // Nickname lock check
            var nicknamelock = await redis.HashGetAsync("nicknamelock", member.Id);

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
            ScamHelpers.UsernameCheckAsync(member);

            // cache user or log change
            await LogAndCacheUserUpdateAsync(client, e.UserAfter);
        }

    }
}
