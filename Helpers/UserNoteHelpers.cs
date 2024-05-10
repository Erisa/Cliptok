namespace Cliptok.Helpers
{
    public class UserNoteHelpers
    {
        public static async Task<DiscordEmbed> GenerateUserNotesEmbedAsync(DiscordUser user)
        {
            var notes = Program.db.HashGetAll(user.Id.ToString())
                .Where(x => JsonConvert.DeserializeObject<UserNote>(x.Value).Type == WarningType.Note).ToDictionary(
                    x => x.Name.ToString(),
                    x => JsonConvert.DeserializeObject<UserNote>(x.Value)
                );
            
            var keys = notes.Keys.OrderByDescending(note => Convert.ToInt64(note));
            string str = "";

            var embed = new DiscordEmbedBuilder()
                .WithDescription(str)
                .WithColor(new DiscordColor(0xFEC13D))
                .WithTimestamp(DateTime.Now)
                .WithFooter(
                    $"User ID: {user.Id}",
                    null
                )
                .WithAuthor(
                    $"Notes for {DiscordHelpers.UniqueUsername(user)}",
                    null,
                    await LykosAvatarMethods.UserOrMemberAvatarURL(user, Program.homeGuild, "png")
                );

            if (!notes.Any())
                embed.WithDescription("This user has no notes.")
                    .WithColor(color: DiscordColor.DarkGreen);
            else
            {
                foreach (string key in keys)
                {
                    UserNote note = notes[key];
                    
                    var text = note.NoteText;
                    
                    text = text.Replace("`", "\\`").Replace("*", "\\*");

                    if (text.Length > 29)
                    {
                        text = StringHelpers.Truncate(text, 29) + "…";
                    }
                    
                    str += $"`{StringHelpers.Pad(note.NoteId)}` **{text}** • <t:{TimeHelpers.ToUnixTimestamp(note.Timestamp)}:R>\n";
                }

                embed.WithDescription(str);
            }

            return embed;
        }
        
        public static async Task<DiscordEmbed> GenerateUserNoteDetailEmbedAsync(UserNote note, DiscordUser user)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithDescription($"**Note**\n{note.NoteText}")
                .WithColor(new DiscordColor(0xFEC13D))
                .WithTimestamp(DateTime.Now)
                .WithFooter(
                    $"User ID: {user.Id}",
                    null
                )
                .WithAuthor(
                    $"Note for {DiscordHelpers.UniqueUsername(user)}",
                    null,
                    await LykosAvatarMethods.UserOrMemberAvatarURL(user, Program.homeGuild, "png")
                )
                .AddField("Note ID", StringHelpers.Pad(note.NoteId))
                .AddField("Show on Modmail", note.ShowOnModmail ? "Yes" : "No")
                .AddField("Show on Warn", note.ShowOnWarn ? "Yes" : "No")
                .AddField("Show all Mods", note.ShowAllMods ? "Yes" : "No")
                .AddField("Show Once", note.ShowOnce ? "Yes" : "No")
                .AddField("Responsible moderator", $"<@{note.ModUserId}>")
                .AddField("Time", $"<t:{TimeHelpers.ToUnixTimestamp(note.Timestamp)}:f>");

            return embed;
        }
    }
}