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
                Program.discord.Logger.LogError(eventId: Program.CliptokEventID, exception: ex, message: "Error ocurred trying to type in {0}", args: channel.Id);
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

    }
}
