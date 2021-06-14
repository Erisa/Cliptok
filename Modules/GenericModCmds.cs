using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Cliptok.Modules
{

    public class ModCmds : BaseCommandModule
    {
        public const char dehoistCharacter = '\u17b5';
        public bool ongoingLockdown = false;

    public static async Task<bool> BanFromServerAsync(ulong targetUserId, string reason, ulong moderatorId, DiscordGuild guild, int deleteDays = 7, DiscordChannel channel = null, TimeSpan banDuration = default, bool appealable = false)
        {
            DiscordUser naughtyUser = await Program.discord.GetUserAsync(targetUserId);
            bool permaBan = false;
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);
            DiscordRole mutedRole = guild.GetRole(Program.cfgjson.MutedRole);
            DateTime? expireTime = DateTime.Now + banDuration;
            DiscordMember moderator = await guild.GetMemberAsync(moderatorId);

            if (banDuration == default)
            {
                permaBan = true;
                expireTime = null;
            }

            MemberPunishment newBan = new MemberPunishment()
            {
                MemberId = targetUserId,
                ModId = moderatorId,
                ServerId = guild.Id,
                ExpireTime = expireTime
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
                    await targetMember.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} You have been banned from **{guild.Name}** for {Warnings.TimeToPrettyFormat(banDuration, false)}!\nReason: **{reason}**");
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
                    logOut = $"{Program.cfgjson.Emoji.Banned} <@{targetUserId}> was permanently banned by `{moderator.Username}#{moderator.Discriminator}` (`{moderatorId}`).\nReason: **{reason}**";
                }
                else
                {
                    logOut = $"{Program.cfgjson.Emoji.Banned} <@{targetUserId}> was banned for {Warnings.TimeToPrettyFormat(banDuration, false)} by `{moderator.Username}#{moderator.Discriminator}` (`{moderatorId}`).\nReason: **{reason}**";
                }
                await logChannel.SendMessageAsync(logOut);

                logOut += $"\nChannel: {channel.Mention}";
                foreach (KeyValuePair<ulong, DiscordChannel> potentialChannelPair in guild.Channels)
                {
                    var potentialChannel = potentialChannelPair.Value;
                    if (potentialChannel.Type == ChannelType.Text && potentialChannel.Topic != null && potentialChannel.Topic.EndsWith($"User ID: {targetUserId}")) {
                        await potentialChannel.SendMessageAsync(logOut);
                        break;
                    }
                }

            }
            catch
            {
                return false;
            }
            return true;

        }

        public static async Task UnbanFromServerAsync(DiscordGuild targetGuild, ulong targetUserId)
        {
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);

            try
            {
                DiscordUser user = await Program.discord.GetUserAsync(targetUserId);
                await targetGuild.UnbanMemberAsync(user, "Temporary ban expired");
            }
            catch
            {
                await logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} Attempt to unban <@{targetUserId}> failed!\nMaybe they were already unbanned?");
            }
            // Even if the bot failed to unban, it reported that failure to a log channel and thus the ban record
            //  can be safely removed internally.
            await Program.db.HashDeleteAsync("bans", targetUserId);
        }

        public static async Task<bool> CheckBansAsync()
        {
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);
            Dictionary<string, MemberPunishment> banList = Program.db.HashGetAll("bans").ToDictionary(
                x => x.Name.ToString(),
                x => JsonConvert.DeserializeObject<MemberPunishment>(x.Value)
            );
            if (banList == null | banList.Keys.Count == 0)
                return false;
            else
            {
                // The success value will be changed later if any of the unmutes are successful.
                bool success = false;
                foreach (KeyValuePair<string, MemberPunishment> entry in banList)
                {
                    MemberPunishment banEntry = entry.Value;
                    DiscordGuild targetGuild = await Program.discord.GetGuildAsync(banEntry.ServerId);
                    if (DateTime.Now > banEntry.ExpireTime)
                    {
                        targetGuild = await Program.discord.GetGuildAsync(banEntry.ServerId);
                        await UnbanFromServerAsync(targetGuild, banEntry.MemberId);
                        success = true;

                    }

                }
#if DEBUG
                Console.WriteLine($"Checked bans at {DateTime.Now} with result: {success}");
#endif
                return success;
            }
        }


        // If invoker is allowed to mod target.
        public static bool AllowedToMod(DiscordMember invoker, DiscordMember target)
        {
            return GetHier(invoker) > GetHier(target);
        }

        public static int GetHier(DiscordMember target)
        {
            return target.IsOwner ? int.MaxValue : (target.Roles.Count() == 0 ? 0 : target.Roles.Max(x => x.Position));
        }

        [Command("lockdown")]
        [Aliases("lock")]
        [Description("Locks the current channel, preventing any new messages. See also: unlock")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod), RequireBotPermissions(Permissions.ManageChannels)]
        public async Task LockdownCommand(CommandContext ctx, [RemainingText] string reason = "")
        {
            var currentChannel = ctx.Channel;
            if (!Program.cfgjson.LockdownEnabledChannels.Contains(currentChannel.Id))
            {
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} You can't lock or unlock this channel!\nIf this is in error, add its ID (`{currentChannel.Id}`) to the lockdown whitelist.");
                return;
            }

            if (ongoingLockdown)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} A mass lockdown or unlock is already ongoing. Refusing your request. sorry.");
                return;
            }

            if (reason == "all")
            {
                ongoingLockdown = true;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it, please hold...");
                foreach(var chanID in Program.cfgjson.LockdownEnabledChannels)
                {
                    try
                    {
                        var channel = await ctx.Client.GetChannelAsync(chanID);
                        await channel.AddOverwriteAsync(ctx.Guild.CurrentMember, Permissions.SendMessages, Permissions.None, "Failsafe 1 for Lockdown: ");
                        await channel.AddOverwriteAsync(ctx.Guild.GetRole(Program.cfgjson.ModRole), Permissions.SendMessages, Permissions.None, "Failsafe 2 for Lockdown");
                        await channel.AddOverwriteAsync(ctx.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages, "Lockdown command");
                        await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Locked} This channel has been locked by a Moderator.");
                    } catch
                    {

                    }

                }
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Done!");
                ongoingLockdown = false;
                return;

            }

            await ctx.Message.DeleteAsync();

            await currentChannel.AddOverwriteAsync(ctx.Guild.CurrentMember, Permissions.SendMessages, Permissions.None, "Failsafe 1 for Lockdown");
            await currentChannel.AddOverwriteAsync(ctx.Guild.GetRole(Program.cfgjson.ModRole), Permissions.SendMessages, Permissions.None, "Failsafe 2 for Lockdown");
            await currentChannel.AddOverwriteAsync(ctx.Guild.EveryoneRole, Permissions.None, Permissions.SendMessages, "Lockdown command");

            if (reason == "")
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Locked} This channel has been locked by a Moderator.");
            else
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Locked} This channel has been locked: **{reason}**");
        ;}

        public async Task<bool> UnlockChannel(DiscordChannel discordChannel, DiscordMember discordMember)
        {
            bool success = false;
            var permissions = discordChannel.PermissionOverwrites.ToArray();
            foreach (var permission in permissions)
            {
                if (permission.Type == DSharpPlus.OverwriteType.Role)
                {
                    var role = await permission.GetRoleAsync();
                    if (
                        (role == discordChannel.Guild.EveryoneRole
                        && permission.Denied == Permissions.SendMessages)
                        ||
                        (role == discordChannel.Guild.GetRole(Program.cfgjson.ModRole)
                        && permission.Allowed == Permissions.SendMessages
                        )
                        )
                    {
                        success = true;
                        await permission.DeleteAsync();
                    }
                }
                else
                {
                    var member = await permission.GetMemberAsync();
                    if ((member == discordMember || member == discordChannel.Guild.CurrentMember) && permission.Allowed == Permissions.SendMessages)
                    {
                        success = true;
                        await permission.DeleteAsync();
                    }

                }
            }
            if (success)
            {
                await discordChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Unlock} This channel has been unlocked!");
            } else
            {
                await discordChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} This channel is not locked.");
            }
            return success;
        }

        [Command("unlock")]
        [Description("Unlocks a previously locked channel. See also: lockdown")]
        [Aliases("unlockdown"), HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod), RequireBotPermissions(Permissions.ManageChannels)]
        public async Task UnlockCommand(CommandContext ctx, [RemainingText] string reason = "")
        {
            var currentChannel = ctx.Channel;
            if (!Program.cfgjson.LockdownEnabledChannels.Contains(currentChannel.Id))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Denied} You can't lock or unlock this channel!\nIf this is in error, add its ID (`{currentChannel.Id}`) to the lockdown whitelist.");
                return;
            }

            if (ongoingLockdown)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} A mass lockdown or unlock is already ongoing. Refusing your request. sorry.");
                return;
            }

            bool success = false;
            if (reason == "all")
            {
                ongoingLockdown = true;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it, please hold...");
                foreach (var chanID in Program.cfgjson.LockdownEnabledChannels)
                {
                    try
                    {
                        success = false;
                        currentChannel = await ctx.Client.GetChannelAsync(chanID);
                        await UnlockChannel(currentChannel, ctx.Member);
                    }
                    catch
                    {

                    }
                }
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Done!");
                ongoingLockdown = false;
                return;
            }

            success = await UnlockChannel(currentChannel, ctx.Member);
        }

        [Command("ban")]
        [Aliases("tempban")]
        [Description("Bans a user that you have permssion to ban, deleting all their messages in the process. See also: bankeep.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod), RequirePermissions(Permissions.BanMembers)]
        public async Task BanCmd(CommandContext ctx,
            [Description("The user you wish to ban. Accepts many formats")] DiscordUser targetMember,
            [RemainingText, Description("The time and reason for the ban. e.g. '14d trolling'")] string timeAndReason = "No reason specified.")
        {
            bool appealable = false;
            bool timeParsed = false;

            TimeSpan banDuration = default;
            string possibleTime = timeAndReason.Split(' ').First();
            try
            {
                banDuration = HumanDateParser.HumanDateParser.Parse(possibleTime).Subtract(ctx.Message.Timestamp.DateTime);
                timeParsed = true;
            } catch
            {
                // keep default
            }

            string reason = timeAndReason;

            if (timeParsed)
            {
                int i = reason.IndexOf(" ") + 1;
                reason = reason.Substring(i);
            }

            if (timeParsed && possibleTime == reason)
                reason = "No reason specified.";

            if (reason.Length > 6 && reason.Substring(0, 7) == "appeal ")
            {
                appealable = true;
                reason = reason[7..^0];
            }

            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(targetMember.Id);
            }
            catch
            {
                member = null;
            }

            if (member == null)
            {
                await ctx.Message.DeleteAsync();
                await BanFromServerAsync(targetMember.Id, reason, ctx.User.Id, ctx.Guild, 7, ctx.Channel, banDuration, appealable);
            }
            else
            {
                if (AllowedToMod(ctx.Member, member))
                {
                    if (AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
                    {
                        await ctx.Message.DeleteAsync();
                        await BanFromServerAsync(targetMember.Id, reason, ctx.User.Id, ctx.Guild, 7, ctx.Channel, banDuration, appealable);
                    }
                    else
                    {
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to ban **{targetMember.Username}#{targetMember.Discriminator}**!");
                        return;
                    }
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You don't have permission to ban **{targetMember.Username}#{targetMember.Discriminator}**!");
                    return;
                }
            }
            reason = reason.Replace("`", "\\`").Replace("*", "\\*");
            if (banDuration == default)
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {targetMember.Mention} has been banned: **{reason}**");
            else
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {targetMember.Mention} has been banned for **{Warnings.TimeToPrettyFormat(banDuration, false)}**: **{reason}**");
        }

        /// I CANNOT find a way to do this as alias so I made it a separate copy of the command.
        /// Sue me, I beg you.
        [Command("bankeep")]
        [Aliases("bansave")]
        [Description("Bans a user but keeps their messages around."), HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod), RequirePermissions(Permissions.BanMembers)]
        public async Task BankeepCmd(CommandContext ctx, DiscordUser targetMember, [RemainingText] string timeAndReason = "No reason specified.")
        {
            bool appealable = false;
            bool timeParsed = false;

            TimeSpan banDuration = default;
            string possibleTime = timeAndReason.Split(' ').First();
            try
            {
                banDuration = HumanDateParser.HumanDateParser.Parse(possibleTime).Subtract(ctx.Message.Timestamp.DateTime);
                timeParsed = true;
            }
            catch
            {
                // keep default
            }

            string reason = timeAndReason;

            if (timeParsed)
            {
                int i = reason.IndexOf(" ") + 1;
                reason = reason.Substring(i);
            }

            if (timeParsed && possibleTime == reason)
                reason = "No reason specified.";

            if (reason.Length > 6 && reason.Substring(0, 7) == "appeal ")
            {
                appealable = true;
                reason = reason[7..^0];
            }

            DiscordMember member;
                try
                {
                    member = await ctx.Guild.GetMemberAsync(targetMember.Id);
                }
                catch
                {
                    member = null;
                }

                if (member == null)
                {
                    await ctx.Message.DeleteAsync();
                    await BanFromServerAsync(targetMember.Id, reason, ctx.User.Id, ctx.Guild, 0, ctx.Channel, banDuration, appealable);
                }
                else
                {
                    if (AllowedToMod(ctx.Member, member))
                    {
                        if (AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
                        {
                            await ctx.Message.DeleteAsync();
                            await BanFromServerAsync(targetMember.Id, reason, ctx.User.Id, ctx.Guild, 0, ctx.Channel, banDuration, appealable);
                        }
                        else
                        {
                            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to ban **{targetMember.Username}#{targetMember.Discriminator}**!");
                            return;
                        }
                    }
                    else
                    {
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You don't have permission to ban **{targetMember.Username}#{targetMember.Discriminator}**!");
                        return;
                    }
                }
                reason = reason.Replace("`", "\\`").Replace("*", "\\*");
                if (banDuration == default)
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {targetMember.Mention} has been banned: **{reason}**");
                else
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {targetMember.Mention} has been banned for **{Warnings.TimeToPrettyFormat(banDuration, false)}**: **{reason}**");
        }

        [Command("kick")]
        [Aliases("yeet", "shoo", "goaway")]
        [Description("Kicks a user, removing them from the server until they rejoin. Generally not very useful.")]
        [RequirePermissions(Permissions.KickMembers), HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod)]
        public async Task Kick(CommandContext ctx, DiscordUser target, [RemainingText] string reason = "No reason specified.")
        {
            reason = reason.Replace("`", "\\`").Replace("*", "\\*");

            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(target.Id);
            }
            catch
            {
                await ctx.Channel.SendMessageAsync($"{ctx.User.Mention}, that user doesn't appear to be in the server.");
                return;
            }

            if (AllowedToMod(ctx.Member, member))
            {
                if (AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
                {
                    await ctx.Message.DeleteAsync();
                    await KickAndLogAsync(member, reason, ctx.Member);
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Ejected} {target.Mention} has been kicked: **{reason}**");
                    return;
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to kick **{target.Username}#{target.Discriminator}**!");
                    return;
                }
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You aren't allowed to kick **{target.Username}#{target.Discriminator}**!");
                return;
            }
        }

        [Command("unban")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod), RequirePermissions(Permissions.BanMembers)]
        public async Task UnmuteCmd(CommandContext ctx, DiscordUser targetUser)
        {
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);

            if ((await Program.db.HashExistsAsync("bans", targetUser.Id)))
            {
                await UnbanUserAsync(ctx.Guild, targetUser);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Unbanned} Successfully unbanned **{targetUser.Username}#{targetUser.Discriminator}**.");
            }
            else
            {
                bool banSuccess = await UnbanUserAsync(ctx.Guild, targetUser);
                if (banSuccess)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Unbanned} Successfully unbanned **{targetUser.Username}#{targetUser.Discriminator}**.");
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.Member.Mention}, that user doesn't appear to be banned, *and* an error ocurred while attempting to unban them anyway.\nPlease contact the bot owner if this wasn't expected, the error has been logged.");
                }
            }
        }

        [Command("dehoist")]
        [Description("Adds an invisible character to someones nickname that drops them to the bottom of the member list. Accepts multiple members.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialMod)]
        public async Task Dehoist(CommandContext ctx, [Description("List of server members to dehoist")] params DiscordMember[] discordMembers)
        {
            if (discordMembers.Length == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You need to tell me who to dehoist!");
                return;
            }
            else if (discordMembers.Length == 1)
            {
                if (discordMembers[0].DisplayName[0] == dehoistCharacter)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {discordMembers[0].Mention} is already dehoisted!");
                    return;
                }
                try
                {
                    await discordMembers[0].ModifyAsync(a =>
                    {
                        a.Nickname = DehoistName(discordMembers[0].DisplayName);
                    });
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully dehoisted {discordMembers[0].Mention}!");
                }
                catch
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to dehoist {discordMembers[0].Mention}!");
                }
                return;
            }

            var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it...");
            int failedCount = 0;

            foreach (DiscordMember discordMember in discordMembers)
            {
                var origName = discordMember.DisplayName;
                if (origName[0] == '\u17b5')
                {
                    failedCount++;
                }
                else
                {
                    try
                    {
                        await discordMember.ModifyAsync(a =>
                        {
                            a.Nickname = DehoistName(origName);
                        });
                    }
                    catch
                    {
                        failedCount++;
                    }
                }

            }
            await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully dehoisted {discordMembers.Count() - failedCount} of {discordMembers.Count()} member(s)! (Check Audit Log for details)");
        }

        [Command("massdehoist")]
        [Description("Dehoist everyone on the server who has a bad name. WARNING: This is a computationally expensive operation.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod)]
        public async Task MassDehoist(CommandContext ctx)
        {
            var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it. This will take a while.");
            var discordMembers = await ctx.Guild.GetAllMembersAsync();
            int failedCount = 0;

            foreach (DiscordMember discordMember in discordMembers)
            {
                bool success = await Program.CheckAndDehoistMemberAsync(discordMember);
                if (!success)
                    failedCount++;
            }
            await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully dehoisted {discordMembers.Count() - failedCount} of {discordMembers.Count()} member(s)! (Check Audit Log for details)");
        }

        [Command("massundehoist")]
        [Description("Remove the dehoist for users attached via a txt file.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod)]
        public async Task MassUndhoist(CommandContext ctx)
        {
            int failedCount = 0;

            if (ctx.Message.Attachments.Count == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Please upload an attachment as well.");
            } else
            {
                string strList;
                using (WebClient client = new WebClient())
                {
                    strList = client.DownloadString(ctx.Message.Attachments[0].Url);
                }

                var list = strList.Split(' ');

                var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it. This will take a while.");

                foreach (string strID in list)
                {
                    ulong id = Convert.ToUInt64(strID);
                    DiscordMember member = default;
                    try
                    {
                        member = await ctx.Guild.GetMemberAsync(id);
                    } catch (DSharpPlus.Exceptions.NotFoundException)
                    {
                        failedCount++;
                        continue;
                    }

                    if(member.Nickname != null && member.Nickname[0] == dehoistCharacter)
                    {
                        var newNickname = member.Nickname.Substring(1);
                        await member.ModifyAsync(a =>
                        {
                            a.Nickname = newNickname;
                        }
                        );
                    }
                    else
                    {
                        failedCount++;
                    }
                }

                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully undehoisted {list.Count() - failedCount} of {list.Count()} member(s)! (Check Audit Log for details)");

            }
        }


        public static string DehoistName(string origName)
        {
            if (origName.Length == 32)
            {
                origName = origName[0..^1];
            }
            return dehoistCharacter + origName;
        }

        public async static Task<bool> UnbanUserAsync(DiscordGuild guild, DiscordUser target)
        {
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);
            try
            {
                await guild.UnbanMemberAsync(target);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
            await logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Unbanned} Successfully unbanned <@{target.Id}>!");
            await Program.db.HashDeleteAsync("bans", target.Id.ToString());
            return true;
        }

        public async static Task KickAndLogAsync(DiscordMember target, string reason, DiscordMember moderator)
        {
            DiscordChannel logChannel = await Program.discord.GetChannelAsync(Program.cfgjson.LogChannel);
            await target.RemoveAsync(reason);
            await logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Ejected} <@{target.Id}> was kicked by `{moderator.Username}#{moderator.Discriminator}` (`{moderator.Id}`).\nReason: **{reason}**");
        }

        [Command("tellraw")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod)]
        public async Task TellRaw(CommandContext ctx, DiscordChannel discordChannel, [RemainingText] string output)
        {
            try
            {
                await discordChannel.SendMessageAsync(output);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Your dumb message didn't want to send. Congrats, I'm proud of you.");
                return;
            }
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} I sent your stupid message to {discordChannel.Mention}.");

        }

        public class Reminder
        {
            [JsonProperty("userID")]
            public ulong UserID { get; set; }

            [JsonProperty("channelID")]
            public ulong ChannelID { get; set; }

            [JsonProperty("messageLink")]
            public string MessageLink { get; set; }

            [JsonProperty("reminderText")]
            public string ReminderText { get; set; }

            [JsonProperty("reminderTime")]
            public DateTime ReminderTime { get; set; }

            [JsonProperty("originalTime")]
            public DateTime OriginalTime { get; set; }
        }

        [Command("remindme")]
        [Aliases("reminder")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Tier4)]
        public async Task RemindMe(CommandContext ctx, string timetoParse, [RemainingText] string reminder)
        {
            DateTime t = HumanDateParser.HumanDateParser.Parse(timetoParse);
            if (t <= DateTime.Now)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Time can't be in the past!");
                return;
            }
            else if (t < (DateTime.Now + TimeSpan.FromSeconds(59)))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Time must be at least a minute in the future!");
                return;
            }

            var reminderObject = new Reminder()
            {
                UserID = ctx.User.Id,
                ChannelID = ctx.Channel.Id,
                MessageLink = $"https://discord.com/channels/{ctx.Guild.Id}/{ctx.Channel.Id}/{ctx.Message.Id}",
                ReminderText = reminder,
                ReminderTime = t,
                OriginalTime = DateTime.Now
            };

            await Program.db.ListRightPushAsync("reminders", JsonConvert.SerializeObject(reminderObject));
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} I'll try my best to remind you about that at: `{t}` (In roughly **{Warnings.TimeToPrettyFormat(t.Subtract(ctx.Message.Timestamp.DateTime), false)}**)");
        }

        public static async Task<bool> CheckRemindersAsync(bool includeRemutes = false)
        {
            bool success = false;
            foreach (var reminder in Program.db.ListRange("reminders", 0, -1))
            {
                var guild = await Program.discord.GetGuildAsync(Program.cfgjson.ServerID);
                var reminderObject = JsonConvert.DeserializeObject<Reminder>(reminder);
                if (reminderObject.ReminderTime <= DateTime.Now)
                {
                    var user = await Program.discord.GetUserAsync(reminderObject.UserID);
                    DiscordChannel channel = null;
                    try
                    {
                        channel = await Program.discord.GetChannelAsync(reminderObject.ChannelID);
                    }
                    catch
                    {
                        // channel likely doesnt exist
                    }
                    if (channel == null)
                    {
                        var member = await guild.GetMemberAsync(reminderObject.UserID);
                        if (Warnings.GetPermLevel(member) >= ServerPermLevel.TrialMod)
                        {
                            channel = await Program.discord.GetChannelAsync(Program.cfgjson.HomeChannel);
                        }
                        else
                        {
                            channel = await Program.discord.GetChannelAsync(240528256292356096);
                        }
                    }

                    await Program.db.ListRemoveAsync("reminders", reminder);
                    success = true;

                    var embed = new DiscordEmbedBuilder()
                    .WithDescription(reminderObject.ReminderText)
                    .WithColor(new DiscordColor(0xD084))
                    .WithFooter(
                        "Reminder was set",
                        null
                    )
                    .WithTimestamp(reminderObject.OriginalTime)
                    .WithAuthor(
                        $"Reminder from {Warnings.TimeToPrettyFormat(DateTime.Now.Subtract(reminderObject.OriginalTime), true)}",
                        null,
                        user.AvatarUrl
                    )
                    .AddField("Context", $"[`Jump to context`]({reminderObject.MessageLink})", true);

                    await channel.SendMessageAsync($"<@!{reminderObject.UserID}>, you asked to be reminded of something:", embed);
                }

            }
            return success;
        }



        [Group("debug")]
        [Aliases("troubleshoot", "unbug", "bugn't", "helpsomethinghasgoneverywrong")]
        [Description("Commands and things for fixing the bot in the unlikely event that it breaks a bit.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Mod)]
        class DebugCmds : BaseCommandModule
        {
            [Command("gif")]
            [Description("Debug GIF properties.")]
            public async Task GifDebug(CommandContext ctx, string gifToCheck)
            {
                SeizureDetection.ImageInfo Gif = SeizureDetection.GetGifProperties(gifToCheck);
                string strOut = "**GIF information**\n";
                strOut += $"----------\nFrame count: **{Gif.FrameCount}**\n";
                strOut += $"Unique frame count: **{Gif.UniqueFrameCount}**\n";
                strOut += $"Average frame difference: **{Math.Round(Gif.AverageFrameDifference, 2)}**\n";
                strOut += $"Average frame contrast: **{Math.Round(Gif.AverageContrast, 2)}**\n";
                strOut += $"Length: **{Gif.Length}ms**\n";
                strOut += $"Frame duration: **{Math.Round(Gif.Duration, 2)}ms**\n";
                strOut += $"Framerate: **{Math.Round(Gif.FrameRate, 2)}fps**\n";
                strOut += $"Seizure-inducing: **{Gif.IsSeizureInducing}**\n";
                strOut += "----------";
                await ctx.RespondAsync(strOut);
            }

            [Command("mutes")]
            [Description("Debug the list of mutes.")]
            public async Task MuteDebug(CommandContext ctx)
            {
                string strOut = "```json";
                var muteList = Program.db.HashGetAll("mutes").ToDictionary();
                if (muteList == null | muteList.Keys.Count == 0)
                {
                    await ctx.Channel.SendMessageAsync("No mutes found in database!");
                    return;
                }
                else
                {
                    foreach (var entry in muteList)
                    {
                        strOut += $"\n{entry.Value}";
                    }
                }
                strOut += "```";
                await ctx.Channel.SendMessageAsync(strOut);
            }

            [Command("bans")]
            [Description("Debug the list of bans.")]
            public async Task BanDebug(CommandContext ctx)
            {
                string strOut = "```json";
                var muteList = Program.db.HashGetAll("bans").ToDictionary();
                if (muteList == null | muteList.Keys.Count == 0)
                {
                    await ctx.Channel.SendMessageAsync("No bans found in database!");
                    return;
                }
                else
                {
                    foreach (var entry in muteList)
                    {
                        strOut += $"\n{entry.Value}";
                    }
                }
                strOut += "```";
                await ctx.RespondAsync(strOut);
            }

            [Command("restart")]
            [RequireHomeserverPerm(ServerPermLevel.Admin), Description("Restart the bot. If not under Docker (Cliptok is, dw) this WILL exit instead.")]
            public async Task Restart(CommandContext ctx)
            {
                await ctx.RespondAsync("Now restarting bot.");
                Environment.Exit(1);
            }

            [Command("shutdown")]
            [RequireHomeserverPerm(ServerPermLevel.Admin), Description("Panics and shuts the bot down. Check the arguments for usage.")]
            public async Task Shutdown(CommandContext ctx, [Description("This MUST be set to \"I understand what I am doing\" for the command to work."), RemainingText] string verificationArgument)
            {
                if (verificationArgument == "I understand what I am doing")
                {
                    await ctx.RespondAsync("WARNING: The bot is now shutting down. This action is permanent.");
                    Environment.Exit(0);
                }
                else
                {
                    await ctx.RespondAsync("Invalid argument. Make sure you know what you are doing.");

                };

            }
            [Command("refresh")]
            public async Task Refresh(CommandContext ctx)
            {
                var msg = await ctx.RespondAsync("Checking for pending unmutes and unbans...");
                bool bans = await CheckBansAsync();
                bool mutes = await Mutes.CheckMutesAsync(true);
                bool reminders = await ModCmds.CheckRemindersAsync();
                await msg.ModifyAsync($"Unban check result: `{bans.ToString()}`\nUnmute check result: `{mutes.ToString()}`\nReminders check result: `{reminders}`");
            }
        }

    }
}
