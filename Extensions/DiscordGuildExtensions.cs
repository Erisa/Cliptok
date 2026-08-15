namespace Cliptok.Extensions
{
    public static class DiscordGuildExtensions
    {
        internal static List<ulong> UsersNotInServerCache = [];

        extension(DiscordGuild guild)
        {
            public async Task<DiscordMember> CheckAndGetMemberAsync(ulong userId, bool updateCache = false)
            {
                if (UsersNotInServerCache.Contains(userId))
                    return null;

                DiscordMember member;
                try
                {
                    member = await guild.GetMemberAsync(userId, updateCache);
                    UsersNotInServerCache.Remove(member.Id);
                }
                catch (DSharpPlus.Exceptions.NotFoundException)
                {
                    UsersNotInServerCache.Add(userId);
                    return null;
                }

                return member;
            }
        }
    }
}
