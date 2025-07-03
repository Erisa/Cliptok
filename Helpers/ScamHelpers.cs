using static Cliptok.Program;

namespace Cliptok.Helpers
{

    public class ScamHelpers
    {
        public static async Task<bool> UsernameCheckAsync(DiscordMember member)
        {
            var guild = homeGuild;
            if (redis.HashExists("unbanned", member.Id))
                return false;

            if (cfgjson.UsernameAPILogChannel != 0 && Environment.GetEnvironmentVariable("USERNAME_CHECK_ENDPOINT") is not null)
            {
                if (redis.SetContains("safeusernamestore", member.Username))
                {
                    discord.Logger.LogDebug("Unnecessary username check skipped for {member}", member.Username);
                }
                else
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
                                .WithAuthor($"{DiscordHelpers.UniqueUsername(member)}", null, member.AvatarUrl)
                                .AddField("Infringing name", member.Username)
                                .AddField("API Response", $"```json\n{apiResult.responseString}\n```")
                                .WithColor(new DiscordColor(0xf03916));
                            await LogChannelHelper.LogMessageAsync("username", $"{cfgjson.Emoji.Warning} {member.Mention} was flagged by the experimental username API.", embed);
                        }
                        else
                        {
                            discord.Logger.LogDebug("Experimental Username check for {member}: {status} {response}", member.Username, apiResult.statusCode, apiResult.responseString);
                            await redis.SetAddAsync("safeusernamestore", member.Username);
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
                    await BanHelpers.BanFromServerAsync(member.Id, "Automatic ban for matching patterns of common bot accounts. Please appeal if you are a human.", discord.CurrentUser.Id, guild, 7, null, default, true);
                    var embed = new DiscordEmbedBuilder()
                        .WithTimestamp(DateTime.Now)
                        .WithFooter($"User ID: {member.Id}", null)
                        .WithAuthor($"{DiscordHelpers.UniqueUsername(member)}", null, member.AvatarUrl)
                        .AddField("Infringing name", member.Username)
                        .AddField("Matching pattern", username)
                        .WithColor(new DiscordColor(0xf03916));
                    await LogChannelHelper.LogMessageAsync("investigations", $"{cfgjson.Emoji.Banned} {member.Mention} was banned for matching blocked username patterns.", embed);
                    result = true;
                    break;
                }
            }

            return result;
        }
    }
}
