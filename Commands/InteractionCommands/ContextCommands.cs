namespace Cliptok.Commands.InteractionCommands
{
    internal class ContextCommands : ApplicationCommandModule
    {
        [ContextMenu(ApplicationCommandType.UserContextMenu, "Show Avatar")]
        public async Task ContextAvatar(ContextMenuContext ctx)
        {
            string avatarUrl = await LykosAvatarMethods.UserOrMemberAvatarURL(ctx.TargetUser, ctx.Guild);

            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
            .WithColor(new DiscordColor(0xC63B68))
            .WithTimestamp(DateTime.UtcNow)
            .WithImageUrl(avatarUrl)
            .WithAuthor(
                $"Avatar for {ctx.TargetUser.Username} (Click to open in browser)",
                avatarUrl
            );

            await ctx.RespondAsync(null, embed, ephemeral: true);
        }

        [ContextMenu(ApplicationCommandType.UserContextMenu, "Show Warnings")]
        public async Task ContextWarnings(ContextMenuContext ctx)
        {
            await ctx.RespondAsync(embed: WarningHelpers.GenerateWarningsEmbed(ctx.TargetUser), ephemeral: true);
        }

        [ContextMenu(ApplicationCommandType.UserContextMenu, "User Information")]
        public async Task ContextUserInformation(ContextMenuContext ctx)
        {
            var target = ctx.TargetUser;
            DiscordEmbed embed;
            DiscordMember member = default;

            string avatarUrl = await LykosAvatarMethods.UserOrMemberAvatarURL(ctx.TargetUser, ctx.Guild, "default", 256);

            try
            {
                member = await ctx.Guild.GetMemberAsync(target.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                embed = new DiscordEmbedBuilder()
                    .WithThumbnail(avatarUrl)
                    .WithTitle($"User information for {target.Username}#{target.Discriminator}")
                    .AddField("User", target.Mention, true)
                    .AddField("User ID", target.Id.ToString(), true)
                    .AddField($"{ctx.Client.CurrentUser.Username} permission level", "N/A (not in server)", true)
                    .AddField("Roles", "N/A (not in server)", false)
                    .AddField("Last joined server", "N/A (not in server)", true)
                    .AddField("Account created", $"<t:{TimeHelpers.ToUnixTimestamp(target.CreationTimestamp.DateTime)}:F>", true);
                await ctx.RespondAsync(embed: embed, ephemeral: true);
                return;
            }

            string rolesStr = "None";

            if (member.Roles.Any())
            {
                rolesStr = "";

                foreach (DiscordRole role in member.Roles.OrderBy(x => x.Position).Reverse())
                {
                    rolesStr += role.Mention + " ";
                }
            }

            embed = new DiscordEmbedBuilder()
                .WithThumbnail(avatarUrl)
                .WithTitle($"User information for {target.Username}#{target.Discriminator}")
                .AddField("User", member.Mention, true)
                .AddField("User ID", member.Id.ToString(), true)
                .AddField($"{ctx.Client.CurrentUser.Username} permission level", GetPermLevel(member).ToString(), false)
                .AddField("Roles", rolesStr, false)
                .AddField("Last joined server", $"<t:{TimeHelpers.ToUnixTimestamp(member.JoinedAt.DateTime)}:F>", true)
                .AddField("Account created", $"<t:{TimeHelpers.ToUnixTimestamp(member.CreationTimestamp.DateTime)}:F>", true);

            await ctx.RespondAsync(embed: embed, ephemeral: true);

        }

        [ContextMenu(ApplicationCommandType.UserContextMenu, "Hug")]
        public async Task Hug(ContextMenuContext ctx)
        {
            var user = ctx.TargetUser;

            if (user != null)
            {
                switch (new Random().Next(4))
                {
                    case 0:
                        await ctx.RespondAsync($"*{ctx.User.Mention} snuggles {user.Mention}*");
                        break;

                    case 1:
                        await ctx.RespondAsync($"*{ctx.User.Mention} huggles {user.Mention}*");
                        break;

                    case 2:
                        await ctx.RespondAsync($"*{ctx.User.Mention} cuddles {user.Mention}*");
                        break;

                    case 3:
                        await ctx.RespondAsync($"*{ctx.User.Mention} hugs {user.Mention}*");
                        break;
                }
            }
        }

    }
}
