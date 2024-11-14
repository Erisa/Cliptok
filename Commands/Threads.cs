namespace Cliptok.Commands
{
    internal class Threads
    {
        [Command("archivetextcmd")]
        [TextAlias("archive")]
        [Description("Archive the current thread or another thread.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task ArchiveCommand(TextCommandContext ctx, DiscordChannel channel = default)
        {
            if (channel == default)
                channel = ctx.Channel;

            if (channel.Type is not DiscordChannelType.PrivateThread && channel.Type is not DiscordChannelType.PublicThread && channel.Type is not DiscordChannelType.NewsThread)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {channel.Mention} is not a thread!");
                return;
            }

            var thread = (DiscordThreadChannel)channel;

            await thread.ModifyAsync(a =>
            {
                a.IsArchived = true;
                a.Locked = false;
            });
        }

        [Command("lockthreadtextcmd")]
        [TextAlias("lockthread")]
        [Description("Lock the current thread or another thread.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task LockThreadCommand(TextCommandContext ctx, DiscordChannel channel = default)
        {
            if (channel == default)
                channel = ctx.Channel;

            if (channel.Type is not DiscordChannelType.PrivateThread && channel.Type is not DiscordChannelType.PublicThread)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {channel.Mention} is not a thread!");
                return;
            }

            var thread = (DiscordThreadChannel)channel;

            await thread.ModifyAsync(a =>
            {
                a.IsArchived = true;
                a.Locked = true;
            });
        }

        [Command("unarchivetextcmd")]
        [TextAlias("unarchive")]
        [Description("Unarchive a thread")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task UnarchiveCommand(TextCommandContext ctx, DiscordChannel channel = default)
        {
            if (channel == default)
                channel = ctx.Channel;

            if (channel.Type is not DiscordChannelType.PrivateThread && channel.Type is not DiscordChannelType.PublicThread)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {channel.Mention} is not a thread!");
                return;
            }

            var thread = (DiscordThreadChannel)(channel);

            await thread.ModifyAsync(a =>
            {
                a.IsArchived = false;
                a.Locked = false;
            });
        }
    }
}
