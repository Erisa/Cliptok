using static Cliptok.Helpers.UserNoteHelpers;

namespace Cliptok.Commands.InteractionCommands
{
    internal class UserNoteInteractions : ApplicationCommandModule
    {
        [SlashCommandGroup("note", "Manage user notes", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), SlashCommandPermissions(permissions: DiscordPermission.ModerateMembers)]
        public class UserNoteSlashCommands
        {
            [SlashCommand("add", "Add a note to a user. Only visible to mods.")]
            public async Task AddUserNoteAsync(InteractionContext ctx,
                [Option("user", "The user to add a note for.")] DiscordUser user,
                [Option("note", "The note to add.")] string noteText,
                [Option("show_on_modmail", "Whether to show the note when the user opens a modmail thread. Default: true")] bool showOnModmail = true,
                [Option("show_on_warn", "Whether to show the note when the user is warned. Default: true")] bool showOnWarn = true,
                [Option("show_all_mods", "Whether to show this note to all mods, versus just yourself. Default: true")] bool showAllMods = true,
                [Option("show_once", "Whether to show this note once and then discard it. Default: false")] bool showOnce = false,
                [Option("show_on_join_and_leave", "Whether to show this note when the user joins & leaves. Works like joinwatch. Default: false")] bool showOnJoinAndLeave = false)
            {
                await ctx.DeferAsync();

                // Assemble new note
                long noteId = Program.db.StringIncrement("totalWarnings");
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
                    Timestamp = DateTime.Now,
                    Type = WarningType.Note
                };

                await Program.db.HashSetAsync(user.Id.ToString(), note.NoteId, JsonConvert.SerializeObject(note));

                // Log to mod-logs
                var embed = await GenerateUserNoteDetailEmbedAsync(note, user);
                await LogChannelHelper.LogMessageAsync("mod", $"{Program.cfgjson.Emoji.Information} New note for {user.Mention}!", embed);

                // Respond
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Successfully added note!").AsEphemeral());
            }

            [SlashCommand("delete", "Delete a note.")]
            public async Task RemoveUserNoteAsync(InteractionContext ctx,
                [Option("user", "The user whose note to delete.")] DiscordUser user,
                [Autocomplete(typeof(NotesAutocompleteProvider))][Option("note", "The note to delete.")] string targetNote)
            {
                // Get note
                UserNote note;
                try
                {
                    note = JsonConvert.DeserializeObject<UserNote>(await Program.db.HashGetAsync(user.Id.ToString(), Convert.ToInt64(targetNote)));
                }
                catch
                {
                    await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I couldn't find that note! Make sure you've got the right ID.").AsEphemeral());
                    return;
                }

                // If user manually provided an ID of a warning, refuse the request and suggest /delwarn instead
                if (note.Type == WarningType.Warning)
                {
                    await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} That's a warning, not a note! Try using `/delwarn` instead, or make sure you've got the right note ID.").AsEphemeral());
                    return;
                }

                // Delete note
                await Program.db.HashDeleteAsync(user.Id.ToString(), note.NoteId);

                // Log to mod-logs
                var embed = new DiscordEmbedBuilder(await GenerateUserNoteDetailEmbedAsync(note, user)).WithColor(0xf03916);
                await LogChannelHelper.LogMessageAsync("mod", $"{Program.cfgjson.Emoji.Deleted} Note deleted: `{note.NoteId}` (belonging to {user.Mention}, deleted by {ctx.User.Mention})", embed);

                // Respond
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Successfully deleted note!").AsEphemeral());
            }

            [SlashCommand("edit", "Edit a note for a user.")]
            public async Task EditUserNoteAsync(InteractionContext ctx,
                [Option("user", "The user to edit a note for.")] DiscordUser user,
                [Autocomplete(typeof(NotesAutocompleteProvider))][Option("note", "The note to edit.")] string targetNote,
                [Option("new_text", "The new note text. Leave empty to not change.")] string newNoteText = default,
                [Option("show_on_modmail", "Whether to show the note when the user opens a modmail thread.")] bool? showOnModmail = null,
                [Option("show_on_warn", "Whether to show the note when the user is warned.")] bool? showOnWarn = null,
                [Option("show_all_mods", "Whether to show this note to all mods, versus just yourself.")] bool? showAllMods = null,
                [Option("show_once", "Whether to show this note once and then discard it.")] bool? showOnce = null,
                [Option("show_on_join_and_leave", "Whether to show this note when the user joins & leaves. Works like joinwatch. Default: false")] bool? showOnJoinAndLeave = false)
            {
                // Get note
                UserNote note;
                try
                {
                    note = JsonConvert.DeserializeObject<UserNote>(await Program.db.HashGetAsync(user.Id.ToString(), Convert.ToInt64(targetNote)));
                }
                catch
                {
                    await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I couldn't find that note! Make sure you've got the right ID.").AsEphemeral());
                    return;
                }

                // If new text is not provided, use old text
                if (newNoteText == default)
                    newNoteText = note.NoteText;

                // If no changes are made, refuse the request
                if (note.NoteText == newNoteText && showOnModmail is null && showOnWarn is null && showAllMods is null && showOnce is null && showOnJoinAndLeave is null)
                {
                    await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} You didn't change anything about the note!").AsEphemeral());
                    return;
                }

                // If user manually provided an ID of a warning, refuse the request and suggest /editwarn instead
                if (note.Type == WarningType.Warning)
                {
                    await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} That's a warning, not a note! Try using `/editwarn` instead, or make sure you've got the right note ID.").AsEphemeral());
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

                await Program.db.HashSetAsync(user.Id.ToString(), note.NoteId, JsonConvert.SerializeObject(note));

                // Log to mod-logs
                var embed = await GenerateUserNoteDetailEmbedAsync(note, user);
                await LogChannelHelper.LogMessageAsync("mod", $"{Program.cfgjson.Emoji.Information} Note edited: `{note.NoteId}` (belonging to {user.Mention})", embed);

                // Respond
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Successfully edited note!").AsEphemeral());
            }

            [SlashCommand("list", "List all notes for a user.")]
            public async Task ListUserNotesAsync(InteractionContext ctx,
                [Option("user", "The user whose notes to list.")] DiscordUser user,
                [Option("public", "Whether to show the notes in public chat. Default: false")] bool showPublicly = false)
            {
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().AddEmbed(await GenerateUserNotesEmbedAsync(user)).AsEphemeral(!showPublicly));
            }

            [SlashCommand("details", "Show the details of a specific note for a user.")]
            public async Task ShowUserNoteAsync(InteractionContext ctx,
                [Option("user", "The user whose note to show details for.")] DiscordUser user,
                [Autocomplete(typeof(NotesAutocompleteProvider))][Option("note", "The note to show.")] string targetNote,
                [Option("public", "Whether to show the note in public chat. Default: false")] bool showPublicly = false)
            {
                // Get note
                UserNote note;
                try
                {
                    note = JsonConvert.DeserializeObject<UserNote>(await Program.db.HashGetAsync(user.Id.ToString(), Convert.ToInt64(targetNote)));
                }
                catch
                {
                    await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I couldn't find that note! Make sure you've got the right ID.").AsEphemeral());
                    return;
                }

                // If user manually provided an ID of a warning, refuse the request and suggest /warndetails instead
                if (note.Type == WarningType.Warning)
                {
                    await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} That's a warning, not a note! Try using `/warndetails` instead, or make sure you've got the right note ID.").AsEphemeral());
                    return;
                }

                // Respond
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().AddEmbed(await GenerateUserNoteDetailEmbedAsync(note, user)).AsEphemeral(!showPublicly));
            }

            private class NotesAutocompleteProvider : IAutocompleteProvider
            {
                public async Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
                {
                    var list = new List<DiscordAutoCompleteChoice>();

                    var useroption = ctx.Options.FirstOrDefault(x => x.Name == "user");
                    if (useroption == default)
                    {
                        return list;
                    }

                    var user = await ctx.Client.GetUserAsync((ulong)useroption.Value);

                    var notes = Program.db.HashGetAll(user.Id.ToString())
                        .Where(x => JsonConvert.DeserializeObject<UserNote>(x.Value).Type == WarningType.Note).ToDictionary(
                            x => x.Name.ToString(),
                            x => JsonConvert.DeserializeObject<UserNote>(x.Value)
                        ).OrderByDescending(x => x.Value.NoteId);

                    foreach (var note in notes)
                    {
                        if (list.Count >= 25)
                            break;

                        string noteString = $"{StringHelpers.Pad(note.Value.NoteId)} - {StringHelpers.Truncate(note.Value.NoteText, 29, true)} - {TimeHelpers.TimeToPrettyFormat(DateTime.Now - note.Value.Timestamp, true)}";

                        if (ctx.FocusedOption.Value.ToString() == "" || note.Value.NoteText.Contains((string)ctx.FocusedOption.Value) || noteString.ToLower().Contains(ctx.FocusedOption.Value.ToString().ToLower()))
                            list.Add(new DiscordAutoCompleteChoice(noteString, StringHelpers.Pad(note.Value.NoteId)));
                    }

                    return list;
                }
            }
        }
    }
}