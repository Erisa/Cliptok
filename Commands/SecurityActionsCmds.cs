namespace Cliptok.Commands
{
    public class SecurityActionsCmds
    {
        [Command("pausedms")]
        [Description("Temporarily pause DMs between server members.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task SlashPauseDMs(CommandContext ctx, [Parameter("time"), Description("The amount of time to pause DMs for. Cannot be greater than 24 hours.")] string time)
        {
            // need to make our own api calls because D#+ can't do this natively?

            // parse time from message
            DateTime t = HumanDateParser.HumanDateParser.Parse(time);
            if (t <= DateTime.Now)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Time can't be in the past!");
                return;
            }
            if (t > DateTime.Now.AddHours(24))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Time can't be greater than 24 hours!");
                return;
            }
            var dmsDisabledUntil = t.ToUniversalTime().ToString("o");

            // get current security actions to avoid unintentionally resetting invites_disabled_until
            var currentActions = await SecurityActionHelpers.GetCurrentSecurityActions(ctx.Guild.Id);
            JToken invitesDisabledUntil;
            if (currentActions is null || !currentActions.HasValues)
                invitesDisabledUntil = null;
            else
                invitesDisabledUntil = currentActions["invites_disabled_until"];

            // create json body
            var newSecurityActions = JsonConvert.SerializeObject(new
            {
                invites_disabled_until = invitesDisabledUntil,
                dms_disabled_until = dmsDisabledUntil,
            });

            // set actions
            var setActionsResponse = await SecurityActionHelpers.SetCurrentSecurityActions(ctx.Guild.Id, newSecurityActions);

            // respond
            if (setActionsResponse.IsSuccessStatusCode)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully paused DMs until <t:{TimeHelpers.ToUnixTimestamp(t)}>!");
            else
            {
                ctx.Client.Logger.LogError("Failed to set Security Actions.\nPayload: {payload}\nResponse: {statuscode} {body}", newSecurityActions.ToString(), (int)setActionsResponse.StatusCode, await setActionsResponse.Content.ReadAsStringAsync());
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Something went wrong and I wasn't able to pause DMs! Discord returned status code `{setActionsResponse.StatusCode}`.");
            }
        }

        [Command("unpausedms")]
        [Description("Unpause DMs between server members.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task SlashUnpauseDMs(CommandContext ctx)
        {
            // need to make our own api calls because D#+ can't do this natively?

            // get current security actions to avoid unintentionally resetting invites_disabled_until
            var currentActions = await SecurityActionHelpers.GetCurrentSecurityActions(ctx.Guild.Id);
            JToken dmsDisabledUntil, invitesDisabledUntil;
            if (currentActions is null || !currentActions.HasValues)
            {
                dmsDisabledUntil = null;
                invitesDisabledUntil = null;
            }
            else
            {
                dmsDisabledUntil = currentActions["dms_disabled_until"];
                invitesDisabledUntil = currentActions["invites_disabled_until"];
            }

            // if dms are already unpaused, return error
            if (dmsDisabledUntil is null)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} DMs are already unpaused!");
                return;
            }

            // create json body
            var newSecurityActions = JsonConvert.SerializeObject(new
            {
                invites_disabled_until = invitesDisabledUntil,
                dms_disabled_until = (object)null,
            });

            // set actions
            var setActionsResponse = await SecurityActionHelpers.SetCurrentSecurityActions(ctx.Guild.Id, newSecurityActions);

            // respond
            if (setActionsResponse.IsSuccessStatusCode)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully unpaused DMs!");
            else
            {
                ctx.Client.Logger.LogError("Failed to set Security Actions.\nPayload: {payload}\nResponse: {statuscode} {body}", newSecurityActions.ToString(), (int)setActionsResponse.StatusCode, await setActionsResponse.Content.ReadAsStringAsync());
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Something went wrong and I wasn't able to unpause DMs! Discord returned status code `{setActionsResponse.StatusCode}`.");
            }
        }
    }
}