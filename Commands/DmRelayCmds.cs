namespace Cliptok.Commands
{
    public class DmRelayCmds
    {
        [Command("dmrelayblocktextcmd")]
        [TextAlias("dmrelayblock", "dmblock")]
        [Description("Stop a member's DMs from being relayed to the configured DM relay channel.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task DmRelayBlockCommand(TextCommandContext ctx, [Description("The member to stop relaying DMs from.")] DiscordUser user)
        {
            // Only function in configured DM relay channel/thread; do nothing if in wrong channel
            if (ctx.Channel.Id != LogChannelHelper.GetLogChannelId("dms")) return;

            // Check blocklist for user
            if (await Program.db.SetContainsAsync("dmRelayBlocklist", user.Id))
            {
                // If already in list, remove
                await Program.db.SetRemoveAsync("dmRelayBlocklist", user.Id);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} {user.Mention} has been unblocked successfully!");
                return;
            }

            // If not in list, add
            await Program.db.SetAddAsync("dmRelayBlocklist", user.Id);
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} {user.Mention} has been blocked. Their DMs will not appear here.");
        }
    }
}