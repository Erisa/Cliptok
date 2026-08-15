namespace Cliptok.Commands
{
    internal class ContextMenuCmds
    {
        // Used to pass context to modal handling
        // <user ID, message from context>
        public static Dictionary<ulong, DiscordMessage> ReminderInteractionCache = new();

        [Command("Dump message data")]
        [SlashCommandTypes(DiscordApplicationCommandType.MessageContextMenu)]
        [AllowedProcessors(typeof(MessageCommandProcessor))]
        public async Task DumpMessage(MessageCommandContext ctx, DiscordMessage targetMessage)
        {
            var rawMsgData = JsonConvert.SerializeObject(targetMessage, Formatting.Indented);
            await ctx.RespondAsync((await StringHelpers.CodeOrHasteBinAsync(rawMsgData, "json")).Text, ephemeral: true);
        }

        [Command("Show Avatar")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task ContextAvatar(UserCommandContext ctx, DiscordUser targetUser)
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

        [Command("User Information")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task ContextUserInformation(UserCommandContext ctx, DiscordUser targetUser)
        {
            await ctx.RespondAsync(embed: await DiscordHelpers.GenerateUserEmbed(targetUser, ctx.Guild), ephemeral: true);
        }

        [Command("Show Warnings")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task ContextWarnings(UserCommandContext ctx, DiscordUser targetUser)
        {
            await ctx.RespondAsync(embed: await WarningHelpers.GenerateWarningsEmbedAsync(targetUser), ephemeral: true);
        }

        [Command("Show Notes")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(userPermissions: [DiscordPermission.ModerateMembers], botPermissions: [])]
        public async Task ShowNotes(UserCommandContext ctx, DiscordUser targetUser)
        {
            await ctx.RespondAsync(embed: await UserNoteHelpers.GenerateUserNotesEmbedAsync(targetUser), ephemeral: true);
        }

        [Command("Hug")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        public async Task Hug(UserCommandContext ctx, DiscordUser targetUser)
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

        [Command("Remind Me About This")]
        [AllowedProcessors(typeof(MessageCommandProcessor))]
        [SlashCommandTypes(DiscordApplicationCommandType.MessageContextMenu)]

        public static async Task ContextReminder(MessageCommandContext ctx, DiscordMessage targetMessage)
        {
            await ctx.RespondWithModalAsync(new DiscordModalBuilder()
                .WithTitle("Remind Me About This")
                .WithCustomId("remind-me-about-this-modal-callback")
                .AddTextInput(new DiscordTextInputComponent("remind-me-about-this-time-input"), "When do you want to be reminded?")
            );

            ReminderInteractionCache[ctx.User.Id] = targetMessage;
        }
    }
}
