namespace Cliptok.Commands
{
    public class KickCmds
    {
        [Command("kick")]
        [TextAlias("yeet", "shoo", "goaway", "defenestrate")]
        [Description("Kicks a user, removing them from the server until they rejoin.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(DiscordPermission.KickMembers)]
        public async Task KickCmd(CommandContext ctx, [Parameter("user"), Description("The user you want to kick from the server.")] DiscordUser target, [Parameter("reason"), Description("The reason for kicking this user."), RemainingText] string reason = "No reason specified.")
        {
            if (target.IsBot)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} To prevent accidents, I won't kick bots. If you really need to do this, do it manually in Discord.");
                return;
            }

            if (ctx is TextCommandContext)
                await ctx.As<TextCommandContext>().Message.DeleteAsync();

            reason = reason.Replace("`", "\\`").Replace("*", "\\*");

            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(target.Id);
            }
            catch
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be in the server!");
                return;
            }

            if (DiscordHelpers.AllowedToMod(ctx.Member, member))
            {
                if (DiscordHelpers.AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
                {
                    await KickHelpers.KickAndLogAsync(member, reason, ctx.Member);
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Ejected} {target.Mention} has been kicked: **{reason}**");
                    if (ctx is SlashCommandContext)
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Done!", ephemeral: true);
                    return;
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I don't have permission to kick **{DiscordHelpers.UniqueUsername(target)}**!", ephemeral: true);
                    return;
                }
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You aren't allowed to kick **{DiscordHelpers.UniqueUsername(target)}**!", ephemeral: true);
                return;
            }
        }
    }
}