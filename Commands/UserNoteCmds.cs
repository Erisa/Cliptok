using static Cliptok.Helpers.UserNoteHelpers;

namespace Cliptok.Commands
{
    internal class UserNoteCmds
    {
        [Command("Show Notes")]
        [SlashCommandTypes(DiscordApplicationCommandType.UserContextMenu)]
        [AllowedProcessors(typeof(UserCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task ShowNotes(UserCommandContext ctx, DiscordUser targetUser)
        {
            await ctx.RespondAsync(embed: await UserNoteHelpers.GenerateUserNotesEmbedAsync(targetUser), ephemeral: true);
        }

        [Command("note")]
        [Description("Manage user notes")]
        [AllowedProcessors(typeof(SlashCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public class UserNoteSlashCommands
        {
            [Command("add")]
            [Description("Add a note to a user. Only visible to mods.")]
            public async Task AddUserNoteAsync(SlashCommandContext ctx,
                [Parameter("user"), Description("The user to add a note for.")] DiscordUser user,
                [Parameter("note"), Description("The note to add.")] string noteText,
                [Parameter("show_on_modmail"), Description("Whether to show the note when the user opens a modmail thread. Default: true")] bool showOnModmail = true,
                [Parameter("show_on_warn"), Description("Whether to show the note when the user is warned. Default: true")] bool showOnWarn = true,
                [Parameter("show_all_mods"), Description("Whether to show this note to all mods, versus just yourself. Default: true")] bool showAllMods = true,
                [Parameter("show_once"), Description("Whether to show this note once and then discard it. Default: false")] bool showOnce = false,
                [Parameter("show_on_join_and_leave"), Description("Whether to show this note when the user joins & leaves. Works like joinwatch. Default: false")] bool showOnJoinAndLeave = false)
            {
                await ctx.DeferResponseAsync();

                // Assemble new note
                long noteId = Program.redis.StringIncrement("totalWarnings");
                UserNote note = new()
                {
                    TargetUserId = user.Id,
                    ModUserId = ctx.User.Id,
                    NoteText = noteText,
                    ShowOnModmail = showOnModmail,
                    ShowOnWarn = showOnWarn,
                    ShowAllMods = showAllMods,
                    ShowOnce = showOnce,
                    ShowOnJoinAndLeave = showOnJoinAndLeave,
                    NoteId = noteId,
                    Timestamp = DateTime.UtcNow,
                    Type = WarningType.Note
                };

                await Program.redis.HashSetAsync(user.Id.ToString(), note.NoteId, JsonConvert.SerializeObject(note));

                // Log to mod-logs
                var embed = await GenerateUserNoteDetailEmbedAsync(note, user);
                await LogChannelHelper.LogMessageAsync("mod", $"{Program.cfgjson.Emoji.Information} New note for {user.Mention}!", embed);

                // Respond
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Successfully added note!").AsEphemeral());
            }

            [Command("delete")]
            [Description("Delete a note.")]
            public async Task RemoveUserNoteAsync(SlashCommandContext ctx,
                [Parameter("user"), Description("The user whose note to delete.")] DiscordUser user,
                [SlashAutoCompleteProvider(typeof(NotesAutocompleteProvider))][Parameter("note"), Description("The note to delete.")] string targetNote)
            {
                // Get note
                UserNote note;
                try
                {
                    note = JsonConvert.DeserializeObject<UserNote>(await Program.redis.HashGetAsync(user.Id.ToString(), Convert.ToInt64(targetNote)));
                }
                catch
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I couldn't find that note! Make sure you've got the right ID.").AsEphemeral());
                    return;
                }

                // If user manually provided an ID of a warning, refuse the request and suggest /delwarn instead
                if (note.Type == WarningType.Warning)
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} That's a warning, not a note! Try using `/delwarn` instead, or make sure you've got the right note ID.").AsEphemeral());
                    return;
                }

                // Delete note
                await Program.redis.HashDeleteAsync(user.Id.ToString(), note.NoteId);

                // Log to mod-logs
                var embed = new DiscordEmbedBuilder(await GenerateUserNoteDetailEmbedAsync(note, user)).WithColor(0xf03916);
                await LogChannelHelper.LogMessageAsync("mod", $"{Program.cfgjson.Emoji.Deleted} Note deleted: `{note.NoteId}` (belonging to {user.Mention}, deleted by {ctx.User.Mention})", embed);

                // Respond
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Successfully deleted note!").AsEphemeral());
            }

            [Command("edit")]
            [Description("Edit a note for a user.")]
            public async Task EditUserNoteAsync(SlashCommandContext ctx,
                [Parameter("user"), Description("The user to edit a note for.")] DiscordUser user,
                [SlashAutoCompleteProvider(typeof(NotesAutocompleteProvider))][Parameter("note"), Description("The note to edit.")] string targetNote,
                [Parameter("new_text"), Description("The new note text. Leave empty to not change.")] string newNoteText = default,
                [Parameter("show_on_modmail"), Description("Whether to show the note when the user opens a modmail thread.")] bool? showOnModmail = null,
                [Parameter("show_on_warn"), Description("Whether to show the note when the user is warned.")] bool? showOnWarn = null,
                [Parameter("show_all_mods"), Description("Whether to show this note to all mods, versus just yourself.")] bool? showAllMods = null,
                [Parameter("show_once"), Description("Whether to show this note once and then discard it.")] bool? showOnce = null,
                [Parameter("show_on_join_and_leave"), Description("Whether to show this note when the user joins & leaves. Works like joinwatch.")] bool? showOnJoinAndLeave = null)
            {
                // Get note
                UserNote note;
                try
                {
                    note = JsonConvert.DeserializeObject<UserNote>(await Program.redis.HashGetAsync(user.Id.ToString(), Convert.ToInt64(targetNote)));
                }
                catch
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I couldn't find that note! Make sure you've got the right ID.").AsEphemeral());
                    return;
                }

                // If new text is not provided, use old text
                if (newNoteText == default)
                    newNoteText = note.NoteText;

                // If no changes are made, refuse the request
                if (note.NoteText == newNoteText && showOnModmail is null && showOnWarn is null && showAllMods is null && showOnce is null && showOnJoinAndLeave is null)
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} You didn't change anything about the note!").AsEphemeral());
                    return;
                }

                // If user manually provided an ID of a warning, refuse the request and suggest /editwarn instead
                if (note.Type == WarningType.Warning)
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} That's a warning, not a note! Try using `/editwarn` instead, or make sure you've got the right note ID.").AsEphemeral());
                    return;
                }

                // For any options the user didn't provide, use options from the note
                if (showOnModmail is null)
                    showOnModmail = note.ShowOnModmail;
                if (showOnWarn is null)
                    showOnWarn = note.ShowOnWarn;
                if (showAllMods is null)
                    showAllMods = note.ShowAllMods;
                if (showOnce is null)
                    showOnce = note.ShowOnce;
                if (showOnJoinAndLeave is null)
                    showOnJoinAndLeave = note.ShowOnJoinAndLeave;

                // Assemble new note
                note.ModUserId = ctx.User.Id;
                note.NoteText = newNoteText;
                note.ShowOnModmail = (bool)showOnModmail;
                note.ShowOnWarn = (bool)showOnWarn;
                note.ShowAllMods = (bool)showAllMods;
                note.ShowOnce = (bool)showOnce;
                note.ShowOnJoinAndLeave = (bool)showOnJoinAndLeave;
                note.Type = WarningType.Note;

                await Program.redis.HashSetAsync(user.Id.ToString(), note.NoteId, JsonConvert.SerializeObject(note));

                // Log to mod-logs
                var embed = await GenerateUserNoteDetailEmbedAsync(note, user);
                await LogChannelHelper.LogMessageAsync("mod", $"{Program.cfgjson.Emoji.Information} Note edited: `{note.NoteId}` (belonging to {user.Mention})", embed);

                // Respond
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Successfully edited note!").AsEphemeral());
            }

            [Command("list")]
            [Description("List all notes for a user.")]
            public async Task ListUserNotesAsync(SlashCommandContext ctx,
                [Parameter("user"), Description("The user whose notes to list.")] DiscordUser user,
                [Parameter("public"), Description("Whether to show the notes in public chat. Default: false")] bool showPublicly = false)
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(await GenerateUserNotesEmbedAsync(user)).AsEphemeral(!showPublicly));
            }

            [Command("details")]
            [Description("Show the details of a specific note for a user.")]
            public async Task ShowUserNoteAsync(SlashCommandContext ctx,
                [Parameter("user"), Description("The user whose note to show details for.")] DiscordUser user,
                [SlashAutoCompleteProvider(typeof(NotesAutocompleteProvider))][Parameter("note"), Description("The note to show.")] string targetNote,
                [Parameter("public"), Description("Whether to show the note in public chat. Default: false")] bool showPublicly = false)
            {
                // Get note
                UserNote note;
                try
                {
                    note = JsonConvert.DeserializeObject<UserNote>(await Program.redis.HashGetAsync(user.Id.ToString(), Convert.ToInt64(targetNote)));
                }
                catch
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I couldn't find that note! Make sure you've got the right ID.").AsEphemeral());
                    return;
                }

                // If user manually provided an ID of a warning, refuse the request and suggest /warndetails instead
                if (note.Type == WarningType.Warning)
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} That's a warning, not a note! Try using `/warndetails` instead, or make sure you've got the right note ID.").AsEphemeral());
                    return;
                }

                // Respond
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(await GenerateUserNoteDetailEmbedAsync(note, user)).AsEphemeral(!showPublicly));
            }

            private class NotesAutocompleteProvider : IAutoCompleteProvider
            {
                public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext ctx)
                {
                    var list = new List<DiscordAutoCompleteChoice>();

                    var useroption = ctx.Options.FirstOrDefault(x => x.Name == "user");
                    if (useroption == default)
                    {
                        return list;
                    }

                    var user = await ctx.Client.GetUserAsync((ulong)useroption.Value);

                    var notes = (await Program.redis.HashGetAllAsync(user.Id.ToString()))
                        .Where(x => JsonConvert.DeserializeObject<UserNote>(x.Value).Type == WarningType.Note).ToDictionary(
                            x => x.Name.ToString(),
                            x => JsonConvert.DeserializeObject<UserNote>(x.Value)
                        ).OrderByDescending(x => x.Value.NoteId);

                    foreach (var note in notes)
                    {
                        if (list.Count >= 25)
                            break;

                        string noteString = $"{StringHelpers.Pad(note.Value.NoteId)} - {StringHelpers.Truncate(note.Value.NoteText, 29, true)} - {TimeHelpers.TimeToPrettyFormat(DateTime.UtcNow - note.Value.Timestamp, true)}";

                        var focusedOption = ctx.Options.FirstOrDefault(option => option.Focused);
                        if (focusedOption is not null)
                            if (note.Value.NoteText.Contains((string)focusedOption.Value) || noteString.ToLower().Contains(focusedOption.Value.ToString().ToLower()))
                                list.Add(new DiscordAutoCompleteChoice(noteString, StringHelpers.Pad(note.Value.NoteId)));
                    }

                    return list;
                }
            }
        }
    }
}