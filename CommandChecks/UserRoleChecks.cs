namespace Cliptok.CommandChecks
{
    public class UserRolesPresentAttribute : ContextCheckAttribute
    {
        public async Task<bool> ExecuteCheckAsync(CommandContext ctx)
        {
            return Program.cfgjson.UserRoles is not null;
        }
    }
}
