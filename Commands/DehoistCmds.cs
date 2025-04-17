namespace Cliptok.Commands
{
    public class DehoistCmds
    {
        [Command("dehoist")]
        [Description("Dehoist a member, dropping them to the bottom of the list. Lasts until they change nickname.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task DehoistCmd(CommandContext ctx, [Parameter("member"), Description("The member to dehoist.")] DiscordUser user)
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
                return;
            }

            if (member.MemberFlags.Value.HasFlag(DiscordMemberFlags.AutomodQuarantinedUsername))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {member.Mention} is quarantined because their name is in violation of AutoMod rules! Discord will not let me dehoist them. Please change their nickname manually.", ephemeral: true);
                return;
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
                if (await Program.db.SetContainsAsync("permadehoists", discordUser.Id))
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.On} {discordUser.Mention} is permadehoisted.", mentions: false);
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Off} {discordUser.Mention} is not permadehoisted.", mentions: false);
            }
        }

        [Command("massdehoisttextcmd")]
        [TextAlias("massdehoist")]
        [Description("Dehoist everyone on the server who has a bad name. This may take a while and can exhaust rate limits.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task MassDehoist(TextCommandContext ctx)
        {
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it. This will take a while.");
            var msg = await ctx.GetResponseAsync();

            var (totalMembers, failedMembers) = await DehoistHelpers.MassDehoistAsync(ctx.Guild, ctx.User);

            _ = msg.DeleteAsync();
            await ctx.Channel.SendMessageAsync(new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Successfully dehoisted {totalMembers - failedMembers} of {totalMembers} member(s)! (Check Audit Log for details)").WithReply(ctx.Message.Id, true, false));
        }

        [Command("massundehoisttextcmd")]
        [TextAlias("massundehoist")]
        [Description("Remove the dehoist for users attached via a txt file.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task MassUndhoist(TextCommandContext ctx)
        {
            int failedCount = 0;

            if (ctx.Message.Attachments.Count == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Please upload an attachment as well.");
            }
            else
            {
                string strList;
                using (HttpClient client = new())
                {
                    strList = await client.GetStringAsync(ctx.Message.Attachments[0].Url);
                }

                var list = strList.Split(' ');

                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it. This will take a while.");
                var msg = await ctx.GetResponseAsync();

                foreach (string strID in list)
                {
                    ulong id = Convert.ToUInt64(strID);
                    DiscordMember member = default;
                    try
                    {
                        member = await ctx.Guild.GetMemberAsync(id);
                    }
                    catch (DSharpPlus.Exceptions.NotFoundException)
                    {
                        failedCount++;
                        continue;
                    }

                    if (member.DisplayName[0] == DehoistHelpers.dehoistCharacter && !member.MemberFlags.Value.HasFlag(DiscordMemberFlags.AutomodQuarantinedUsername))
                    {
                        var newNickname = member.Nickname[1..];
                        await member.ModifyAsync(a =>
                        {
                            a.Nickname = newNickname;
                            a.AuditLogReason = $"[Mass undehoist by {DiscordHelpers.UniqueUsername(ctx.User)}]";
                        }
                        );
                    }
                    else
                    {
                        failedCount++;
                    }
                }

                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully undehoisted {list.Length - failedCount} of {list.Length} member(s)! (Check Audit Log for details)");

            }
        }
    }
}