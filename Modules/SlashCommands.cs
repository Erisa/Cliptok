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

            var messageBuild = new DiscordMessageBuilder()
                .WithContent($"{Program.cfgjson.Emoji.Warning} {user.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");

            var msg = await channel.SendMessageAsync(messageBuild);

            _ = await Warnings.GiveWarningAsync(user, ctx.User, reason, Warnings.MessageLink(msg), ctx.Channel);
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
                        await Bans.BanFromServerAsync(user.Id, reason, ctx.User.Id, ctx.Guild, 7, ctx.Channel, banDuration, appealable);
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

        [ContextMenu(ApplicationCommandType.UserContextMenu, "Show Warnings")]
        public async Task ContextWarnings(ContextMenuContext ctx)
        {
            await ctx.RespondAsync(embed: Warnings.GenerateWarningsEmbed(ctx.TargetUser), ephemeral: true);
        }

        [ContextMenu(ApplicationCommandType.UserContextMenu, "Hug")]
        public async Task Hug(ContextMenuContext ctx)
        {
            var user = ctx.TargetUser;

            if (ctx.Guild == null)
            {
                await ctx.RespondAsync("Pretty sure you want to send that in a server, right?");
                return;
            }

            if (user != null)
            {
                switch (new Random().Next(4))
                {
                    case 0:
                        await ctx.RespondAsync($"*{ctx.User.Mention} Snuggles {user.Mention}*");
                        break;

                    case 1:
                        await ctx.RespondAsync($"*{ctx.User.Mention} Huggles {user.Mention}*");
                        break;

                    case 2:
                        await ctx.RespondAsync($"*{ctx.User.Mention} Cuddles {user.Mention}*");
                        break;

                    case 3:
                        await ctx.RespondAsync($"*{ctx.User.Mention} Hugs {user.Mention}*");
                        break;
                }
            }
            else
            {
                switch (new Random().Next(4))
                {
                    case 0:
                        await ctx.RespondAsync($"*Snuggles {ctx.User.Mention} back*");
                        break;

                    case 1:
                        await ctx.RespondAsync($"*Huggles {ctx.User.Mention} back*");
                        break;

                    case 2:
                        await ctx.RespondAsync($"*Cuddles {ctx.User.Mention} back*");
                        break;

                    case 3:
                        await ctx.RespondAsync($"*Hugs {ctx.User.Mention} back*");
                        break;
                }
            }
    }


    }
}
