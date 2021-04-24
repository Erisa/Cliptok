using DSharpPlus.SlashCommands;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.SlashCommands.Attributes;
using DSharpPlus;
using HumanDateParser;

namespace Cliptok.Modules
{
    public class SlashCommands : SlashCommandModule
    {
        [SlashCommand("warn", "Formally warn a user, usually for breaking the server rules.")]
        public async Task TestCommand(InteractionContext ctx,
             [Option("user", "The user to warn.")] DiscordUser user,
             [Option("reason", "The reason they're being warned.")] string reason,
             [Option("channel", "The channel to warn the user in, implied if not supplied.")] DiscordChannel channel = null
            )
        {
            DiscordInteractionResponseBuilder eout;

            if (Warnings.GetPermLevel(ctx.Member) < ServerPermLevel.TrialMod)
            {
                eout = new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.NoPermissions} Invalid permissions to use command **{ctx.CommandName}**!");
                eout.IsEphemeral = true;
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, eout);
                return;
            }

            DiscordMember targetMember;

            try
            {
                targetMember = await ctx.Guild.GetMemberAsync(user.Id);
                if (Warnings.GetPermLevel(ctx.Member) == ServerPermLevel.TrialMod && (Warnings.GetPermLevel(targetMember) >= ServerPermLevel.TrialMod || targetMember.IsBot))
                {
                    eout = new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} As a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
                    eout.IsEphemeral = true;
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, eout);
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
            UserWarning warning = await Warnings.GiveWarningAsync(user, ctx.User, reason, Warnings.MessageLink(msg), channel);

            eout = new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Success} User was warned successfully in {channel.Mention}\n[Jump to warning]({Warnings.MessageLink(msg)})");
            eout.IsEphemeral = true;
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, eout);
        }

        //[SlashCommand("ban", "Bans a user from the server, either permanently or temporarily.")]
        //public async Task BanSlashCommand(InteractionContext ctx,
        //        [Option("user", "The user to ban")] DiscordUser user,
        //        [Option("reason", "The reason the user is being banned")] string reason,
        //        [Option("time", "The length of time the user is banned for, permanent if not specified.")] string time = null,
        //        [Option("keepMessages", "Whether to keep the users messages when banning")] bool keepMessages = false,
        //        [Option("appealLink", "Whether to show the user an appeal URL in the ban message.")] bool appealLink = false,
        //        [Option("silent", "Whether to not send any DM communication about the ban at all")] bool silent = false
        //)
        //{
        //    TimeSpan banDuration = default;
        //    DiscordInteractionResponseBuilder eout;

        //    if (Warnings.GetPermLevel(ctx.Member) < ServerPermLevel.TrialMod)
        //    {
        //        eout = new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.NoPermissions} Invalid permissions to use command **{ctx.CommandName}**!");
        //        eout.IsEphemeral = true;
        //        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, eout);
        //        return;
        //    }

        //    DiscordMember targetMember;

        //    try
        //    {
        //        targetMember = await ctx.Guild.GetMemberAsync(user.Id);
        //        if (Warnings.GetPermLevel(ctx.Member) == ServerPermLevel.TrialMod && (Warnings.GetPermLevel(targetMember) >= ServerPermLevel.TrialMod || targetMember.IsBot))
        //        {
        //            eout = new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} As a Trial Moderator you cannot perform moderation actions on other staff members or bots.");
        //            eout.IsEphemeral = true;
        //            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, eout);
        //            return;
        //        }
        //    }
        //    catch
        //    {
        //        // do nothing :/
        //    }

        //    if (time == null)
        //        banDuration = default;
        //    else
        //        banDuration = HumanDateParser.HumanDateParser.Parse(time);

        //    if (reason.Length > 6 && reason.Substring(0, 7) == "appeal ")
        //    {
        //        appealable = true;
        //        reason = reason[7..^0];
        //    }

        //    DiscordMember member;
        //    try
        //    {
        //        member = await ctx.Guild.GetMemberAsync(targetMember.Id);
        //    }
        //    catch
        //    {
        //        member = null;
        //    }

        //    if (member == null)
        //    {
        //        await ctx.Message.DeleteAsync();
        //        await BanFromServerAsync(targetMember.Id, reason, ctx.User.Id, ctx.Guild, 7, ctx.Channel, banDuration, appealable);
        //    }
        //    else
        //    {
        //        if (AllowedToMod(ctx.Member, member))
        //        {
        //            if (AllowedToMod(await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id), member))
        //            {
        //                await ctx.Message.DeleteAsync();
        //                await BanFromServerAsync(targetMember.Id, reason, ctx.User.Id, ctx.Guild, 7, ctx.Channel, banDuration, appealable);
        //            }
        //            else
        //            {
        //                await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, I don't have permission to ban **{targetMember.Username}#{targetMember.Discriminator}**!");
        //                return;
        //            }
        //        }
        //        else
        //        {
        //            await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Error} {ctx.User.Mention}, you don't have permission to ban **{targetMember.Username}#{targetMember.Discriminator}**!");
        //            return;
        //        }
        //    }
        //    reason = reason.Replace("`", "\\`").Replace("*", "\\*");
        //    if (banDuration == default)
        //        await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {targetMember.Mention} has been banned: **{reason}**");
        //    else
        //        await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Banned} {targetMember.Mention} has been banned for **{Warnings.TimeToPrettyFormat(banDuration, false)}**: **{reason}**");


        //}

    }
}
