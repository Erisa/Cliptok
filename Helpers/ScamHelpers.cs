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
