namespace Cliptok.Events
{
    internal class UnbanEvent
    {
        public static async Task OnUnban(DiscordClient client, GuildBanRemovedEventArgs e)
        {
            client.Logger.LogDebug("Got unban event for {user}", e.Member.Id);
            if (e.Guild.Id != Program.cfgjson.ServerID)
                return;

            var _ = MuteHelpers.UnmuteUserAsync(e.Member, "Unmuted due to being unbanned.", false);
        }
    }
}
