namespace Cliptok.Helpers
{
    public class MemberHelpers
    {
        public static async Task CheckAndChangeBadMemberNameAsync(DiscordMember member)
        {
            if (Program.badNicknames.Any(x => x.ToLower() == member.DisplayName.ToLower()))
                await member.ModifyAsync(x =>
                {
                    x.Nickname = $"{Program.badNicknameAdverbs[Program.rand.Next(Program.badNicknameAdverbs.Length)]} Not {member.DisplayName}";
                    x.AuditLogReason = "Automatically changing bad member name.";
                });
        }
    }
}