﻿namespace Cliptok.Helpers
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
                    a.AuditLogReason = responsibleMod != default ? isMassDehoist ? $"[Mass dehoist by {responsibleMod.Username}#{responsibleMod.Discriminator}]" : $"[Dehoist by {responsibleMod.Username}#{responsibleMod.Discriminator}]" : "[Automatic dehoist]";
                });
                return true;
            }
            catch
            {
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

            if (discordMember.DisplayName[0] != dehoistCharacter)
            {
                // Dehoist member
                try
                {
                    await discordMember.ModifyAsync(a =>
                    {
                        a.Nickname = DehoistName(discordMember.DisplayName);
                        a.AuditLogReason =
                            $"[Permadehoist by {responsibleMod.Username}#{responsibleMod.Discriminator}]";
                    });
                }
                catch (DSharpPlus.Exceptions.UnauthorizedException)
                {
                    return (false, true);
                }
                catch
                {
                    // Add member ID to permadehoist list
                    await Program.db.SetAddAsync("permadehoists", discordUser.Id);

                    return (false, false);
                }
            }

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
                if (discordMember.DisplayName[0] == dehoistCharacter)
                {
                    var newNickname = discordMember.DisplayName[1..];
                    try
                    {
                        await discordMember.ModifyAsync(a =>
                        {
                            a.Nickname = newNickname;
                            a.AuditLogReason = $"[Permadehoist removed by {responsibleMod.Username}#{responsibleMod.Discriminator}]";
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
    }
}
