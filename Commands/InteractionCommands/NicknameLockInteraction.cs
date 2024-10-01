using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cliptok.Commands.InteractionCommands
{
    public class NicknameLockInteraction
    {
        [Command("nicknamelock")]
        [Description("Prevent a member from changing their nickname.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermissions.ManageNicknames)]
        public class NicknameLockSlashCommands
        {
            [Command("enable")]
			[Description("Prevent a member from changing their nickname.")]
            public async Task NicknameLockEnableSlashCmd(SlashCommandContext ctx, [Parameter("member"), Description("The member to nickname lock.")] DiscordUser discordUser)
            {
                DiscordMember member = default;

                try
                {
                    member = await ctx.Guild.GetMemberAsync(discordUser.Id);
                } catch (Exception e)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to find {discordUser.Mention} as a member! Are they in the server?\n```\n{e.Message}```", ephemeral: true);
                    return;
                }

                var currentValue = await Program.db.HashGetAsync($"nicknamelock", discordUser.Id);

                if (currentValue.HasValue)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {discordUser.Mention} is already nickname locked!", mentions: false);
                } else
                {
                    await Program.db.HashSetAsync("nicknamelock", discordUser.Id, member.DisplayName);
                    var msg = $"{Program.cfgjson.Emoji.On} Nickname locked {discordUser.Mention} as `{member.DisplayName}`!";
                    await ctx.RespondAsync(msg, mentions: false);
                    await LogChannelHelper.LogMessageAsync("nicknames", msg);
                }
            }

            [Command("disable")]
			[Description("Allow a member to change their nickname again.")]
            public async Task NicknameLockDisableSlashCmd(SlashCommandContext ctx, [Parameter("member"), Description("The member to remove the nickname lock for.")] DiscordUser discordUser)
            {
                DiscordMember member = default;

                try
                {
                    member = await ctx.Guild.GetMemberAsync(discordUser.Id);
                }
                catch (Exception e)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to find {discordUser.Mention} as a member! Are they in the server?\n```\n{e.Message}```", ephemeral: true);
                    return;
                }

                var currentValue = await Program.db.HashGetAsync($"nicknamelock", discordUser.Id);

                if (currentValue.HasValue)
                {
                    await Program.db.HashDeleteAsync("nicknamelock", discordUser.Id);
                    var msg = $"{Program.cfgjson.Emoji.Off} Removed nickname lock for {discordUser.Mention}!";
                    await ctx.RespondAsync(msg, mentions: false);
                    await LogChannelHelper.LogMessageAsync("nicknames", msg);
                }
                else
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {discordUser.Mention} is not nickname locked!", mentions: false);
                }
            }

            [Command("status")]
			[Description("Check the status of nickname lock for a member.")]
            public async Task NicknameLockStatusSlashCmd(SlashCommandContext ctx, [Parameter("member"), Description("The member whose nickname lock status to check.")] DiscordUser discordUser)
            {
                if ((await Program.db.HashGetAsync("nicknamelock", discordUser.Id)).HasValue)
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.On} {discordUser.Mention} is nickname locked.", mentions: false);
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Off} {discordUser.Mention} is not nickname locked.", mentions: false);
            }
        }
    }

}
