namespace Cliptok.CommandChecks
{
    public class IsBotOwnerAttribute : CheckBaseAttribute
    {
        public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
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
