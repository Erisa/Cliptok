namespace Cliptok.Commands.InteractionCommands
{
    internal class ThreadInteractions : ApplicationCommandModule
    {
        [SlashCommand("keepalive", "Toggle whether to keep a thread alive permanent until locked.")]
        [Description("Toggle whether or not to keep a thread alive permanently until locked.")]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task KeepaliveCommand(InteractionContext ctx, [Option("thread", "The thread to toggle, if not the current one.")] DiscordChannel channel = default)
        {
            if (channel == default)
                channel = ctx.Channel;

            if (channel.Type is not ChannelType.PrivateThread && channel.Type is not ChannelType.PublicThread)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {channel.Mention} is not a thread!", ephemeral: true);
                return;
            }

            var thread = (DiscordThreadChannel)await ctx.Client.GetChannelAsync(channel.Id);

            if (Program.db.SetContains("openthreads", thread.Id))
            {
                await Program.db.SetRemoveAsync("openthreads", thread.Id);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Off} Thread keepalive for {thread.Mention} has been **disabled**!\nThis thread will close naturally.");
            }
            else
            {
                if (thread.ThreadMetadata.IsArchived)
                {
                    await thread.ModifyAsync(thread =>
                    {
                        thread.IsArchived = false;
                    });
                }

                await Program.db.SetAddAsync("openthreads", thread.Id);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.On} Thread keepalive for {thread.Mention} has been **enabled**!\nTo archive this thread: disable keepalive, Lock the thread or use the `archive` text command.");

            }
        }
    }
}
