namespace Cliptok.Commands
{
    public class DmRelayCmds
    {
        [Command("dmrelayblock")]
        [TextAlias("dmblock")]
        [Description("Stop a member's DMs from being relayed to the configured DM relay channel.")]
        [AllowedProcessors(typeof(TextCommandProcessor), typeof(SlashCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task DmRelayBlockCommand(CommandContext ctx, [Description("The member to stop relaying DMs from.")] DiscordUser user)
        {
            // Only function in configured DM relay channel/thread; do nothing if in wrong channel
            var logChannelId = LogChannelHelper.GetLogChannelId("dms");
            if (ctx.Channel.Id != logChannelId)
            {
                if (ctx is SlashCommandContext)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command can only be used in <#{logChannelId}>!", ephemeral: true);
                }

                return;
            }

            // Check blocklist for user
            if (await Program.redis.SetContainsAsync("dmRelayBlocklist", user.Id))
            {
                // If already in list, remove
                await Program.redis.SetRemoveAsync("dmRelayBlocklist", user.Id);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} {user.Mention} has been unblocked successfully!");
                return;
            }

            // If not in list, add
            await Program.redis.SetAddAsync("dmRelayBlocklist", user.Id);
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} {user.Mention} has been blocked. Their DMs will not appear here.");
        }
    }
}