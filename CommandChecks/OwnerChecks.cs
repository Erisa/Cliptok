namespace Cliptok.CommandChecks
{
    public class IsBotOwnerAttribute : ContextCheckAttribute
    {
        public async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            if (Program.cfgjson.BotOwners.Contains(ctx.User.Id))
            {
                return true;
            }
            else
            {
                if (!help)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.NoPermissions} This command is only accessible to bot owners.");
                }
                return false;
            }
        }
    }
}
