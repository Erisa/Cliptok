namespace Cliptok.Helpers
{
    public class BanHelpers
    {
        public static async Task<bool> BanFromServerAsync(ulong targetUserId, string reason, ulong moderatorId, DiscordGuild guild, int deleteDays = 7, DiscordChannel channel = null, TimeSpan banDuration = default, bool appealable = false)
        {
            bool permaBan = false;
            DateTime? actionTime = DateTime.Now;
            DateTime? expireTime = actionTime + banDuration;
            DiscordMember moderator = await guild.GetMemberAsync(moderatorId);

            if (banDuration == default)
            {
                permaBan = true;
                expireTime = null;
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

            await Program.db.HashSetAsync("bans", targetUserId, JsonConvert.SerializeObject(newBan));

            try
            {
                DiscordMember targetMember = await guild.GetMemberAsync(targetUserId);
                if (permaBan)
                {
                    if (appealable)
                    {
                        await targetMember.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} You have been banned from **{guild.Name}**!\nReason: **{reason}**\nYou can appeal the ban here: <{Program.cfgjson.AppealLink}>");
                    }
                    else
                    {
                        await targetMember.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} You have been permanently banned from **{guild.Name}**!\nReason: **{reason}**");
                    }
                }
                else
                {
                    await targetMember.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} You have been banned from **{guild.Name}** for {TimeHelpers.TimeToPrettyFormat(banDuration, false)}!\nReason: **{reason}**\nBan expires: <t:{TimeHelpers.ToUnixTimestamp(expireTime)}:R>");
                }
            }
            catch
            {
                // A DM failing to send isn't important.
            }

            try
            {
                string logOut;
                await guild.BanMemberAsync(targetUserId, deleteDays, reason);
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
            }
            catch
            {
                return false;
            }
            return true;

        }

        public static async Task FindModmailThreadAndSendMessage(DiscordGuild guild, string searchText, string messageToSend)
        {
            var matchPair = guild.Channels.FirstOrDefault(c => c.Value.Type == ChannelType.Text && c.Value.Topic is not null && c.Value.Topic.EndsWith(searchText));
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
            await LogChannelHelper.LogMessageAsync("mod", new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Unbanned} Successfully unbanned {target.Mention}!").WithAllowedMentions(Mentions.None));
            await Program.db.HashDeleteAsync("bans", target.Id.ToString());
            return true;
        }

        public static async Task<bool> BanSilently(DiscordGuild targetGuild, ulong targetUserId, string reason = "Mass ban")
        {
            try
            {
                await targetGuild.BanMemberAsync(targetUserId, 7, reason);
                return true;
            }
            catch
            {
                return false;
            }

        }

    }
}
