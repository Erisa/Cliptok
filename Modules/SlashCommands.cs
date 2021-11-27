using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System;
using System.Threading.Tasks;

namespace Cliptok.Modules
{
    public static class BaseContextExtensions
    {
        public static async Task PrepareResponseAsync(this BaseContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
        }

        public static async Task RespondAsync(this BaseContext ctx, string text = null, DiscordEmbed embed = null, bool ephemeral = false, params DiscordComponent[] components)
        {
            DiscordInteractionResponseBuilder response = new();

            if (text != null) response.WithContent(text);
            if (embed != null) response.AddEmbed(embed);
            if (components.Length != 0) response.AddComponents(components);

            response.AsEphemeral(ephemeral);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, response);
        }

        public static async Task EditAsync(this BaseContext ctx, string text = null, DiscordEmbed embed = null, params DiscordComponent[] components)
        {
            DiscordWebhookBuilder response = new();

            if (text != null) response.WithContent(text);
            if (embed != null) response.AddEmbed(embed);
            if (components.Length != 0) response.AddComponents(components);

            await ctx.EditResponseAsync(response);
        }

        public static async Task FollowAsync(this BaseContext ctx, string text = null, DiscordEmbed embed = null, bool ephemeral = false, params DiscordComponent[] components)
        {
            DiscordFollowupMessageBuilder response = new();

            response.AddMentions(Mentions.All);

            if (text != null) response.WithContent(text);
            if (embed != null) response.AddEmbed(embed);
            if (components.Length != 0) response.AddComponents(components);

            response.AsEphemeral(ephemeral);

            await ctx.FollowUpAsync(response);
        }
    }

    public class SlashCommands : ApplicationCommandModule
    {

        [SlashCommand("warn", "Formally warn a user, usually for breaking the server rules.")]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialMod)]
        public async Task WarnSlashCommand(InteractionContext ctx,
             [Option("user", "The user to warn.")] DiscordUser user,
             [Option("reason", "The reason they're being warned.")] string reason,
             [Option("channel", "The channel to warn the user in, implied if not supplied.")] DiscordChannel channel = null
            )
        {
            // Initial response to avoid the 3 second timeout, will edit later.
            var eout = new DiscordInteractionResponseBuilder().AsEphemeral(true);
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource, eout);

            // Edits need a webhook rather than interaction..?
            DiscordWebhookBuilder webhookOut;

            DiscordMember targetMember;

            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(user.Id);
                if (Warnings.GetPermLevel(ctx.Member) == ServerPermLevel.TrialMod && (Warnings.GetPermLevel(targetMember) >= ServerPermLevel.TrialMod || targetMember.IsBot))
                {
                    webhookOut = new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Error} As a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
                    await ctx.EditResponseAsync(webhookOut);
                    return;
                }
            }
            catch
            {
                // do nothing :/
            }

            if (channel == null)
                channel = ctx.Channel;

            if (channel == null)
                channel = await ctx.Client.GetChannelAsync(ctx.Interaction.ChannelId);

            var messageBuild = new DiscordMessageBuilder()
                .WithContent($"{Program.cfgjson.Emoji.Warning} {user.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");

            var msg = await channel.SendMessageAsync(messageBuild);

            _ = await Warnings.GiveWarningAsync(user, ctx.User, reason, Warnings.MessageLink(msg), channel);
            webhookOut = new DiscordWebhookBuilder().WithContent($"{Program.cfgjson.Emoji.Success} User was warned successfully in {channel.Mention}\n[Jump to warning]({Warnings.MessageLink(msg)})");
            await ctx.EditResponseAsync(webhookOut);
        }

        [SlashCommand("ban", "Bans a user from the server, either permanently or temporarily.")]
        [SlashRequireHomeserverPerm(ServerPermLevel.Mod)]
        public async Task BanSlashCommand(InteractionContext ctx,
                [Option("user", "The user to ban")] DiscordUser user,
                [Option("reason", "The reason the user is being banned")] string reason,
                [Option("keep_messages", "Whether to keep the users messages when banning")] bool keepMessages = false,
                [Option("time", "The length of time the user is banned for")] string time = null,
                [Option("appeal_link", "Whether to show the user an appeal URL in the DM")] bool appealable = false
        //[Option("silent", "Whether to not send any DM communication about the ban at all")] bool silent = false
        )
        {
            // Initial response to avoid the 3 second timeout, will edit later.
            var eout = new DiscordInteractionResponseBuilder().AsEphemeral(true);
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource, eout);

            // Edits need a webhook rather than interaction..?
            DiscordWebhookBuilder webhookOut = new();
            int messageDeleteDays = 7;
            if (keepMessages)
                messageDeleteDays = 0;

            DiscordMember targetMember;

            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(user.Id);
                if (Warnings.GetPermLevel(ctx.Member) == ServerPermLevel.TrialMod && (Warnings.GetPermLevel(targetMember) >= ServerPermLevel.TrialMod || targetMember.IsBot))
                {
                    webhookOut.Content = $"{Program.cfgjson.Emoji.Error} As a Trial Moderator you cannot perform moderation actions on other staff members or bots.";
                    await ctx.EditResponseAsync(webhookOut);
                    return;
                }
            }
            catch
            {
                // do nothing :/
            }

            TimeSpan banDuration;
            if (time == null)
                banDuration = default;
            else
            {
                try
                {
                    banDuration = HumanDateParser.HumanDateParser.Parse(time).Subtract(ctx.Interaction.CreationTimestamp.DateTime);
                }
                catch
                {
                    webhookOut.Content = $"{Program.cfgjson.Emoji.Error} There was an error parsing your supplied ban length!";
                    await ctx.EditResponseAsync(webhookOut);
                    return;
                }

            }

            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(user.Id);
            }
            catch
            {
                member = null;
            }

            if (member == null)
            {
                await Bans.BanFromServerAsync(user.Id, reason, ctx.User.Id, ctx.Guild, messageDeleteDays, ctx.Channel, banDuration, appealable);
            }
            else
            {
                if (ModCmds.AllowedToMod(ctx.Member, member))
                {
                    if (ModCmds.AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
                    {
                        await Bans.BanFromServerAsync(user.Id, reason, ctx.User.Id, ctx.Guild, messageDeleteDays, ctx.Channel, banDuration, appealable);
                    }
                    else
                    {
                        webhookOut.Content = $"{Program.cfgjson.Emoji.Error} I don't have permission to ban **{user.Username}#{user.Discriminator}**!";
                        await ctx.EditResponseAsync(webhookOut);
                        return;
                    }
                }
                else
                {
                    webhookOut.Content = $"{Program.cfgjson.Emoji.Error} You don't have permission to ban **{user.Username}#{user.Discriminator}**!";
                    await ctx.EditResponseAsync(webhookOut);
                    return;
                }
            }
            reason = reason.Replace("`", "\\`").Replace("*", "\\*");
            if (banDuration == default)
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {user.Mention} has been banned: **{reason}**");
            else
                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {user.Mention} has been banned for **{Warnings.TimeToPrettyFormat(banDuration, false)}**: **{reason}**");

            webhookOut.Content = $"{Program.cfgjson.Emoji.Success} User was successfully bonked.";
            await ctx.EditResponseAsync(webhookOut);
        }

        [SlashCommand("warnings", "Fetch the warnings for a user.")]
        public async Task WarningsSlashCommand(InteractionContext ctx,
                [Option("user", "The user to find the warnings for.")] DiscordUser user,
                [Option("private", "Whether to show the warnings to you privately.")] bool privateWarnings = false
        )
        {
            var eout = new DiscordInteractionResponseBuilder().AddEmbed(Warnings.GenerateWarningsEmbed(user));
            if (privateWarnings)
                eout.AsEphemeral(true);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, eout);
        }

        [SlashCommand("transfer_warnings", "Transfer warnings from one user to another.")]
        [SlashRequireHomeserverPerm(ServerPermLevel.Mod)]
        public async Task TransferWarningsSlashCommand(InteractionContext ctx,
            [Option("source_user", "The user currently holding the warnings.")] DiscordUser sourceUser,
            [Option("target_user", "The user recieving the warnings.")] DiscordUser targetUser,
            [Option("merge", "Whether to merge the source user's warnings and the target user's warnings.")] bool merge = false,
            [Option("force_override", "DESTRUCTIVE OPERATION: Whether to OVERRIDE and DELETE the target users existing warnings.")] bool forceOverride = false
        )
        {
            var sourceWarnings = await Program.db.HashGetAllAsync(sourceUser.Id.ToString());
            var targetWarnings = await Program.db.HashGetAllAsync(targetUser.Id.ToString());

            if (sourceWarnings.Length == 0)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} The source user has no warnings to transfer.", Warnings.GenerateWarningsEmbed(sourceUser));
                return;
            }
            else if (merge)
            {
                foreach (var warning in sourceWarnings)
                {
                    await Program.db.HashSetAsync(targetUser.Id.ToString(), warning.Name, warning.Value);
                }
                await Program.db.KeyDeleteAsync(sourceUser.Id.ToString());
            }
            else if (targetWarnings.Length > 0 && !forceOverride)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Warning} **CAUTION**: The target user has warnings.\n\n" +
                    $"If you are sure you want to **OVERRIDE** and **DELETE** these warnings, please consider the consequences before adding `force_override: true` to the command.\nIf you wish to **NOT** override the target's warnings, please use merge: true instead.",
                    Warnings.GenerateWarningsEmbed(targetUser));
                return;
            }
            else if (targetWarnings.Length > 0 && forceOverride)
            {
                await Program.db.KeyDeleteAsync(targetUser.Id.ToString());
                await Program.db.KeyRenameAsync(sourceUser.Id.ToString(), targetUser.Id.ToString());
            }
            else
            {
                await Program.db.KeyRenameAsync(sourceUser.Id.ToString(), targetUser.Id.ToString());
            }
            string operationText = "";
            if (merge)
                operationText = "merge ";
            else if (forceOverride)
                operationText = "force ";
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successully {operationText}transferred warnings from {sourceUser.Mention} to {targetUser.Mention}!");
            await Program.logChannel.SendMessageAsync($"{Program.cfgjson.Emoji.Information} Warnings from {sourceUser.Mention} were {operationText}transferred to {targetUser.Mention} by `{ctx.User.Username}#{ctx.User.Discriminator}`", Warnings.GenerateWarningsEmbed(targetUser));
        }

        [ContextMenu(ApplicationCommandType.UserContextMenu, "Show Avatar")]
        public async Task ContextAvatar(ContextMenuContext ctx)
        {
            var target = ctx.TargetUser;
            var member = await ctx.Guild.GetMemberAsync(target.Id);

            string hash = member.GuildAvatarHash;

            var format = hash.StartsWith("a_") ? "gif" : "png";


            string avatarUrl;
            if (member.GuildAvatarHash != target.AvatarHash)
                avatarUrl = $"https://cdn.discordapp.com/guilds/{ctx.Guild.Id}/users/{target.Id}/avatars/{hash}.{format}?size=4096";
            else
                avatarUrl = $"https://cdn.discordapp.com/avatars/{target.Id}/{member.AvatarHash}.{format}?size=4096";

            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
            .WithColor(new DiscordColor(0xC63B68))
            .WithTimestamp(DateTime.UtcNow)
            .WithImageUrl(avatarUrl)
            .WithAuthor(
                $"Avatar for {target.Username} (Click to open in browser)",
                avatarUrl
            );

            await ctx.RespondAsync(null, embed, ephemeral: true);
        }

        [ContextMenu(ApplicationCommandType.UserContextMenu, "Show Warnings")]
        public async Task ContextWarnings(ContextMenuContext ctx)
        {
            await ctx.RespondAsync(embed: Warnings.GenerateWarningsEmbed(ctx.TargetUser), ephemeral: true);
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
