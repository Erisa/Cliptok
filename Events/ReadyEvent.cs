using DSharpPlus.EventArgs;
using static Cliptok.Program;

namespace Cliptok.Events
{
    public class ReadyEvent
    {
        public static async Task OnReady(DiscordClient client, SessionReadyEventArgs _)
        {

            homeGuild = await discord.GetGuildAsync(cfgjson.ServerID);
            await LogChannelHelper.UnpackLogConfigAsync(cfgjson);
            var fetchResult = await APIs.ServerAPI.FetchMaliciousServersList();
            if (fetchResult is not null)
            {
                serverApiList = fetchResult;
                client.Logger.LogDebug("Successfully initalised malicious invite list with {count} servers.", fetchResult.Count);
            }

            client.Logger.LogInformation(CliptokEventID, "Logged in as {user}", $"{DiscordHelpers.UniqueUsername(client.CurrentUser)}");
        }

        public static async Task OnStartup(DiscordClient client)
        {
            // wait until the log helper is ready
            while (true)
            {
                await Task.Delay(100);
                if (LogChannelHelper.ready)
                    break;
            }

            if (Environment.GetEnvironmentVariable("CLIPTOK_GITHUB_TOKEN") is null || Environment.GetEnvironmentVariable("CLIPTOK_GITHUB_TOKEN") == "githubtokenhere")
                discord.Logger.LogWarning(CliptokEventID, "GitHub API features disabled due to missing access token.");

            if (Environment.GetEnvironmentVariable("RAVY_API_TOKEN") is null || Environment.GetEnvironmentVariable("RAVY_API_TOKEN") == "goodluckfindingone")
                discord.Logger.LogWarning(CliptokEventID, "Ravy API features disabled due to missing API token.");

            Tasks.PunishmentTasks.CheckMutesAsync();
            Tasks.PunishmentTasks.CheckBansAsync();
            Tasks.ReminderTasks.CheckRemindersAsync();
            Tasks.RaidmodeTasks.CheckRaidmodeAsync(cfgjson.ServerID);

            string commitHash = "";
            string commitMessage = "";
            string commitTime = "";

            if (File.Exists("CommitHash.txt"))
            {
                using var sr = new StreamReader("CommitHash.txt");
                commitHash = sr.ReadToEnd();
            }

            if (Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA") is not null)
            {
                commitHash = Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA");
                commitHash = commitHash[..Math.Min(commitHash.Length, 7)];
            }

            if (string.IsNullOrWhiteSpace(commitHash))
            {
                commitHash = "dev";
            }

            if (File.Exists("CommitMessage.txt"))
            {
                using var sr = new StreamReader("CommitMessage.txt");
                commitMessage = sr.ReadToEnd();
            }

            if (Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_MESSAGE") is not null)
            {
                commitMessage = Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_MESSAGE");
            }

            if (string.IsNullOrWhiteSpace(commitMessage))
            {
                commitMessage = "N/A (Only available when built with Docker)";
            }

            if (File.Exists("CommitTime.txt"))
            {
                using var sr = new StreamReader("CommitTime.txt");
                commitTime = sr.ReadToEnd();
            }

            if (string.IsNullOrWhiteSpace(commitTime))
            {
                commitTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
            }

            bool listSuccess = false;
            if (cfgjson.GitListDirectory is not null && cfgjson.GitListDirectory != "")
            {

                ShellResult finishedShell = RunShellCommand($"cd Lists/{cfgjson.GitListDirectory} && git pull");

                string result = Regex.Replace(finishedShell.result, "ghp_[0-9a-zA-Z]{36}", "ghp_REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_TOKEN"), "REDACTED");

                if (finishedShell.proc.ExitCode != 0)
                {
                    listSuccess = false;
                    client.Logger.LogError(eventId: CliptokEventID, "Error updating lists:\n{result}", result);
                }
                else
                {
                    UpdateLists();
                    client.Logger.LogInformation(eventId: CliptokEventID, "Success updating lists:\n{result}", result);
                    listSuccess = true;
                }
            }

            LogChannelHelper.LogMessageAsync("home", $"{cfgjson.Emoji.On} {discord.CurrentUser.Username} started successfully!\n\n" +
                $"**Version**: `{commitHash.Trim()}`\n" +
                $"**Version timestamp**: `{commitTime}`\n**Framework**: `{RuntimeInformation.FrameworkDescription}`\n" +
                $"**Platform**: `{RuntimeInformation.OSDescription}`\n" +
                $"**Library**: `DSharpPlus {discord.VersionString}`\n" +
                $"**List update success**: `{listSuccess}`\n\n" +
                $"Most recent commit message:\n" +
                $"```\n" +
                $"{commitMessage}\n" +
                $"```");
        }

    }
}
