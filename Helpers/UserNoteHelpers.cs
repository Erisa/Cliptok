namespace Cliptok.Helpers
{
    public class UserNoteHelpers
    {
        public static async Task<DiscordEmbed> GenerateUserNotesEmbedAsync(DiscordUser user, bool showOnlyWarningNotes = false, Dictionary<string, UserNote> notesToUse = default, bool showSimpleSingleNote = true, DiscordColor? colorOverride = null)
        {
            Dictionary<string, UserNote> notes;

            // If provided with a set of notes, use them instead
            if (notesToUse == default)
            {
                notes = (await Program.redis.HashGetAllAsync(user.Id.ToString()))
                    .Where(x => JsonConvert.DeserializeObject<UserNote>(x.Value).Type == WarningType.Note).ToDictionary(
                        x => x.Name.ToString(),
                        x => JsonConvert.DeserializeObject<UserNote>(x.Value)
                    );

                // Filter to 'show on warn' notes if requested
                if (showOnlyWarningNotes)
                    notes = notes.Where(x => x.Value.ShowOnWarn).ToDictionary(x => x.Key, x => x.Value);
            }
            else
            {
                notes = notesToUse;
            }

            // If there is only one note in the set to show, just show its details
            if (notes.Count == 1)
            {
                if (showSimpleSingleNote)
                {
                    var noteDetailsEmbed = await GenerateUserNoteSimpleEmbedAsync(notes.First().Value, user, colorOverride);
                    return new DiscordEmbedBuilder(noteDetailsEmbed);
                }
                else
                {
                    var noteDetailsEmbed = await GenerateUserNoteDetailEmbedAsync(notes.First().Value, user, colorOverride);
                    return new DiscordEmbedBuilder(noteDetailsEmbed);
                }
            }

            var keys = notes.Keys.OrderByDescending(note => Convert.ToInt64(note));
            string str = "";

            var embed = new DiscordEmbedBuilder()
                .WithDescription(str)
                .WithColor(colorOverride ?? new DiscordColor(0xFEC13D))
                .WithTimestamp(DateTime.Now)
                .WithFooter(
                    $"User ID: {user.Id}",
                    null
                )
                .WithAuthor(
                    (showOnlyWarningNotes || notesToUse != default ? "Relevant " : "") + $"Notes for {DiscordHelpers.UniqueUsername(user)}",
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

        public static async Task<DiscordEmbed> GenerateUserNoteSimpleEmbedAsync(UserNote note, DiscordUser user, DiscordColor? colorOverride = null)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithDescription($"**Note**\n{note.NoteText}")
                .WithColor(colorOverride ?? new DiscordColor(0xFEC13D))
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
                .AddField("Note ID", StringHelpers.Pad(note.NoteId), true)
                .AddField("Responsible moderator", $"<@{note.ModUserId}>", true)
                .AddField("Time", $"<t:{TimeHelpers.ToUnixTimestamp(note.Timestamp)}:f>", true);

            if (note.ShowOnce)
                embed.AddField("Showing Once Only", "This note was set to show only once. It has now been deleted!");

            return embed;
        }

        public static async Task<DiscordEmbed> GenerateUserNoteDetailEmbedAsync(UserNote note, DiscordUser user, DiscordColor? colorOverride = null)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithDescription($"**Note**\n{note.NoteText}")
                .WithColor(colorOverride ?? new DiscordColor(0xFEC13D))
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
                .AddField("Note ID", StringHelpers.Pad(note.NoteId), true)
                .AddField("Show on Modmail", note.ShowOnModmail ? "Yes" : "No", true)
                .AddField("Show on Warn", note.ShowOnWarn ? "Yes" : "No", true)
                .AddField("Show all Mods", note.ShowAllMods ? "Yes" : "No", true)
                .AddField("Show Once", note.ShowOnce ? "Yes" : "No", true)
                .AddField("Show on Join & Leave", note.ShowOnJoinAndLeave ? "Yes" : "No", true)
                .AddField("Responsible moderator", $"<@{note.ModUserId}>", true)
                .AddField("Time", $"<t:{TimeHelpers.ToUnixTimestamp(note.Timestamp)}:f>", true);

            return embed;
        }
    }
}