namespace Cliptok.Helpers
{
    public class LykosAvatarMethods
    {
        readonly static string[] validExts = { "default", "png or gif", "gif", "png", "jpg", "webp" };

        public static string MemberAvatarURL(DiscordMember member, string format = "default", int size = 4096)
        {
            string hash;
            if (member.GuildAvatarHash is null && member.AvatarHash is null)
                return member.DefaultAvatarUrl;
            else if (member.GuildAvatarHash != null)
                hash = member.GuildAvatarHash;
            else
                hash = member.AvatarHash;

            if (format == "default" || format == "png or gif")
            {
                format = hash.StartsWith("a_") ? "gif" : "png";
            }
            else if (!validExts.Any(format.Contains))
            {
                throw new ArgumentException("You supplied an invalid format, " +
                    "either give none or one of the following: `gif`, `png`, `jpg`, `webp`");
            }
            else if (format == "gif" && !hash.StartsWith("a_"))
            {
                throw new ArgumentException("The format of `gif` only applies to animated avatars.\n" +
                    "The user you are trying to lookup does not have an animated avatar.");
            }

            if (member.GuildAvatarHash != null)
                return $"https://cdn.discordapp.com/guilds/{member.Guild.Id}/users/{member.Id}/avatars/{hash}.{format}?size={size}";
            else
                return $"https://cdn.discordapp.com/avatars/{member.Id}/{member.AvatarHash}.{format}?size={size}";
        }

        public static async Task<string> UserOrMemberAvatarURL(DiscordUser user, DiscordGuild guild, string format = "default", int size = 4096)
        {
            if (!validExts.Any(format.Contains))
            {
                throw new ArgumentException("You supplied an invalid format, " +
                    "either give none or one of the following: `gif`, `png`, `jpg`, `webp`");
            }

            try
            {
                return MemberAvatarURL(await guild.GetMemberAsync(user.Id), format, size);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                string hash = user.AvatarHash;

                if (hash is null)
                    return user.DefaultAvatarUrl;

                if (format == "default" || format == "png or gif")
                {
                    format = hash.StartsWith("a_") ? "gif" : "png";
                }
                else if (format == "gif" && !hash.StartsWith("a_"))
                {
                    throw new ArgumentException("The format of `gif` only applies to animated avatars.\n" +
                        "The user you are trying to lookup does not have an animated avatar.");
                }

                return $"https://cdn.discordapp.com/avatars/{user.Id}/{user.AvatarHash}.{format}?size={size}";
            }

        }

    }
}
