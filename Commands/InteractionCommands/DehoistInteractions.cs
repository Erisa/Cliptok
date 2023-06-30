namespace Cliptok.Commands.InteractionCommands
{
    internal class DehoistInteractions : ApplicationCommandModule
    {
        [SlashCommand("dehoist", "Dehoist a member, dropping them to the bottom of the list. Lasts until they change nickname.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.Moderator), SlashCommandPermissions(Permissions.ManageNicknames)]
        public async Task DehoistSlashCmd(InteractionContext ctx, [Option("member", "The member to dehoist.")] DiscordUser user)
        {
            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(user.Id);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to find {user.Mention} as a member! Are they in the server?", ephemeral: true);
                return;
            }

            if (member.DisplayName[0] == DehoistHelpers.dehoistCharacter)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {member.Mention} is already dehoisted!", ephemeral: true);
            }

            try
            {
                await member.ModifyAsync(a =>
                {
                    a.Nickname = DehoistHelpers.DehoistName(member.DisplayName);
                    a.AuditLogReason = $"[Dehoist by {DiscordHelpers.UniqueUsername(ctx.User)}]";
                });
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to dehoist {member.Mention}! Do I have permission?", ephemeral: true);
                return;
            }
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfuly dehoisted {member.Mention}!", mentions: false);
        }

        [SlashCommandGroup("permadehoist", "Permanently/persistently dehoist members.", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), SlashCommandPermissions(Permissions.ManageNicknames)]
        public class PermadehoistSlashCommands
        {
            [SlashCommand("enable", "Permanently dehoist a member. They will be automatically dehoisted until disabled.")]
            public async Task PermadehoistEnableSlashCmd(InteractionContext ctx, [Option("member", "The member to permadehoist.")] DiscordUser discordUser)
            {
                var (success, isPermissionError) = await DehoistHelpers.PermadehoistMember(discordUser, ctx.User, ctx.Guild);

                if (success)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.On} Successfully permadehoisted {discordUser.Mention}!", mentions: false);

                if (!success & !isPermissionError)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {discordUser.Mention} is already permadehoisted!", mentions: false);

                if (!success && isPermissionError)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to permadehoist {discordUser.Mention}!", mentions: false);
            }

            [SlashCommand("disable", "Disable permadehoist for a member.")]
            public async Task PermadehoistDisableSlashCmd(InteractionContext ctx, [Option("member", "The member to remove the permadehoist for.")] DiscordUser discordUser)
            {
                var (success, isPermissionError) = await DehoistHelpers.UnpermadehoistMember(discordUser, ctx.User, ctx.Guild);

                if (success)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Off} Successfully removed the permadehoist for {discordUser.Mention}!", mentions: false);

                if (!success & !isPermissionError)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {discordUser.Mention} isn't permadehoisted!", mentions: false);

                if (!success && isPermissionError)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to remove the permadehoist for {discordUser.Mention}!", mentions: false);
            }

            [SlashCommand("status", "Check the status of permadehoist for a member.")]
            public async Task PermadehoistStatusSlashCmd(InteractionContext ctx, [Option("member", "The member whose permadehoist status to check.")] DiscordUser discordUser)
            {
                if (await Program.db.SetContainsAsync("permadehoists", discordUser.Id))
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.On} {discordUser.Mention} is permadehoisted.", mentions: false);
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Off} {discordUser.Mention} is not permadehoisted.", mentions: false);
            }
        }
    }
}
