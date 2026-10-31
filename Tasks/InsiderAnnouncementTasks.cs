namespace Cliptok.Tasks
{
    internal static class InsiderAnnouncementTasks
    {
        internal static async Task<bool> CheckInsiderAnnouncementsAsync()
        {
            if (Program.cfgjson.InsidersChannel == 0)
                return false;

            bool didAnnounceBuilds = false;

            var insiderRoles = Program.cfgjson.InsiderRoles.Select(id => Program.homeGuild.Roles[id]).Where(role => role.Name.Contains('(')).ToList();
            foreach (DiscordRole insiderRole in insiderRoles)
            {
                List<Models.InsiderReleaseNotesJson> channelReleaseNotes = await InsidersHelpers.GetInsiderReleaseNotesForRoleAsync(insiderRole, insiderRoles, 3);

                foreach (var buildReleaseNoteEntry in channelReleaseNotes)
                {
                    var (buildNumber, urlBuildNumber) = InsidersHelpers.GetBuildNumberFromReleaseNotes(buildReleaseNoteEntry);
                    
                    // using SetMembers instead of SetContains to match combined build announcements like "26200.9807 / 26300.9807"
                    bool buildHasBeenAnnounced = (await Program.redis.SetMembersAsync("announcedInsiderBuilds"))
                        .Any(x => x.ToString().Contains(buildNumber) || (urlBuildNumber != default && x.ToString().Contains(urlBuildNumber)));

                    if (!buildHasBeenAnnounced)
                    {
                        var buildUrl = Constants.InsidersConstants.insider_release_notes_url_prefix + buildReleaseNoteEntry.Href;
                        await InsidersHelpers.AnnounceInsiderBuildAsync(buildNumber, buildUrl, insiderRole.Id.ToString(), default);
                        
                        didAnnounceBuilds = true;
                    }
                }
            }

            return didAnnounceBuilds;
        }
    }
}
