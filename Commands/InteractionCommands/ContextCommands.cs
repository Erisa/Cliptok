namespace Cliptok.Commands.InteractionCommands
{
    internal class ContextCommands
    {
        [Command("Show Avatar")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task ContextAvatar(CommandContext ctx, DiscordUser targetUser)
        {
            string avatarUrl = await LykosAvatarMethods.UserOrMemberAvatarURL(targetUser, ctx.Guild);

            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
            .WithColor(new DiscordColor(0xC63B68))
            .WithTimestamp(DateTime.UtcNow)
            .WithImageUrl(avatarUrl)
            .WithAuthor(
                $"Avatar for {targetUser.Username} (Click to open in browser)",
                avatarUrl
            );

            await ctx.RespondAsync(null, embed, ephemeral: true);
        }

        [Command("Show Notes")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermissions.ModerateMembers)]
        public async Task ShowNotes(CommandContext ctx, DiscordUser targetUser)
        {
            await ctx.RespondAsync(embed: await UserNoteHelpers.GenerateUserNotesEmbedAsync(targetUser), ephemeral: true);
        }

        [Command("Show Warnings")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task ContextWarnings(CommandContext ctx, DiscordUser targetUser)
        {
            await ctx.RespondAsync(embed: await WarningHelpers.GenerateWarningsEmbedAsync(targetUser), ephemeral: true);
        }

        [Command("User Information")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task ContextUserInformation(CommandContext ctx, DiscordUser targetUser)
        {
            await ctx.RespondAsync(embed: await DiscordHelpers.GenerateUserEmbed(targetUser, ctx.Guild), ephemeral: true);
        }

        [Command("Hug")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task Hug(CommandContext ctx, DiscordUser targetUser)
        {
            var user = targetUser;

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
