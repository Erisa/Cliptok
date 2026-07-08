namespace Cliptok.Commands
{
    [Command("permadehoist")]
    [Description("Permanently/persistently dehoist members.")]
    [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
    [RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ManageNicknames)]
    public class PermadehoistCmds
    {
        [DefaultGroupCommand]
        [Command("toggle")]
        [Description("Toggle permadehoist status for a member.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        public async Task PermadehoistToggleCmd(CommandContext ctx, [Description("The member to permadehoist.")] DiscordUser user)
        {
            var (success, isPermissionError, isDehoist) = await DehoistHelpers.TogglePermadehoist(user, ctx.User, ctx.Guild);

            if (success)
            {
                if (isDehoist)
                {
                    await ctx.RespondAsync(new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.On} Successfully permadehoisted {user.Mention}!")
                        .WithAllowedMentions(Mentions.None));
                }
                else
                {
                    await ctx.RespondAsync(new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Off} Successfully removed the permadehoist for {user.Mention}!")
                        .WithAllowedMentions(Mentions.None));
                }
            }
            else
            {
                if (isDehoist)
                {
                    await ctx.RespondAsync(new DiscordMessageBuilder()
                        .WithContent(isPermissionError ? $"{Program.cfgjson.Emoji.Error} Failed to permadehoist {user.Mention}! Do I have permission?" : $"{Program.cfgjson.Emoji.Error} Failed to permadehoist {user.Mention}!")
                        .WithAllowedMentions(Mentions.None));
                }
                else
                {
                    await ctx.RespondAsync(new DiscordMessageBuilder()
                        .WithContent(isPermissionError ? $"{Program.cfgjson.Emoji.Error} Failed to remove the permadehoist for {user.Mention}! Do I have permission?" : $"{Program.cfgjson.Emoji.Error} Failed to remove the permadehoist for {user.Mention}!")
                        .WithAllowedMentions(Mentions.None));
                }
            }
        }

        [Command("enable")]
        [Description("Permanently dehoist a member. They will be automatically dehoisted until disabled.")]
        public async Task PermadehoistEnableSlashCmd(CommandContext ctx, [Parameter("member"), Description("The member to permadehoist.")] DiscordUser discordUser)
        {
            var (success, isPermissionError) = await DehoistHelpers.PermadehoistMember(discordUser, ctx.User, ctx.Guild);

            if (success)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.On} Successfully permadehoisted {discordUser.Mention}!", mentions: false);

            if (!success & !isPermissionError)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {discordUser.Mention} is already permadehoisted!", mentions: false);

            if (!success && isPermissionError)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to permadehoist {discordUser.Mention}!", mentions: false);
        }

        [Command("disable")]
        [Description("Disable permadehoist for a member.")]
        public async Task PermadehoistDisableSlashCmd(CommandContext ctx, [Parameter("member"), Description("The member to remove the permadehoist for.")] DiscordUser discordUser)
        {
            var (success, isPermissionError) = await DehoistHelpers.UnpermadehoistMember(discordUser, ctx.User, ctx.Guild);

            if (success)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Off} Successfully removed the permadehoist for {discordUser.Mention}!", mentions: false);

            if (!success & !isPermissionError)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {discordUser.Mention} isn't permadehoisted!", mentions: false);

            if (!success && isPermissionError)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to remove the permadehoist for {discordUser.Mention}!", mentions: false);
        }

        [Command("status")]
        [Description("Check the status of permadehoist for a member.")]
        public async Task PermadehoistStatusSlashCmd(CommandContext ctx, [Parameter("member"), Description("The member whose permadehoist status to check.")] DiscordUser discordUser)
        {
            if (await Program.redis.SetContainsAsync("permadehoists", discordUser.Id))
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.On} {discordUser.Mention} is permadehoisted.", mentions: false);
            else
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Off} {discordUser.Mention} is not permadehoisted.", mentions: false);
        }
    }
}
