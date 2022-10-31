namespace Cliptok.Commands
{
    internal class Dehoist : BaseCommandModule
    {
        [Command("dehoist")]
        [Description("Adds an invisible character to someone's nickname that drops them to the bottom of the member list. Accepts multiple members.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task DehoistCmd(CommandContext ctx, [Description("List of server members to dehoist")] params DiscordMember[] discordMembers)
        {
            if (discordMembers.Length == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You need to tell me who to dehoist!");
                return;
            }
            else if (discordMembers.Length == 1)
            {
                if (discordMembers[0].DisplayName[0] == DehoistHelpers.dehoistCharacter)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {discordMembers[0].Mention} is already dehoisted!");
                    return;
                }
                try
                {
                    await discordMembers[0].ModifyAsync(a =>
                    {
                        a.Nickname = DehoistHelpers.DehoistName(discordMembers[0].DisplayName);
                        a.AuditLogReason = $"[Dehoist by {ctx.User.Username}#{ctx.User.Discriminator}]";
                    });
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully dehoisted {discordMembers[0].Mention}!");
                }
                catch
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Failed to dehoist {discordMembers[0].Mention}!");
                }
                return;
            }

            var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it...");
            int failedCount = 0;

            foreach (DiscordMember discordMember in discordMembers)
            {
                var origName = discordMember.DisplayName;
                if (origName[0] == '\u17b5')
                {
                    failedCount++;
                }
                else
                {
                    try
                    {
                        await discordMember.ModifyAsync(a =>
                        {
                            a.Nickname = DehoistHelpers.DehoistName(origName);
                            a.AuditLogReason = $"[Dehoist by {ctx.User.Username}#{ctx.User.Discriminator}]";
                        });
                    }
                    catch
                    {
                        failedCount++;
                    }
                }

            }
            _ = await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully dehoisted {discordMembers.Length - failedCount} of {discordMembers.Length} member(s)! (Check Audit Log for details)");
        }

        [Command("massdehoist")]
        [Description("Dehoist everyone on the server who has a bad name. WARNING: This is a computationally expensive operation.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task MassDehoist(CommandContext ctx)
        {
            var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it. This will take a while.");
            var discordMembers = await ctx.Guild.GetAllMembersAsync();
            int failedCount = 0;

            foreach (DiscordMember discordMember in discordMembers)
            {
                bool success = await DehoistHelpers.CheckAndDehoistMemberAsync(discordMember, ctx.User, true);
                if (!success)
                    failedCount++;
            }

            _ = msg.DeleteAsync();
            await ctx.Channel.SendMessageAsync(new DiscordMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Successfully dehoisted {discordMembers.Count - failedCount} of {discordMembers.Count} member(s)! (Check Audit Log for details)").WithReply(ctx.Message.Id, true, false));
        }

        [Command("massundehoist")]
        [Description("Remove the dehoist for users attached via a txt file.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task MassUndhoist(CommandContext ctx)
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

                var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it. This will take a while.");

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

                    if (member.DisplayName[0] == DehoistHelpers.dehoistCharacter)
                    {
                        var newNickname = member.Nickname[1..];
                        await member.ModifyAsync(a =>
                        {
                            a.Nickname = newNickname;
                            a.AuditLogReason = $"[Mass undehoist by {ctx.User.Username}#{ctx.User.Discriminator}]";
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

        [Group("permadehoist")]
        [Description("Permanently/persistently dehoist members.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public class Permadehoist : BaseCommandModule
        {
            // Toggle
            [GroupCommand]
            public async Task PermadehoistToggleCmd(CommandContext ctx, [Description("The member(s) to permadehoist.")] params DiscordUser[] discordUsers)
            {
                if (discordUsers.Length == 0)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You need to tell me who to permadehoist!");
                    return;
                }

                if (discordUsers.Length == 1)
                {
                    // Toggle permadehoist for single member

                    var (success, isPermissionError, isDehoist) = await DehoistHelpers.TogglePermadehoist(discordUsers[0], ctx.User, ctx.Guild);

                    if (success)
                    {
                        if (isDehoist)
                        {
                            await ctx.RespondAsync(new DiscordMessageBuilder()
                                .WithContent($"{Program.cfgjson.Emoji.On} Successfully permadehoisted {discordUsers[0].Mention}!")
                                .WithAllowedMentions(Mentions.None));
                        }
                        else
                        {
                            await ctx.RespondAsync(new DiscordMessageBuilder()
                                .WithContent($"{Program.cfgjson.Emoji.Off} Successfully removed the permadehoist for {discordUsers[0].Mention}!")
                                .WithAllowedMentions(Mentions.None));
                        }
                    }
                    else
                    {
                        if (isDehoist)
                        {
                            await ctx.RespondAsync(new DiscordMessageBuilder()
                                .WithContent(isPermissionError ? $"{Program.cfgjson.Emoji.Error} Failed to permadehoist {discordUsers[0].Mention}! Do I have permission?" : $"{Program.cfgjson.Emoji.Error} Failed to permadehoist {discordUsers[0].Mention}!")
                                .WithAllowedMentions(Mentions.None));
                        }
                        else
                        {
                            await ctx.RespondAsync(new DiscordMessageBuilder()
                                .WithContent(isPermissionError ? $"{Program.cfgjson.Emoji.Error} Failed to remove the permadehoist for {discordUsers[0].Mention}! Do I have permission?" : $"{Program.cfgjson.Emoji.Error} Failed to remove the permadehoist for {discordUsers[0].Mention}!")
                                .WithAllowedMentions(Mentions.None));
                        }
                    }

                    return;
                }

                // Toggle permadehoist for multiple members

                var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it...");
                int failedCount = 0;

                foreach (var discordUser in discordUsers)
                {
                    var (success, _, _) = await DehoistHelpers.TogglePermadehoist(discordUser, ctx.User, ctx.Guild);

                    if (!success)
                        failedCount++;
                }
                _ = await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully toggled permadehoist for {discordUsers.Length - failedCount} of {discordUsers.Length} member(s)! (Check Audit Log for details)");
            }

            [Command("enable")]
            [Description("Permanently dehoist a member (or members). They will be automatically dehoisted until disabled.")]
            public async Task PermadehoistEnableCmd(CommandContext ctx, [Description("The member(s) to permadehoist.")] params DiscordUser[] discordUsers)
            {
                if (discordUsers.Length == 0)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You need to tell me who to permadehoist!");
                    return;
                }

                if (discordUsers.Length == 1)
                {
                    // Permadehoist single member

                    var (success, isPermissionError) = await DehoistHelpers.PermadehoistMember(discordUsers[0], ctx.User, ctx.Guild);

                    if (success)
                        await ctx.RespondAsync(new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.On} Successfully permadehoisted {discordUsers[0].Mention}!")
                            .WithAllowedMentions(Mentions.None));

                    if (!success & !isPermissionError)
                        await ctx.RespondAsync(new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Error} {discordUsers[0].Mention} is already permadehoisted!")
                            .WithAllowedMentions(Mentions.None));

                    if (!success && isPermissionError)
                        await ctx.RespondAsync(new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Error} Failed to permadehoist {discordUsers[0].Mention}!")
                            .WithAllowedMentions(Mentions.None));

                    return;
                }

                // Permadehoist multiple members

                var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it...");
                int failedCount = 0;

                foreach (var discordUser in discordUsers)
                {
                    var (success, _) = await DehoistHelpers.PermadehoistMember(discordUser, ctx.User, ctx.Guild);

                    if (!success)
                        failedCount++;
                }
                _ = await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully permadehoisted {discordUsers.Length - failedCount} of {discordUsers.Length} member(s)! (Check Audit Log for details)");
            }

            [Command("disable")]
            [Description("Disable permadehoist for a member (or members).")]
            public async Task PermadehoistDisableCmd(CommandContext ctx, [Description("The member(s) to remove the permadehoist for.")] params DiscordUser[] discordUsers)
            {
                if (discordUsers.Length == 0)
                {
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} You need to tell me who to un-permadehoist!");
                    return;
                }

                if (discordUsers.Length == 1)
                {
                    // Un-permadehoist single member

                    var (success, isPermissionError) = await DehoistHelpers.UnpermadehoistMember(discordUsers[0], ctx.User, ctx.Guild);

                    if (success)
                        await ctx.RespondAsync(new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Off} Successfully removed the permadehoist for {discordUsers[0].Mention}!")
                            .WithAllowedMentions(Mentions.None));

                    if (!success & !isPermissionError)
                        await ctx.RespondAsync(new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Error} {discordUsers[0].Mention} isn't permadehoisted!")
                            .WithAllowedMentions(Mentions.None));

                    if (!success && isPermissionError)
                        await ctx.RespondAsync(new DiscordMessageBuilder()
                            .WithContent($"{Program.cfgjson.Emoji.Error} Failed to remove the permadehoist for {discordUsers[0].Mention}!")
                            .WithAllowedMentions(Mentions.None));

                    return;
                }

                // Un-permadehoist multiple members

                var msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Working on it...");
                int failedCount = 0;

                foreach (var discordUser in discordUsers)
                {
                    var (success, _) = await DehoistHelpers.UnpermadehoistMember(discordUser, ctx.User, ctx.Guild);

                    if (!success)
                        failedCount++;
                }
                _ = await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully removed the permadehoist for {discordUsers.Length - failedCount} of {discordUsers.Length} member(s)! (Check Audit Log for details)");
            }

            [Command("status")]
            [Description("Check the status of permadehoist for a member.")]
            public async Task PermadehoistStatus(CommandContext ctx, [Description("The member whose permadehoist status to check.")] DiscordUser discordUser)
            {
                if (await Program.db.SetContainsAsync("permadehoists", discordUser.Id))
                    await ctx.RespondAsync(new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.On} {discordUser.Mention} is permadehoisted.")
                        .WithAllowedMentions(Mentions.None));
                else
                    await ctx.RespondAsync(new DiscordMessageBuilder()
                        .WithContent($"{Program.cfgjson.Emoji.Off} {discordUser.Mention} is not permadehoisted.")
                        .WithAllowedMentions(Mentions.None));
            }
        }
    }
}
