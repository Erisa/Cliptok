using static Cliptok.Program;

namespace Cliptok.Helpers
{
    public class ScamHelpers
    {
        public static async Task<bool> UsernameCheckAsync(DiscordMember member)
        {
            var guild = homeGuild;
            if (db.HashExists("unbanned", member.Id))
                return false;

            bool result = false;

            foreach (var username in badUsernames)
            {
                // emergency failsafe, for newlines and other mistaken entries
                if (username.Length < 4)
                    continue;

                if (member.Username.ToLower().Contains(username.ToLower()))
                {
                    if (autoBannedUsersCache.Contains(member.Id))
                        break;
                    IEnumerable<ulong> enumerable = autoBannedUsersCache.Append(member.Id);
                    await Bans.BanFromServerAsync(member.Id, "Automatic ban for matching patterns of common bot accounts. Please appeal if you are a human.", discord.CurrentUser.Id, guild, 7, null, default, true);
                    var embed = new DiscordEmbedBuilder()
                        .WithTimestamp(DateTime.Now)
                        .WithFooter($"User ID: {member.Id}", null)
                        .WithAuthor($"{member.Username}#{member.Discriminator}", null, member.AvatarUrl)
                        .AddField("Infringing name", member.Username)
                        .AddField("Matching pattern", username)
                        .WithColor(new DiscordColor(0xf03916));
                    var investigations = await discord.GetChannelAsync(cfgjson.InvestigationsChannelId);
                    await investigations.SendMessageAsync($"{cfgjson.Emoji.Banned} {member.Mention} was banned for matching blocked username patterns.", embed);
                    result = true;
                    break;
                }
            }

            return result;
        }

        public static async Task<bool> CheckAvatarsAsync(DiscordMember member)
        {
            if (Environment.GetEnvironmentVariable("RAVY_API_TOKEN") == null || Environment.GetEnvironmentVariable("RAVY_API_TOKEN") == "goodluckfindingone")
                return false;

            string usedHash;
            string usedUrl;

            if (member.GuildAvatarHash == null && member.AvatarHash == null)
                return false;

            // turns out checking guild avatars isnt important

            //               if (member.GuildAvatarHash != null)
            //               {
            //                   usedHash = member.GuildAvatarHash;
            //                   usedUrl = member.GuildAvatarUrl;
            //               } else
            //               {
            usedHash = member.AvatarHash;
            usedUrl = member.GetAvatarUrl(ImageFormat.Png);
            //                }

            if (usedHash.StartsWith("a_"))
                return false;

            if (db.SetContains("safeavatarstore", usedHash))
            {
                discord.Logger.LogDebug("Unnecessary avatar check skipped for {member}", member.Id);
                return false;
            }

            var (httpStatus, responseString, avatarResponse) = await APIs.AvatarAPI.CheckAvatarUrlAsync(usedUrl);

            if (httpStatus == HttpStatusCode.OK && avatarResponse is not null)
            {
                discord.Logger.LogDebug("Avatar check for {member}: {status} {response}", member.Id, httpStatus, responseString);

                if (avatarResponse.Matched && avatarResponse.Key != "logo")
                {
                    var embed = new DiscordEmbedBuilder()
                        .WithDescription($"API Response:\n```json\n{responseString}\n```")
                        .WithAuthor($"{member.Username}#{member.Discriminator}", null, usedUrl)
                        .WithFooter($"User ID: {member.Id}")
                        .WithImageUrl(await LykosAvatarMethods.UserOrMemberAvatarURL(member, member.Guild, "default", 256));

                    await badMsgLog.SendMessageAsync($"{cfgjson.Emoji.Banned} {member.Mention} has been appeal-banned for an infringing avatar.", embed);
                    await Bans.BanFromServerAsync(member.Id, "Automatic ban for matching patterns of common bot/compromised accounts. Please appeal if you are human.", discord.CurrentUser.Id, member.Guild, 7, appealable: true);
                    return true;
                }
                else if (!avatarResponse.Matched)
                {
                    await db.SetAddAsync("safeavatarstore", usedHash);
                    return false;
                }
            }
            else
            {
                discord.Logger.LogError("Avatar check for {member}: {status} {response}", member.Id, httpStatus, responseString);
            }

            return false;
        }
    }
}
