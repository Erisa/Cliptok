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

        public static async Task<bool> CheckAndDehoistMemberAsync(DiscordMember targetMember, DiscordUser responsibleMod = default, bool isMassDehoist = false)
        {

            if (
                !(
                    targetMember.DisplayName[0] != dehoistCharacter
                    && (
                        Program.cfgjson.AutoDehoistCharacters.Contains(targetMember.DisplayName[0])
                        || (targetMember.Nickname is not null && targetMember.Nickname[0] != targetMember.Username[0] && Program.cfgjson.SecondaryAutoDehoistCharacters.Contains(targetMember.Nickname[0])
                        || (targetMember.GlobalName is not null && targetMember.Username[0] != targetMember.GlobalName[0] && (targetMember.Nickname is null || targetMember.Nickname[0] != targetMember.GlobalName[0]) && Program.cfgjson.SecondaryAutoDehoistCharacters.Contains(targetMember.GlobalName[0])))
                        )
                ))
            {
                if (targetMember.DisplayName[0] == dehoistCharacter && targetMember.DisplayName.Length == 1)
                {
                    await targetMember.ModifyAsync(a =>
                    {
                        a.Nickname = DehoistName(targetMember.Username);
                        a.AuditLogReason = responsibleMod != default ? isMassDehoist ? $"[Mass dehoist by {DiscordHelpers.UniqueUsername(responsibleMod)}]" : $"[Dehoist by {DiscordHelpers.UniqueUsername(responsibleMod)}]" : "[Automatic dehoist]";
                    });
                    return true;
                }

                return false;
            }
            
            if (targetMember.MemberFlags.Value.HasFlag(DiscordMemberFlags.AutomodQuarantinedUsername))
                return false;

            try
            {
                await targetMember.ModifyAsync(a =>
                {
                    a.Nickname = DehoistName(targetMember.DisplayName);
                    a.AuditLogReason = responsibleMod != default ? isMassDehoist ? $"[Mass dehoist by {DiscordHelpers.UniqueUsername(responsibleMod)}]" : $"[Dehoist by {DiscordHelpers.UniqueUsername(responsibleMod)}]" : "[Automatic dehoist]";
                });
                return true;
            }
            catch (Exception ex)
            {
                if (ex is DSharpPlus.Exceptions.BadRequestException)
                    Program.discord.Logger.LogInformation(Program.CliptokEventID, "Failed to dehoist member {memberId}! Discord said Bad Request. If this member's nickname is in violation of AutoMod rules, this is expected!", targetMember.Id);
                return false;
            }
        }

        public static async Task<(bool success, bool isPermissionError)> PermadehoistMember(DiscordUser discordUser, DiscordUser responsibleMod, DiscordGuild guild)
        {
            // If member is already in permadehoist list, fail
            if (await Program.db.SetContainsAsync("permadehoists", discordUser.Id))
                return (false, false);

            DiscordMember discordMember;
            try
            {
                discordMember = await guild.GetMemberAsync(discordUser.Id);
            }
            catch
            {
                // Add member ID to permadehoist list
                await Program.db.SetAddAsync("permadehoists", discordUser.Id);

                return (true, false);
            }

            // If member is dehoisted already, but NOT permadehoisted, skip updating nickname.

            // If member is not dehoisted
            if (discordMember.DisplayName[0] != dehoistCharacter && !discordMember.MemberFlags.Value.HasFlag(DiscordMemberFlags.AutomodQuarantinedUsername))
            {
                // Dehoist member
                try
                {
                    await discordMember.ModifyAsync(a =>
                    {
                        a.Nickname = DehoistName(discordMember.DisplayName);
                        a.AuditLogReason =
                            $"[Permadehoist by {DiscordHelpers.UniqueUsername(responsibleMod)}]";
                    });
                }
                catch (DSharpPlus.Exceptions.UnauthorizedException)
                {
                    return (false, true);
                }
                catch
                {
                    // On failure, add member ID to permadehoist list anyway
                    await Program.db.SetAddAsync("permadehoists", discordUser.Id);

                    return (false, false);
                }
            }

            // On success or if member is already dehoisted, just add member ID to permadehoist list
            await Program.db.SetAddAsync("permadehoists", discordUser.Id);

            return (true, false);
        }

        public static async Task<(bool success, bool isPermissionError)> UnpermadehoistMember(DiscordUser discordUser, DiscordUser responsibleMod, DiscordGuild guild)
        {
            // If member is not dehoisted and is not in permadehoist list, fail
            if (!await Program.db.SetContainsAsync("permadehoists", discordUser.Id))
                return (false, false);

            // Remove member ID from permadehoist list
            await Program.db.SetRemoveAsync("permadehoists", discordUser.Id);

            if (guild is not null)
            {
                DiscordMember discordMember;
                try
                {
                    discordMember = await guild.GetMemberAsync(discordUser.Id);
                }
                catch
                {
                    return (true, false);
                }

                // Un-dehoist member
                if (discordMember.DisplayName[0] == dehoistCharacter && !discordMember.MemberFlags.Value.HasFlag(DiscordMemberFlags.AutomodQuarantinedUsername))
                {
                    var newNickname = discordMember.DisplayName[1..];
                    try
                    {
                        await discordMember.ModifyAsync(a =>
                        {
                            a.Nickname = newNickname;
                            a.AuditLogReason = $"[Permadehoist removed by {DiscordHelpers.UniqueUsername(responsibleMod)}]";
                        });
                    }
                    catch
                    {
                        return (false, true);
                    }
                }
            }

            return (true, false);
        }

        public static async Task<(bool success, bool isPermissionError, bool isDehoist)> TogglePermadehoist(DiscordUser discordUser, DiscordUser responsibleMod, DiscordGuild guild)
        {
            if (await Program.db.SetContainsAsync("permadehoists", discordUser.Id))
            {
                // Member is dehoisted; un-permadehoist
                var (success, isPermissionError) = await UnpermadehoistMember(discordUser, responsibleMod, guild);
                return (success, isPermissionError, false);
            }
            else
            {
                // Member is not permadehoisted; permadehoist
                var (success, isPermissionError) = await PermadehoistMember(discordUser, responsibleMod, guild);
                return (success, isPermissionError, true);
            }
        }

        public static async Task<(int totalMembers, int failedMembers)> MassDehoistAsync(DiscordGuild guild)
        {
            var discordMembers = await guild.GetAllMembersAsync().ToListAsync();
            int failedCount = 0;

            foreach (DiscordMember discordMember in discordMembers)
            {
                bool success = await CheckAndDehoistMemberAsync(discordMember, Program.discord.CurrentUser, true);
                if (!success)
                    failedCount++;
            }
            return (discordMembers.Count, failedCount);
        }
    }
}
