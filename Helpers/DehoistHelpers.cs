namespace Cliptok.Helpers
{
    public class DehoistHelpers
    {
        public const char dehoistCharacter = '\u17b5';

        public static string DehoistName(string origName)
        {
            if (origName.Length == 32)
            {
                origName = origName[0..^1];
            }
            return dehoistCharacter + origName;
        }

        public static async Task<bool> CheckAndDehoistMemberAsync(DiscordMember targetMember)
        {

            if (
                !(
                    targetMember.DisplayName[0] != dehoistCharacter
                    && (
                        Program.cfgjson.AutoDehoistCharacters.Contains(targetMember.DisplayName[0])
                        || (targetMember.Nickname != null && targetMember.Nickname[0] != targetMember.Username[0] && Program.cfgjson.SecondaryAutoDehoistCharacters.Contains(targetMember.Nickname[0]))
                        )
                ))
            {
                return false;
            }

            try
            {
                await targetMember.ModifyAsync(a =>
                {
                    a.Nickname = DehoistName(targetMember.DisplayName);
                    a.AuditLogReason = "Dehoisted";
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
