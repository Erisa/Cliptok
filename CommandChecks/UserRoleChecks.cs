namespace Cliptok.CommandChecks
{
    public class UserRolesPresentAttribute : ContextCheckAttribute;

    public class UserRolesPresentCheck : IContextCheck<UserRolesPresentAttribute>
    {
        public async ValueTask<string?> ExecuteCheckAsync(UserRolesPresentAttribute attribute, CommandContext ctx)
        {
            return Program.cfgjson.UserRoles is null ? "A user role command was executed, but user roles are not configured in config.json." : null;
        }
    }
}
