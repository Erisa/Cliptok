namespace Cliptok.Helpers
{
    public class MemberHelpers
    {
        public static async Task CheckAndChangeBadMemberNameAsync(DiscordMember member)
        {
            if (Program.cfgjson.AutoRenameBadNames.Any(x => x.ToLower() == member.DisplayName.ToLower()))
                await member.ModifyAsync(x =>
                {
                    x.Nickname = $"{Program.cfgjson.AutoRenameAdverbs[Program.rand.Next(Program.cfgjson.AutoRenameAdverbs.Count - 1)]} Not {member.DisplayName}";
                    x.AuditLogReason = "Automatically changing bad member name.";
                });
        }
    }
}