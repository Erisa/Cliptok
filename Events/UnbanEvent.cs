namespace Cliptok.Events
{
    internal class UnbanEvent
    {
        public static async Task OnUnban(DiscordClient client, GuildBanRemoveEventArgs e)
        {
            if (e.Guild.Id != Program.cfgjson.ServerID)
                return;

            var _ = MuteHelpers.UnmuteUserAsync(e.Member, "Unmuted due to being unbanned.", false);
        }
    }
}
