using static Cliptok.Helpers.UserNoteHelpers;

namespace Cliptok.Commands.InteractionCommands
{
    internal class UserNoteInteractions : ApplicationCommandModule
    {
        [SlashCommandGroup("note", "Manage user notes", defaultPermission: false)]
        [SlashRequireHomeserverPerm(ServerPermLevel.TrialModerator), SlashCommandPermissions(Permissions.ModerateMembers)]
        public class UserNoteSlashCommands
        {
            [SlashCommand("add", "Add a note to a user. Only visible to mods.")]
            public async Task AddUserNoteAsync(InteractionContext ctx,
                [Option("user", "The user to add a note for.")] DiscordUser user,
                [Option("note", "The note to add.")] string noteText,
                [Option("show_on_modmail", "Whether to show the note when the user opens a modmail thread. Default: true")] bool showOnModmail = true,
                [Option("show_on_warn", "Whether to show the note when the user is warned. Default: true")] bool showOnWarn = true,
                [Option("show_all_mods", "Whether to show this note to all mods, versus just yourself. Default: true")] bool showAllMods = true,
                [Option("show_once", "Whether to show this note once and then discard it. Default: false")] bool showOnce = false)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource, new DiscordInteractionResponseBuilder().AsEphemeral());
                
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
                    NoteId = noteId,
                    Timestamp = DateTime.Now,
                    Type = WarningType.Note
                };
                
                await Program.db.HashSetAsync(user.Id.ToString(), note.NoteId, JsonConvert.SerializeObject(note));
                
                // Respond
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Successfully added note!").AsEphemeral());
            }

            [SlashCommand("delete", "Delete a note.")]
            public async Task RemoveUserNoteAsync(InteractionContext ctx,
                [Option("user", "The user whose note to delete.")] DiscordUser user,
                [Autocomplete(typeof(NotesAutocompleteProvider))] [Option("note", "The note to delete.")] string targetNote)
            {
                // Get note
                UserNote note;
                try
                {
                    note = JsonConvert.DeserializeObject<UserNote>(await Program.db.HashGetAsync(user.Id.ToString(), Convert.ToInt64(targetNote)));
                }
                catch (ArgumentNullException)
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
                
                // Respond
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Success} Successfully deleted note!").AsEphemeral());
            }
            
            [SlashCommand("edit", "Edit a note for a user.")]
            public async Task EditUserNoteAsync(InteractionContext ctx,
                [Option("user", "The user to edit a note for.")] DiscordUser user,
                [Autocomplete(typeof(NotesAutocompleteProvider))] [Option("note", "The note to edit.")] string targetNote,
                [Option("new_note", "The new note text.")] string newNoteText,
                [Option("show_on_modmail", "Whether to show the note when the user opens a modmail thread. Default: false")] bool showOnModmail = false,
                [Option("show_on_warn", "Whether to show the note when the user is warned. Default: true")] bool showOnWarn = true,
                [Option("show_all_mods", "Whether to show this note to all mods, versus just yourself. Default: true")] bool showAllMods = true,
                [Option("show_once", "Whether to show this note once and then discard it. Default: false")] bool showOnce = false)
            {
                // Get note
                UserNote note;
                try
                {
                    note = JsonConvert.DeserializeObject<UserNote>(await Program.db.HashGetAsync(user.Id.ToString(), Convert.ToInt64(targetNote)));
                }
                catch (ArgumentNullException)
                {
                    await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} I couldn't find that note! Make sure you've got the right ID.").AsEphemeral());
                    return;
                }
                
                // If user manually provided an ID of a warning, refuse the request and suggest /editwarn instead
                if (note.Type == WarningType.Warning)
                {
                    await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent($"{Program.cfgjson.Emoji.Error} That's a warning, not a note! Try using `/editwarn` instead, or make sure you've got the right note ID.").AsEphemeral());
                    return;
                }
                
                // Assemble new note
                note.NoteText = newNoteText;
                note.ShowOnModmail = showOnModmail;
                note.ShowOnWarn = showOnWarn;
                note.ShowAllMods = showAllMods;
                note.ShowOnce = showOnce;
                note.Type = WarningType.Note;
                
                await Program.db.HashSetAsync(user.Id.ToString(), note.NoteId, JsonConvert.SerializeObject(note));
                
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
            
            [SlashCommand("show", "Show the details of a specific note for a user.")]
            public async Task ShowUserNoteAsync(InteractionContext ctx,
                [Option("user", "The user whose note to show.")] DiscordUser user,
                [Autocomplete(typeof(NotesAutocompleteProvider))] [Option("note", "The note to show.")] string targetNote,
                [Option("public", "Whether to show the note in public chat. Default: false")] bool showPublicly = false)
            {
                // Get note
                UserNote note;
                try
                {
                    note = JsonConvert.DeserializeObject<UserNote>(await Program.db.HashGetAsync(user.Id.ToString(), Convert.ToInt64(targetNote)));
                }
                catch (ArgumentNullException)
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