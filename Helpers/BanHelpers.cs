namespace Cliptok.Helpers
{
    public class BanHelpers
    {
        public static MemberPunishment MostRecentBan; 

        public static async Task<bool> BanFromServerAsync(ulong targetUserId, string reason, ulong moderatorId, DiscordGuild guild, int deleteDays = 7, DiscordChannel channel = null, TimeSpan banDuration = default, bool appealable = false, bool compromisedAccount = false)
        {
            bool permaBan = false;
            DateTime? actionTime = DateTime.Now;
            DateTime? expireTime = actionTime + banDuration;
            DiscordMember moderator = await guild.GetMemberAsync(moderatorId);

            if (reason.ToLower().Contains("compromised"))
                compromisedAccount = true;

            if (banDuration == default)
            {
                permaBan = true;
                expireTime = null;
            }

            (DiscordMessage? dmMessage, DiscordMessage? chatMessage) output = new();

            reason = reason.Replace("`", "\\`").Replace("*", "\\*");
            if (channel is not null)
            {
                if (banDuration == default)
                    output.chatMessage = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} <@{targetUserId}> has been banned: **{reason}**");
                else
                    output.chatMessage = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} <@{targetUserId}> has been banned for **{TimeHelpers.TimeToPrettyFormat(banDuration, false)}**: **{reason}**");
            }

            try
            {
                DiscordMember targetMember = await guild.GetMemberAsync(targetUserId);
                if (permaBan)
                {
                    if (appealable)
                    {
                        if (compromisedAccount)
                            output.dmMessage = await targetMember.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} You have been banned from **{guild.Name}**!\nReason: **{reason}**\nYou can appeal the ban here: <{Program.cfgjson.AppealLink}>\nBefore appealing, please follow these steps to protect your account:\n1. Reset your Discord account password. Even if you use MFA, this will reset all session tokens.\n2. Review active sessions and authorised app connections.\n3. Ensure your PC is free of malware.\n4. [Enable MFA](https://support.discord.com/hc/en-us/articles/219576828-Setting-up-Multi-Factor-Authentication) if not already.");
                        else
                            output.dmMessage = await targetMember.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} You have been banned from **{guild.Name}**!\nReason: **{reason}**\nYou can appeal the ban here: <{Program.cfgjson.AppealLink}>");
                    }
                    else
                    {
                        output.dmMessage = await targetMember.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} You have been permanently banned from **{guild.Name}**!\nReason: **{reason}**");
                    }
                }
                else
                {
                    output.dmMessage = await targetMember.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} You have been banned from **{guild.Name}** for {TimeHelpers.TimeToPrettyFormat(banDuration, false)}!\nReason: **{reason}**\nBan expires: <t:{TimeHelpers.ToUnixTimestamp(expireTime)}:R>");
                }
            }
            catch
            {
                // A DM failing to send isn't important.
            }

            MemberPunishment newBan = new()
            {
                MemberId = targetUserId,
                ModId = moderatorId,
                ServerId = guild.Id,
                ExpireTime = expireTime,
                ActionTime = actionTime,
                Reason = reason
            };

            if (output.chatMessage is not null)
                newBan.ContextMessageReference = new()
                {
                    MessageId = output.chatMessage.Id,
                    ChannelId = output.chatMessage.ChannelId
                };

            if (output.dmMessage is not null)
                newBan!.DmMessageReference = new()
                {
                    MessageId = output.dmMessage.Id,
                    ChannelId = output.dmMessage.ChannelId
                };

            await Program.db.HashSetAsync("bans", targetUserId, JsonConvert.SerializeObject(newBan));

            // used for collision detection
            MostRecentBan = newBan;

            // If ban is for a compromised account, add to list so the context message can be more-easily deleted later
            if (compromisedAccount)
                Program.db.HashSet("compromisedAccountBans", targetUserId, JsonConvert.SerializeObject(newBan));

            try
            {
                string logOut;
                await guild.BanMemberAsync(targetUserId, TimeSpan.FromDays(deleteDays), reason);
                if (permaBan)
                {
                    if (appealable)
                    {
                        logOut = $"{Program.cfgjson.Emoji.Banned} <@{targetUserId}> was permanently banned (with appeal) by {moderator.Mention}.\nReason: **{reason}**";
                    }
                    else
                    {
                        logOut = $"{Program.cfgjson.Emoji.Banned} <@{targetUserId}> was permanently banned by {moderator.Mention}.\nReason: **{reason}**";
                    }
                }
                else
                {
                    logOut = $"{Program.cfgjson.Emoji.Banned} <@{targetUserId}> was banned for {TimeHelpers.TimeToPrettyFormat(banDuration, false)} by {moderator.Mention}.\nReason: **{reason}**\nBan expires: <t:{TimeHelpers.ToUnixTimestamp(expireTime)}:R>";
                }
                _ = LogChannelHelper.LogMessageAsync("mod", logOut);

                if (channel is not null)
                    logOut += $"\nChannel: {channel.Mention}";

                _ = FindModmailThreadAndSendMessage(guild, $"User ID: {targetUserId}", logOut);

                // Remove user message tracking
                if (await Program.db.SetContainsAsync("trackedUsers", targetUserId))
                {
                    await Program.db.SetRemoveAsync("trackedUsers", targetUserId);
                    var channelId = Program.db.HashGet("trackingThreads", targetUserId);
                    DiscordThreadChannel thread = (DiscordThreadChannel)await Program.discord.GetChannelAsync((ulong)channelId);
                    await thread.ModifyAsync(thread =>
                    {
                        thread.IsArchived = true;
                    });
                }
            }
            catch
            {
                return false;
            }
            return true;

        }

        public static async Task FindModmailThreadAndSendMessage(DiscordGuild guild, string searchText, string messageToSend)
        {
            var matchPair = guild.Channels.FirstOrDefault(c => c.Value.Type == DiscordChannelType.Text && c.Value.Topic is not null && c.Value.Topic.EndsWith(searchText));
            var channel = matchPair.Value;

            if (channel != default)
            {
                DiscordMessageBuilder message = new()
                {
                    Content = messageToSend
                };
                await channel.SendMessageAsync(message.WithAllowedMentions(Mentions.None));

            }
        }


        public static async Task UnbanFromServerAsync(DiscordGuild targetGuild, ulong targetUserId)
        {
            try
            {
                DiscordUser user = await Program.discord.GetUserAsync(targetUserId);
                await targetGuild.UnbanMemberAsync(user, "Temporary ban expired");
            }
            catch
            {
                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Denied} Attempt to unban <@{targetUserId}> failed!\nMaybe they were already unbanned?")
                        .WithAllowedMentions(Mentions.None)
                    );
            }
            // Even if the bot failed to unban, it reported that failure to a log channel and thus the ban record
            //  can be safely removed internally.
            await Program.db.HashDeleteAsync("bans", targetUserId);
        }

        public async static Task<bool> UnbanUserAsync(DiscordGuild guild, DiscordUser target, string reason = "")
        {
            await Program.db.HashSetAsync("unbanned", target.Id, true);
            try
            {
                await guild.UnbanMemberAsync(user: target, reason: reason);
            }
            catch (Exception e)
            {
                Program.discord.Logger.LogError(Program.CliptokEventID, e, "An exception occurred while unbanning {user}", target.Id);
                return false;
            }
            await LogChannelHelper.LogMessageAsync("mod", new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Unbanned} Successfully unbanned {target.Mention}!\nReason: **{reason}**").WithAllowedMentions(Mentions.None));
            await Program.db.HashDeleteAsync("bans", target.Id.ToString());
            return true;
        }

        public static async Task<bool> BanSilently(DiscordGuild targetGuild, ulong targetUserId, string reason = "Mass ban")
        {
            try
            {
                await targetGuild.BanMemberAsync(targetUserId, TimeSpan.FromDays(7), reason);

                // Remove user message tracking
                if (await Program.db.SetContainsAsync("trackedUsers", targetUserId))
                {
                    await Program.db.SetRemoveAsync("trackedUsers", targetUserId);
                    var channelId = Program.db.HashGet("trackingThreads", targetUserId);
                    DiscordThreadChannel thread = (DiscordThreadChannel)await Program.discord.GetChannelAsync((ulong)channelId);
                    await thread.ModifyAsync(thread =>
                    {
                        thread.IsArchived = true;
                    });
                }

                return true;
            }
            catch
            {
                return false;
            }

        }

        public static async Task<DiscordEmbed> BanStatusEmbed(DiscordUser user, DiscordGuild guild)
        {
            DiscordMember member = default;
            DiscordEmbedBuilder embedBuilder = new();
            var guildBans = await guild.GetBansAsync();
            var userBan = guildBans.FirstOrDefault(x => x.User.Id == user.Id);

            embedBuilder.WithFooter(
                    $"User ID: {user.Id}",
                    null
                )
                .WithAuthor(
                    $"Ban status for {DiscordHelpers.UniqueUsername(user)}",
                    null,
                    await LykosAvatarMethods.UserOrMemberAvatarURL(user, Program.homeGuild, "png")
                );

            if (await Program.db.HashExistsAsync("bans", user.Id))
            {
                MemberPunishment ban = JsonConvert.DeserializeObject<MemberPunishment>(Program.db.HashGet("bans", user.Id));

                embedBuilder.WithDescription("User is banned.")
                    .AddField("Banned", ban.ActionTime is null ? "Unknown time (Ban is too old)" : $"<t:{TimeHelpers.ToUnixTimestamp(ban.ActionTime)}:R>", true)
                    .WithColor(new DiscordColor(0xFEC13D));

                if (ban.ExpireTime is null)
                    embedBuilder.AddField("Ban expires", "Never", true);
                else
                    embedBuilder.AddField("Ban expires", $"<t:{TimeHelpers.ToUnixTimestamp(ban.ExpireTime)}:R>", true);

                embedBuilder.AddField("Banned by", $"<@{ban.ModId}>", true);

                embedBuilder.AddField("Reason", ban.Reason is null ? "No reason provided" : ban.Reason, false);
            }
            else
            {
                if (userBan is null)
                {
                    embedBuilder.WithDescription("User is not banned.")
                        .WithColor(color: DiscordColor.DarkGreen);
                }
                else
                {
                    embedBuilder.WithDescription($"User was banned without using {Program.discord.CurrentUser.Username}, so limited information is available.")
                                            .WithColor(new DiscordColor(0xFEC13D));
                    embedBuilder.AddField("Reason", string.IsNullOrWhiteSpace(userBan.Reason) ? "No reason provided" : userBan.Reason, false);
                }
            }

            return embedBuilder.Build();
        }

    }
}
