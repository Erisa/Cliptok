namespace Cliptok.Helpers
{
    public class DiscordHelpers
    {
        public static async Task<bool> SafeTyping(DiscordChannel channel)
        {
            try
            {
                await channel.TriggerTypingAsync();
                return true;
            }
            catch (Exception ex)
            {
                Program.discord.Logger.LogError(eventId: Program.CliptokEventID, exception: ex, message: "Error occurred trying to type in {channel}", args: channel.Id);
                return false;
            }
        }

        public static string MessageLink(DiscordMessage msg)
        {
            return $"https://discord.com/channels/{msg.Channel.Guild.Id}/{msg.Channel.Id}/{msg.Id}";
        }

        // If invoker is allowed to mod target.
        public static bool AllowedToMod(DiscordMember invoker, DiscordMember target)
        {
            return GetHier(invoker) > GetHier(target);
        }

        public static int GetHier(DiscordMember target)
        {
            return target.IsOwner ? int.MaxValue : (!target.Roles.Any() ? 0 : target.Roles.Max(x => x.Position));
        }

        public static async Task<DiscordMessage?> GetMessageFromReferenceAsync(MessageReference messageReference)
        {
            if (messageReference is null || messageReference.ChannelId == 0 || messageReference.MessageId == 0)
                return null;

            try
            {
                var channel = await Program.discord.GetChannelAsync(messageReference.ChannelId);
                return await channel.GetMessageAsync(messageReference.MessageId);
            }
            catch (Exception ex)
            {
                Program.discord.Logger.LogWarning(eventId: Program.CliptokEventID, exception: ex, message: "Failed to fetch message {message}-{channel}", messageReference.ChannelId, messageReference.MessageId);
                return null;
            }
        }

    }
}
