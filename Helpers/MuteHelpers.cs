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
                    $"Mute status for {user.Username}#{user.Discriminator}",
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
                        .AddField("Muted", $"<t:{TimeHelpers.ToUnixTimestamp(mute.ActionTime)}:R>", true)
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
        public static async Task<(DiscordMessage? dmMessage, DiscordMessage? chatMessage)> MuteUserAsync(DiscordUser naughtyUser, string reason, ulong moderatorId, DiscordGuild guild, DiscordChannel channel = null, TimeSpan muteDuration = default, bool alwaysRespond = false)
        {
            bool permaMute = false;
            DateTime? actionTime = DateTime.Now;
            DiscordRole mutedRole = guild.GetRole(Program.cfgjson.MutedRole);
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
                    string fullReason = $"[Mute by {moderator.Username}#{moderator.Discriminator}]: {reason}";
                    await naughtyMember.GrantRoleAsync(mutedRole, fullReason);
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
                catch
                {
                    return output;
                }
            }

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
                    await LogChannelHelper.LogMessageAsync("mod", new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} was successfully muted for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}** by {moderator.Mention}." +
                            $"\nReason: **{reason}**" +
                            $"\nMute expires: <t:{TimeHelpers.ToUnixTimestamp(expireTime)}:R>")
                        .WithAllowedMentions(Mentions.None)
                    );
                }
            }
            catch (Exception ex)
            {
                Program.discord.Logger.LogError(ex, "thing");
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
                        output.dmMessage = await naughtyMember.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} You have been muted in **{guild.Name}** for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}**!" +
                            $"\nReason: **{reason}**" +
                            $"\nMute expires: <t:{TimeHelpers.ToUnixTimestamp(expireTime)}:R>");
                    }
                }
                catch (DSharpPlus.Exceptions.UnauthorizedException)
                {
                    // A DM failing to send isn't important, but let's put it in chat just so it's somewhere.
                    if (channel is not null)
                    {
                        if (muteDuration == default)
                            output.chatMessage = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been muted: **{reason}**");
                        else
                            output.chatMessage = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been muted for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}**: **{reason}**");
                    }
                }
            }

            if (output.chatMessage is null && channel is not null && alwaysRespond)
            {
                reason = reason.Replace("`", "\\`").Replace("*", "\\*");
                if (muteDuration == default)
                    output.chatMessage = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been muted: **{reason}**");
                else
                    output.chatMessage = await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Muted} {naughtyUser.Mention} has been muted for **{TimeHelpers.TimeToPrettyFormat(muteDuration, false)}**: **{reason}**");
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

            await Program.db.HashSetAsync("mutes", naughtyUser.Id, JsonConvert.SerializeObject(newMute));

            return output;
        }

        public static async Task<bool> UnmuteUserAsync(DiscordUser targetUser, string reason = "", bool manual = true)
        {
            var muteDetailsJson = await Program.db.HashGetAsync("mutes", targetUser.Id);
            bool success = false;
            DiscordGuild guild = await Program.discord.GetGuildAsync(Program.cfgjson.ServerID);

            // todo: store per-guild
            DiscordRole mutedRole = guild.GetRole(Program.cfgjson.MutedRole);
            DiscordMember member = default;
            try
            {
                member = await guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException ex)
            {
                Program.discord.Logger.LogWarning(eventId: Program.CliptokEventID, exception: ex, message: "Failed to unmute {user} in {servername} because they weren't in the server.", $"{targetUser.Username}#{targetUser.Discriminator}", guild.Name);
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
                    await member.RevokeRoleAsync(role: mutedRole, reason);
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
                    await member.TimeoutAsync(until: null, reason: reason);
                }
                catch (Exception ex)
                {
                    Program.discord.Logger.LogError(message: "Error occurred trying to remove Timeout from {user}", args: member.Id, exception: ex, eventId: Program.CliptokEventID);
                }

                if (success)
                {
                    await LogChannelHelper.LogMessageAsync("mod", new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Information} Successfully unmuted {targetUser.Mention}!").WithAllowedMentions(Mentions.None));

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
