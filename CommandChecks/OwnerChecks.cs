namespace Cliptok.CommandChecks
{
    public class IsBotOwnerAttribute : ContextCheckAttribute;

    public class IsBotOwnerCheck : IContextCheck<IsBotOwnerAttribute>
    {
        public async ValueTask<string?> ExecuteCheckAsync(IsBotOwnerAttribute attribute, CommandContext ctx)
        {
            if (Program.cfgjson.BotOwners.Contains(ctx.User.Id))
            {
                return null;
            }
            else
            {
                return "Bot owner-only command was executed by a non-owner.";
            }
        }
    }
}
