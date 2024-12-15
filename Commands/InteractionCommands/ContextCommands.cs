namespace Cliptok.Commands.InteractionCommands
{
    internal class ContextCommands : ApplicationCommandModule
    {
        [ContextMenu(DiscordApplicationCommandType.MessageContextMenu, "Dump message data")]
        public async Task DumpMessage(ContextMenuContext ctx)
        {
            var rawMsgData = JsonConvert.SerializeObject(ctx.TargetMessage, Formatting.Indented);
            await ctx.RespondAsync(await StringHelpers.CodeOrHasteBinAsync(rawMsgData, "json"), ephemeral: true);
        }
        
        [ContextMenu(DiscordApplicationCommandType.UserContextMenu, "Show Avatar", defaultPermission: true)]
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

        [ContextMenu(DiscordApplicationCommandType.UserContextMenu, "Show Notes", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), SlashCommandPermissions(permissions: DiscordPermission.ModerateMembers)]
        public async Task ShowNotes(ContextMenuContext ctx)
        {
            await ctx.RespondAsync(embed: await UserNoteHelpers.GenerateUserNotesEmbedAsync(ctx.TargetUser), ephemeral: true);
        }

        [ContextMenu(DiscordApplicationCommandType.UserContextMenu, "Show Warnings", defaultPermission: true)]
        public async Task ContextWarnings(ContextMenuContext ctx)
        {
            await ctx.RespondAsync(embed: await WarningHelpers.GenerateWarningsEmbedAsync(ctx.TargetUser), ephemeral: true);
        }

        [ContextMenu(DiscordApplicationCommandType.UserContextMenu, "User Information", defaultPermission: true)]
        public async Task ContextUserInformation(ContextMenuContext ctx)
        {
            await ctx.RespondAsync(embed: await DiscordHelpers.GenerateUserEmbed(ctx.TargetUser, ctx.Guild), ephemeral: true);
        }

        [ContextMenu(DiscordApplicationCommandType.UserContextMenu, "Hug", defaultPermission: true),]
        public async Task Hug(ContextMenuContext ctx)
        {
            var user = ctx.TargetUser;

            if (user is not null)
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
