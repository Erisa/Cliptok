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

            if (cfgjson.UsernameAPILogChannel != 0 && Environment.GetEnvironmentVariable("USERNAME_CHECK_ENDPOINT") != null)
            {
                if (db.SetContains("safeusernamestore", member.Username))
                {
                    discord.Logger.LogDebug("Unnecessary username check skipped for {member}", member.Username);
                } else
                {
                    var apiResult = await APIs.UsernameAPI.UsernameAPICheckAsync(member.Username);

                    if (apiResult.statusCode == HttpStatusCode.OK)
                    {
                        if (apiResult.match)
                        {
                            discord.Logger.LogDebug("Experimental Username check for {member}: {status} {response}", member.Username, apiResult.statusCode, apiResult.responseString);
                            var embed = new DiscordEmbedBuilder()
                                .WithTimestamp(DateTime.Now)
                                .WithFooter($"User ID: {member.Id}", null)
                                .WithAuthor($"{member.Username}#{member.Discriminator}", null, member.AvatarUrl)
                                .AddField("Infringing name", member.Username)
                                .AddField("API Response", $"```json\n{apiResult.responseString}\n```")
                                .WithColor(new DiscordColor(0xf03916));
                            var investigations = await discord.GetChannelAsync(cfgjson.InvestigationsChannelId);
                            await usernameAPILogChannel.SendMessageAsync($"{cfgjson.Emoji.Warning} {member.Mention} was flagged by the experimental username API.", embed);
                        }
                        else
                        {
                            discord.Logger.LogDebug("Experimental Username check for {member}: {status} {response}", member.Username, apiResult.statusCode, apiResult.responseString);
                            await db.SetAddAsync("safeusernamestore", member.Username);
                        }
                    }
                    else if (apiResult.statusCode != HttpStatusCode.OK)
                    {
                        discord.Logger.LogError("Experimental username check for {member}: {status} {response}", member.Username, (int)apiResult.statusCode, apiResult.responseString);
                    }
                    else
                    {
                        discord.Logger.LogDebug("Experimental Username check for {member}: {status} {response}", member.Username, apiResult.statusCode, apiResult.responseString);
                    }
                }
            }

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

            // turns out checking guild avatars isn't important

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
