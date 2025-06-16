namespace Cliptok.Helpers
{
    public class KickHelpers
    {
        public async static Task KickAndLogAsync(DiscordMember target, string reason, DiscordMember moderator)
        {
            await target.RemoveAsync(reason);
            await LogChannelHelper.LogMessageAsync("mod",
                new DiscordMessageBuilder()
                    .WithContent($"{Program.cfgjson.Emoji.Ejected} {target.Mention} was kicked by {moderator.Mention}.\nReason: **{reason}**")
                    .WithAllowedMentions(Mentions.None)
            );
        }

        public async static Task<bool> SafeKickAndLogAsync(DiscordMember target, string reason, DiscordMember moderator)
        {
            try
            {
                await target.RemoveAsync(reason);
                await LogChannelHelper.LogMessageAsync("mod",
                    new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Ejected} {target.Mention} was kicked by {moderator.Mention}.\nReason: **{reason}**")
                        .WithAllowedMentions(Mentions.None)
                );
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}