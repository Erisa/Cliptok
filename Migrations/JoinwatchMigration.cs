using static Cliptok.Program;

namespace Cliptok.Migrations
{
    public class JoinwatchMigration
    {
        public static async Task MigrateJoinwatchesToNotesAsync()
        {
            // Migration from joinwatch to user notes
            if (!await db.KeyExistsAsync("joinWatchedUsers"))
                return;
            
            var joinWatchedUsersList = await Program.db.ListRangeAsync("joinWatchedUsers");
            var joinWatchNotesList = await Program.db.HashGetAllAsync("joinWatchedUsersNotes");
            int successfulMigrations = 0;
            int numJoinWatches = joinWatchedUsersList.Length;
            foreach (var user in joinWatchedUsersList)
            {
                // Get text for note; use joinwatch context if available, or "N/A; imported from joinwatch without context" otherwise
                string noteText;
                if (joinWatchNotesList.FirstOrDefault(x => x.Name == user) == default)
                    noteText = "N/A; imported from joinwatch without context";
                else
                    noteText = joinWatchNotesList.First(x => x.Name == user).Value;
                
                // Construct note
                var note = new UserNote
                {
                    TargetUserId = Convert.ToUInt64(user),
                    ModUserId = discord.CurrentUser.Id,
                    NoteText = noteText,
                    ShowOnModmail = false,
                    ShowOnWarn = false,
                    ShowAllMods = false,
                    ShowOnce = false,
                    ShowOnJoinAndLeave = true,
                    NoteId = db.StringIncrement("totalWarnings"),
                    Timestamp = DateTime.Now,
                    Type = WarningType.Note
                };
                
                // Save note & remove joinwatch
                await db.HashSetAsync(note.TargetUserId.ToString(), note.NoteId, JsonConvert.SerializeObject(note));
                await db.ListRemoveAsync("joinWatchedUsers", note.TargetUserId);
                await db.HashDeleteAsync("joinWatchedUsersNotes", note.TargetUserId);
                successfulMigrations++;
            }
            
            if (successfulMigrations > 0)
            {
                discord.Logger.LogInformation(CliptokEventID, "Successfully migrated {count}/{total} joinwatches to notes.", successfulMigrations, numJoinWatches);
            }
            
            if (numJoinWatches != 0 && successfulMigrations != numJoinWatches)
            {
                discord.Logger.LogError(CliptokEventID, "Failed to migrate {count} joinwatches to notes!", numJoinWatches - successfulMigrations);
            }
        }
    }
}