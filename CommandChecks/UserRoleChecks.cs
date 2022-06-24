namespace Cliptok.CommandChecks
{
    public class UserRolesPresentAttribute : CheckBaseAttribute
    {
        public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            return Program.cfgjson.UserRoles != null;
        }
    }
}
