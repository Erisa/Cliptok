using Cliptok.Constants;
using DSharpPlus.Exceptions;

namespace Cliptok.Commands
{
    internal class TechSupportCmds
    {
        [Command("vcredist")]
        [Description("Outputs download URLs for the specified Visual C++ Redistributables version")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        public async Task RedistsCommand(
            SlashCommandContext ctx,

            [SlashChoiceProvider(typeof(VcRedistChoiceProvider))]
            [Parameter("version"), Description("Visual Studio version number or year")] long version
        )
        {
            VcRedist redist = VcRedistConstants.VcRedists
                .First((e) =>
                {
                    return version == e.Version;
                });

            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithTitle($"Visual C++ {redist.Year}{(redist.Year == 2015 ? "+" : "")} Redistributables (version {redist.Version})")
                .WithFooter("The above links are official and safe to download.")
                .WithColor(new("7160e8"));

            foreach (var url in redist.DownloadUrls)
            {
                embed.AddField($"{url.Key.ToString("G")}", $"{url.Value}");
            }

            await ctx.RespondAsync(null, embed.Build(), false);
        }

        [Command("asktextcmd")]
        [TextAlias("ask")]
        [Description("Outputs information on how and where to ask tech support questions. Replying to a message while triggering the command will mirror the reply in the response.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer]
        public async Task AskCmd(TextCommandContext ctx, [Description("Optional, a user to ping with the information")] DiscordUser user = default)
        {
            await ctx.Message.DeleteAsync();
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithColor(13920845);
            if (ctx.Channel.Id == Program.cfgjson.TechSupportChannel || ctx.Channel.ParentId == Program.cfgjson.SupportForumId)
            {
                embed.Title = "**__Need help?__**";
                embed.Description = $"You are in the right place! Please state your question with *plenty of detail* and mention the <@&{Program.cfgjson.CommunityTechSupportRoleID}> role and someone may be able to help you.\n\n" +
                                   $"Details includes error codes and other specific information.";
            }
            else
            {
                embed.Title = "**__Need Help Or Have a Problem?__**";
                embed.Description = $"You're probably looking for <#{Program.cfgjson.TechSupportChannel}> or <#{Program.cfgjson.SupportForumId}>!\n\n" +
                                   $"Once there, please be sure to provide **plenty of details**, ping the <@&{Program.cfgjson.CommunityTechSupportRoleID}> role, and *be patient!*\n\n" +
                                   $"Look under the `🔧 Support` category for the appropriate channel for your issue. See <#413274922413195275> for more info.";
            }

            if (user != default)
            {
                await ctx.Channel.SendMessageAsync(user.Mention, embed);
            }
            else if (ctx.Message.ReferencedMessage is not null)
            {
                var messageBuild = new DiscordMessageBuilder()
                    .AddEmbed(embed)
                    .WithReply(ctx.Message.ReferencedMessage.Id, mention: true);

                await ctx.Channel.SendMessageAsync(messageBuild);
            }
            else
            {
                await ctx.Channel.SendMessageAsync(embed);
            }
        }

        [Command("on-call")]
        [Description("Give yourself the CTS role.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer]
        [RequireHomeserverPerm(ServerPermLevel.TechnicalQueriesSlayer)]
        public async Task OnCallCommand(CommandContext ctx)
        {
            var ctsRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.CommunityTechSupportRoleID);
            await ctx.Member.GrantRoleAsync(ctsRole, "Used !on-call");
            await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"{Program.cfgjson.Emoji.On} Received Community Tech Support Role")
                .WithDescription($"{ctx.User.Mention} is available to help out in **#tech-support**.\n(Use `!off-call` when you're no longer available)")
                .WithColor(DiscordColor.Green)
            ));
        }

        [Command("off-call")]
        [Description("Remove the CTS role.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer]
        [RequireHomeserverPerm(ServerPermLevel.TechnicalQueriesSlayer)]
        public async Task OffCallCommand(CommandContext ctx)
        {
            var ctsRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.CommunityTechSupportRoleID);
            await ctx.Member.RevokeRoleAsync(ctsRole, "Used !off-call");
            await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"{Program.cfgjson.Emoji.Off} Removed Community Tech Support Role")
                .WithDescription($"{ctx.User.Mention} is no longer available to help out in **#tech-support**.\n(Use `!on-call` again when you're available)")
                .WithColor(DiscordColor.Red)
            ));
        }

        [Command("solved")]
        [Description("Mark a #tech-support-forum post as solved and close it.")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [HomeServer]
        public async Task MarkTechSupportPostSolved(SlashCommandContext ctx)
        {
            await ctx.DeferResponseAsync(ephemeral: true);

            // Restrict to #tech-support-forum posts
            if (ctx.Channel.Parent is null || ctx.Channel.Parent.Id != Program.cfgjson.SupportForumId)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command can only be used inside of a <#{Program.cfgjson.SupportForumId}> post!", ephemeral: true);
                return;
            }

            // Re-fetch channel to forcefully re-cache it, & cast to thread to read thread-specific data
            var channel = await ctx.Guild.GetChannelAsync(ctx.Channel.Id, skipCache: true) as DiscordThreadChannel;

            // Restrict to OP or TQS members
            if (ctx.User.Id != channel.CreatorId && await GetPermLevelAsync(ctx.Member) < ServerPermLevel.TechnicalQueriesSlayer)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Only the original poster or a <@&{Program.cfgjson.TqsRoleId}> can mark this post as solved!");
                return;
            }

            var forum = await ctx.Client.GetChannelAsync(channel.Parent.Id) as DiscordForumChannel;
            var solvedTagId = forum.AvailableTags.FirstOrDefault(x => x.Name == "Solved").Id;

            var errorOccurred = false;

            // Add solved tag & archive thread
            List<ulong> tags = channel.AppliedTags.Select(x => x.Id).ToList();
            if (tags.All(x => x != solvedTagId))
            {
                if (tags.Count == 5)
                    tags.RemoveAt(4);
                
                tags.Add(solvedTagId);
                try
                {
                    await channel.ModifyAsync(t => t.AppliedTags = tags);
                }
                catch (Exception ex)
                {
                    errorOccurred = true;
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't add the Solved tag to this post! Please report this to the bot maintainers.");
                    Program.discord.Logger.LogWarning(ex, "An error occurred while attempting to mark this post as solved: {threadLink}:", $"https://discord.com/channels/{ctx.Guild.Id}/{ctx.Channel.Id}");
                }
            }

            await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Success} This post is solved and has been closed!\n**Unless you are the original poster, please do not reopen this post.** If you have a similar issue, please create your own post.");

            try
            {
                await channel.ModifyAsync(t => t.IsArchived = true);
            }
            catch (Exception ex)
            {
                errorOccurred = true;
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} I couldn't close this post! Please report this to the bot maintainers.");
                Program.discord.Logger.LogWarning(ex, "An error occurred while attempting to close this post: {threadLink}:", $"https://discord.com/channels/{ctx.Guild.Id}/{ctx.Channel.Id}");
            }

            // Try to DM the OP a link to their post so they can find it again
            try
            {
                var member = await ctx.Guild.GetMemberAsync(channel.CreatorId);
                await member.SendMessageAsync($"{Program.cfgjson.Emoji.Success} Your post **{channel.Name}** in <#{forum.Id}> has been marked as solved!\nIf you need to refer back to it, you can find it here: https://discord.com/channels/{channel.Guild.Id}/{channel.Id}");
            }
            catch
            {
                // Failing to send this DM isn't important
            }

            if (!errorOccurred)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Post successfully marked as solved!", ephemeral: true);
        }

        [Command("tqsmute")]
        [Description("Temporarily mute a user in tech support channels.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TechnicalQueriesSlayer)]
        public async Task TqsMuteSlashCommand(
    CommandContext ctx,
    [Parameter("user"), Description("The user to mute.")] DiscordUser targetUser,
    [Parameter("reason"), Description("The reason for the mute.")] string reason)
        {
            if (ctx is SlashCommandContext)
                await ctx.As<SlashCommandContext>().DeferResponseAsync(ephemeral: true);
            else
                await ctx.As<TextCommandContext>().Message.DeleteAsync();

            // only work if TQS mute role is configured
            if (Program.cfgjson.TqsMutedRole == 0)
            {
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} TQS mutes are not configured, so this command does nothing. Please contact the bot maintainer if this is unexpected."));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} TQS mutes are not configured, so this command does nothing. Please contact the bot maintainer if this is unexpected.");
                return;
            }

            // Only allow usage in #tech-support, #tech-support-forum, and their threads + #bot-commands
            if (ctx.Channel.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Id != Program.cfgjson.SupportForumId &&
                ctx.Channel.Parent.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Parent.Id != Program.cfgjson.SupportForumId &&
                ctx.Channel.Id != Program.cfgjson.BotCommandsChannel)
            {
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} This command can only be used in <#{Program.cfgjson.TechSupportChannel}>, <#{Program.cfgjson.SupportForumId}>, and threads in those channels!"));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command can only be used in <#{Program.cfgjson.TechSupportChannel}>, <#{Program.cfgjson.SupportForumId}>, and threads in those channels!");
                return;
            }

            // Check if the user is already muted; disallow TQS-mute if so

            DiscordRole mutedRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.MutedRole);
            DiscordRole tqsMutedRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.TqsMutedRole);

            // Get member
            DiscordMember targetMember = default;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // blah
            }

            if (await Program.redis.HashExistsAsync("mutes", targetUser.Id) || (targetMember is not null && (targetMember.Roles.Contains(mutedRole) || targetMember.Roles.Contains(tqsMutedRole))))
            {
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, that user is already muted."));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, that user is already muted.");
                return;
            }

            // Check if user to be muted is staff or TQS, and disallow if so
            if (targetMember != default && (await GetPermLevelAsync(ctx.Member)) == ServerPermLevel.TechnicalQueriesSlayer && ((await GetPermLevelAsync(targetMember)) >= ServerPermLevel.TechnicalQueriesSlayer || targetMember.IsBot))
            {
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, you cannot mute other TQS or staff members."));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, you cannot mute other TQS or staff members.");
                return;
            }

            // mute duration is static for TQS mutes
            TimeSpan muteDuration = TimeSpan.FromHours(Program.cfgjson.TqsMuteDurationHours);

            await MuteHelpers.MuteUserAsync(targetUser, reason, ctx.User.Id, ctx.Guild, ctx.Channel, muteDuration, true, true);
            if (ctx is SlashCommandContext)
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Done. Please open a modmail thread for this user if you haven't already!"));
        }

        [Command("tqsunmute")]
        [TextAlias("tqs-unmute", "untqsmute")]
        [Description("Removes a TQS Mute from a previously TQS-muted user. See also: tqsmute")]
        [AllowedProcessors(typeof(TextCommandProcessor), typeof(SlashCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TechnicalQueriesSlayer)]
        public async Task TqsUnmuteCmd(CommandContext ctx, [Parameter("user"), Description("The user you're trying to unmute.")] DiscordUser targetUser, [Description("The reason for the unmute.")] string reason)
        {
            if (ctx is SlashCommandContext)
                await ctx.As<SlashCommandContext>().DeferResponseAsync();

            // only work if TQS mute role is configured
            if (Program.cfgjson.TqsMutedRole == 0)
            {
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} TQS mutes are not configured, so this command does nothing. Please contact the bot maintainer if this is unexpected."));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} TQS mutes are not configured, so this command does nothing. Please contact the bot maintainer if this is unexpected.");
                return;
            }

            // Only allow usage in #tech-support, #tech-support-forum, and their threads + #bot-commands
            if (ctx.Channel.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Id != Program.cfgjson.SupportForumId &&
                ctx.Channel.Parent.Id != Program.cfgjson.TechSupportChannel &&
                ctx.Channel.Parent.Id != Program.cfgjson.SupportForumId &&
                ctx.Channel.Id != Program.cfgjson.BotCommandsChannel)
            {
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} This command can only be used in <#{Program.cfgjson.TechSupportChannel}>, <#{Program.cfgjson.SupportForumId}>, and threads in those channels!"));
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} This command can only be used in <#{Program.cfgjson.TechSupportChannel}>, <#{Program.cfgjson.SupportForumId}>, their threads, and <#{Program.cfgjson.BotCommandsChannel}>!");
                return;
            }

            // Get muted roles
            DiscordRole mutedRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.MutedRole);
            DiscordRole tqsMutedRole = await ctx.Guild.GetRoleAsync(Program.cfgjson.TqsMutedRole);

            // Get member
            DiscordMember targetMember = default;
            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(targetUser.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // couldn't fetch member, fail
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be in the server!");
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be in the server!");
                return;
            }

            if (await Program.redis.HashExistsAsync("mutes", targetUser.Id) && targetMember is not null && targetMember.Roles.Contains(tqsMutedRole))
            {
                // If the member has a regular mute, leave the TQS mute alone (it's only a role now & it has no effect if they also have Muted); it will be removed when they are unmuted
                if (targetMember.Roles.Contains(mutedRole))
                {
                    if (ctx is SlashCommandContext)
                        await ctx.EditResponseAsync($"{Program.cfgjson.Emoji.Error} {targetUser.Mention} has been muted by a Moderator! Their TQS Mute will be removed when the Moderator-issued mute expires.");
                    else
                        await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} {targetUser.Mention} has been muted by a Moderator! Their TQS Mute will be removed when the Moderator-issued mute expires.");
                    return;
                }

                // user is TQS-muted; unmute
                await MuteHelpers.UnmuteUserAsync(targetUser, reason, true, ctx.User, true);
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync($"{Program.cfgjson.Emoji.Success} Successfully unmuted {targetUser.Mention}!");
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully unmuted {targetUser.Mention}!");
            }
            else
            {
                // member is not TQS-muted, fail
                if (ctx is SlashCommandContext)
                    await ctx.EditResponseAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be TQS-muted!");
                else
                    await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} That user doesn't appear to be TQS-muted!");
            }
        }

    }

    internal class VcRedistChoiceProvider : IChoiceProvider
    {
        public async ValueTask<IEnumerable<DiscordApplicationCommandOptionChoice>> ProvideAsync(CommandParameter _)
        {
            return new List<DiscordApplicationCommandOptionChoice>
            {
                new("Visual Studio 2015+ - v140", "140"),
                new("Visual Studio 2013 - v120", "120"),
                new("Visual Studio 2012 - v110", "110"),
                new("Visual Studio 2010 - v100", "100"),
                new("Visual Studio 2008 - v90", "90"),
                new("Visual Studio 2005 - v80", "80")
            };
        }
    }
}
