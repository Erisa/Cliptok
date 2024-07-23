namespace Cliptok.Helpers
{
    public class MuteHelpers
    {
        public static async Task<DiscordEmbed> MuteStatusEmbed(DiscordUser user, DiscordGuild guild)
        {
            DiscordMember member = default;
            DiscordEmbedBuilder embedBuilder = new();

            embedBuilder.WithFooter(
                    $"User ID: {user.Id}",
                    null
                )
                .WithAuthor(
                    $"Mute status for {DiscordHelpers.UniqueUsername(user)}",
                    null,
                    await LykosAvatarMethods.UserOrMemberAvatarURL(user, Program.homeGuild, "png")
                );

            try
            {
                member = await guild.GetMemberAsync(user.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // nothing
            }

            if (await Program.db.HashExistsAsync("mutes", user.Id))
            {
                MemberPunishment mute = JsonConvert.DeserializeObject<MemberPunishment>(Program.db.HashGet("mutes", user.Id));

                if (mute.Reason is null && mute.ModId == Program.discord.CurrentUser.Id)
                {
                    embedBuilder.WithDescription($"User was muted without using {Program.discord.CurrentUser.Username}, so no information is available.")
                        .WithColor(new DiscordColor(0xFEC13D));
                }
                else
                {
                    embedBuilder.WithDescription("User is muted.")
                        .AddField("Muted", mute.ActionTime is null ? "Unknown time (Mute is too old)" : $"<t:{TimeHelpers.ToUnixTimestamp(mute.ActionTime)}:R>", true)
                        .WithColor(new DiscordColor(0xFEC13D));

                    if (mute.ExpireTime is null)
                        embedBuilder.AddField("Mute expires", "Never", true);
                    else
                        embedBuilder.AddField("Mute expires", $"<t:{TimeHelpers.ToUnixTimestamp(mute.ExpireTime)}:R>", true);

                    embedBuilder.AddField("Muted by", $"<@{mute.ModId}>", true);

                    if (mute.Reason is null && mute.ModId == Program.discord.CurrentUser.Id)
                        embedBuilder.AddField("Reason", "Mute record created when user left server while manually muted.", false);
                    else
                        embedBuilder.AddField("Reason", mute.Reason is null ? "No reason provided" : mute.Reason, false);
                }
            }
            else
            {
                if (member is not null && member.Roles.Any(role => role.Id == Program.cfgjson.MutedRole))
                {
                    embedBuilder.WithDescription($"User was muted without using {Program.discord.CurrentUser.Username}, so no information is available.")
                        .WithColor(new DiscordColor(0xFEC13D));
                }
                else
                {
                    embedBuilder.WithDescription("User is not muted.")
                        .WithColor(color: DiscordColor.DarkGreen);
                }
            }

            return embedBuilder.Build();
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

        // Only to be used on naughty users.
        public static async Task<(DiscordMessage? dmMessage, DiscordMessage? chatMessage)> MuteUserAsync(DiscordUser naughtyUser, string reason, ulong moderatorId, DiscordGuild guild, DiscordChannel channel = null, TimeSpan muteDuration = default, bool alwaysRespond = false, bool isTqsMute = false)
        {
            bool permaMute = false;
            DateTime? actionTime = DateTime.Now;
            DiscordRole mutedRole = isTqsMute
                ? guild.GetRole(Program.cfgjson.TqsMutedRole)
                : guild.GetRole(Program.cfgjson.MutedRole);
            DateTime? expireTime = actionTime + muteDuration;
            DiscordMember moderator = await guild.GetMemberAsync(moderatorId);

            (DiscordMessage? dmMessage, DiscordMessage? chatMessage) output = new();

            DiscordMember naughtyMember = default;
            try
            {
                naughtyMember = await guild.GetMemberAsync(naughtyUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // nothing
            }

            if (muteDuration == default)
            {
                permaMute = true;
                expireTime = null;
            }

            if (naughtyMember != default)
            {
                try
                {
                    string fullReason = $"[{(isTqsMute ? "TQS " : "")}Mute by {DiscordHelpers.UniqueUsername(moderator)}]: {reason}";
                    await naughtyMember.GrantRoleAsync(mutedRole, fullReason);

                    // for global mutes, issue timeout & kick from any voice channel; does not apply to TQS mutes as they are not server-wide
                    if (!isTqsMute)
                    {
                        try
                        {
                            try
                            {
                                await naughtyMember.TimeoutAsync(expireTime + TimeSpan.FromSeconds(10), fullReason);
                            }
                            catch (Exception e)
                            {
                                Program.discord.Logger.LogError(e, "Failed to issue timeout to {user}", naughtyMember.Id);
                            }

                            // Remove the member from any Voice Channel they're currently in.
                            await naughtyMember.ModifyAsync(x => x.VoiceChannel = null);
                        }
                        catch (DSharpPlus.Exceptions.UnauthorizedException)
                        {
                            // do literally nothing. who cares?
                        }
                    }
                }
                catch
                {
                    return output;
                }
            }

            if (naughtyMember != default)
            {
                try
                {
                    if (permaMute)
                    {
                        output.dmMessage = await naughtyMember.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} You have been muted in **{guild.Name}**!\nReason: **{reason}**");
                    }

                    else
                    {
                        if (isTqsMute)
                        {
                            output.dmMessage = await naughtyMember.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} You have been temporarily muted, in **tech support channels only**, in **{guild.Name}** for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}** pending action from a Moderator." +
                                $"\nReason: **{reason}**" +
                                $"\nMute expires: <t:{TimeHelpers.ToUnixTimestamp(expireTime)}:R>");
                        }
                        else
                        {
                            output.dmMessage = await naughtyMember.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} You have been muted in **{guild.Name}** for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}**!" +
                                $"\nReason: **{reason}**" +
                                $"\nMute expires: <t:{TimeHelpers.ToUnixTimestamp(expireTime)}:R>");
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e is not DSharpPlus.Exceptions.UnauthorizedException)
                    {
                        Program.discord.Logger.LogWarning(e, "Failed to send mute DM to user: {user}", naughtyMember.Id);
                    }

                    // A DM failing to send isn't important, but let's put it in chat just so it's somewhere.
                    if (channel is not null)
                    {
                        if (muteDuration == default)
                            output.chatMessage = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been muted: **{reason}**");
                        else
                        {
                            if (isTqsMute)
                            {
                                output.chatMessage = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been temporarily muted, in tech support channels only, for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}** pending action from a Moderator: **{reason}**");
                            }
                            else
                            {
                                output.chatMessage = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been muted for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}**: **{reason}**");
                            }
                        }
                    }
                }
            }

            if (output.chatMessage is null && channel is not null && alwaysRespond)
            {
                reason = reason.Replace("`", "\\`").Replace("*", "\\*");
                if (muteDuration == default)
                    output.chatMessage = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been muted: **{reason}**");
                else
                {
                    if (isTqsMute)
                    {
                        output.chatMessage = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been temporarily muted, in tech support channels only, for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}** pending action from a Moderator: **{reason}**");
                    }
                    else
                    {
                        output.chatMessage = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been muted for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}**: **{reason}**");
                    }
                }
            }

            MemberPunishment newMute = new()
            {
                MemberId = naughtyUser.Id,
                ModId = moderatorId,
                ServerId = guild.Id,
                ExpireTime = expireTime,
                ActionTime = actionTime,
                Reason = reason
            };

            if (output.chatMessage is not null)
                newMute.ContextMessageReference = new()
                {
                    MessageId = output.chatMessage.Id,
                    ChannelId = output.chatMessage.ChannelId
                };

            if (output.dmMessage is not null)
                newMute.DmMessageReference = new()
                {
                    MessageId = output.dmMessage.Id,
                    ChannelId = output.dmMessage.ChannelId
                };
            
            try
            {
                if (permaMute)
                {
                    await LogChannelHelper.LogMessageAsync("mod", new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} was successfully muted by {moderator.Mention}.\nReason: **{reason}**")
                        .WithAllowedMentions(Mentions.None)
                    );
                }
                else
                {
                    // if TQS mute, log to investigations channel also & make it clear in regular mod logs that it's a TQS mute
                    if (isTqsMute)
                    {
                        await LogChannelHelper.LogMessageAsync("investigations", new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} was TQS-muted for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}** by {moderator.Mention}." +
                                $"\nReason: **{reason}**" +
                                $"\nMute expires: <t:{TimeHelpers.ToUnixTimestamp(expireTime)}:R>" +
                                $"\n-# Context: {(await DiscordHelpers.GetMessageFromReferenceAsync(newMute.ContextMessageReference)).JumpLink}")
                            .WithAllowedMentions(Mentions.None)
                        );

                        await LogChannelHelper.LogMessageAsync("mod", new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} was TQS-muted for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}** by {moderator.Mention}." +
                                         $"\nReason: **{reason}**" +
                                         $"\nMute expires: <t:{TimeHelpers.ToUnixTimestamp(expireTime)}:R>")
                            .WithAllowedMentions(Mentions.None)
                        );
                    }
                    else
                    {
                        await LogChannelHelper.LogMessageAsync("mod", new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} was successfully muted for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}** by {moderator.Mention}." +
                                         $"\nReason: **{reason}**" +
                                         $"\nMute expires: <t:{TimeHelpers.ToUnixTimestamp(expireTime)}:R>")
                            .WithAllowedMentions(Mentions.None)
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Program.discord.Logger.LogError(ex, "thing");
            }

            await Program.db.HashSetAsync("mutes", naughtyUser.Id, JsonConvert.SerializeObject(newMute));

            return output;
        }

        public static async Task<bool> UnmuteUserAsync(DiscordUser targetUser, string reason = "", bool manual = true, DiscordUser modUser = default)
        {
            var muteDetailsJson = await Program.db.HashGetAsync("mutes", targetUser.Id);
            bool success = false;
            bool wasTqsMute = false;
            DiscordGuild guild = await Program.discord.GetGuildAsync(Program.cfgjson.ServerID);

            // todo: store per-guild
            DiscordRole mutedRole = guild.GetRole(Program.cfgjson.MutedRole);
            DiscordRole tqsMutedRole = guild.GetRole(Program.cfgjson.TqsMutedRole);
            DiscordMember member = default;
            try
            {
                member = await guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException ex)
            {
                Program.discord.Logger.LogWarning(eventId: Program.CliptokEventID, exception: ex, message: "Failed to unmute {user} in {servername} because they weren't in the server.", $"{DiscordHelpers.UniqueUsername(targetUser)}", guild.Name);
            }

            if (member == default)
            {
                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Information} Attempt to remove Muted role from {targetUser.Mention} failed because the user could not be found.\nThis is expected if the user was banned or left.")
                        .WithAllowedMentions(Mentions.None)
                    );
            }
            else
            {
                // Perhaps we could be catching something specific, but this should do for now.
                try
                {
                    // Try to revoke standard Muted role first. If it fails, the user might just be TQS-muted.
                    // Try removing TQS mute role regardless of whether we could successfully remove the standard
                    // muted role.
                    // If both attempts fail, do standard failure error handling.
                    try
                    {
                        await member.RevokeRoleAsync(role: mutedRole, reason);
                    }
                    finally
                    {
                        // Check member roles for TQS mute role
                        if (member.Roles.Contains(tqsMutedRole))
                        {
                            await member.RevokeRoleAsync(role: tqsMutedRole, reason);
                            wasTqsMute = true; // only true if TQS mute role was found & removed
                        }
                    }

                    foreach (var role in member.Roles)
                    {
                        if (role.Name == "Muted" && role.Id != Program.cfgjson.MutedRole)
                        {
                            try
                            {
                                await member.RevokeRoleAsync(role: role, reason: reason);
                            }
                            catch
                            {
                                // ignore, continue to next role
                            }
                        }
                    }
                    success = true;
                }
                catch
                {
                    await LogChannelHelper.LogMessageAsync("mod",
                        new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Error} Attempt to removed Muted role from {targetUser.Mention} failed because of a Discord API error!" +
                                $"\nIf the role was removed manually, this error can be disregarded safely.")
                            .WithAllowedMentions(Mentions.None)
                        );
                }
                try
                {
                    // only try to remove timeout for non-TQS mutes
                    // TQS mutes are not server-wide so this would fail every time for TQS mutes,
                    // and we don't want to log a failure for every removed TQS mute
                    if (!wasTqsMute)
                        await member.TimeoutAsync(until: null, reason: reason);
                }
                catch (Exception ex)
                {
                    Program.discord.Logger.LogError(message: "Error occurred trying to remove Timeout from {user}", args: member.Id, exception: ex, eventId: Program.CliptokEventID);
                }

                if (success)
                {
                    string unmuteMsg = manual
                        ? $"{Program.cfgjson.Emoji.Information} {targetUser.Mention} was successfully unmuted by {modUser.Mention}!"
                        : $"{Program.cfgjson.Emoji.Information} Successfully unmuted {targetUser.Mention}!";

                    await LogChannelHelper.LogMessageAsync("mod", new DiscordMessageBuilder().WithContent(unmuteMsg).WithAllowedMentions(Mentions.None));

                    if (manual && muteDetailsJson.HasValue)
                    {
                        var muteDetails = JsonConvert.DeserializeObject<MemberPunishment>(muteDetailsJson);

                        var contextMessage = await DiscordHelpers.GetMessageFromReferenceAsync(muteDetails.ContextMessageReference);
                        if (contextMessage is not null)
                            await contextMessage.DeleteAsync();

                        var dmMessage = await DiscordHelpers.GetMessageFromReferenceAsync(muteDetails.DmMessageReference);
                        if (dmMessage is not null)
                        {
                            if (muteDetails.ExpireTime is null)
                            {
                                await dmMessage.ModifyAsync($"{Program.cfgjson.Emoji.Success} You were muted in **{guild.Name}**, but the mute was revoked by a Moderator.");
                            }
                            else
                            {
                                await dmMessage.ModifyAsync($"{Program.cfgjson.Emoji.Success} You were muted in **{guild.Name}**  for **{TimeHelpers.TimeToPrettyFormat((TimeSpan)(muteDetails.ExpireTime - muteDetails.ActionTime), false)}** but the mute was revoked early by a Moderator.");
                            }
                        }
                    }

                }

            }
            // Even if the bot failed to remove the role, it reported that failure to a log channel and thus the mute
            //  can be safely removed internally.
            await Program.db.HashDeleteAsync("mutes", targetUser.Id);

            return true;
        }


    }
}
