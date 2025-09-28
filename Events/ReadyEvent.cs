using static Cliptok.Program;

namespace Cliptok.Events
{
    public class ReadyEvent
    {
        public static async Task OnReady(DiscordClient client, SessionCreatedEventArgs _)
        {
            try
            {
                if (!LogChannelHelper.ready)
                    await LogChannelHelper.UnpackLogConfigAsync(cfgjson);
            }
            catch (Exception e)
            {
                client.Logger.LogCritical(e, "Fatal error unpacking log config!");
                Environment.Exit(1);
            }

            var fetchResult = await APIs.ServerAPI.FetchMaliciousServersList();
            if (fetchResult is not null)
            {
                serverApiList = fetchResult;
                client.Logger.LogDebug("Successfully initalised malicious invite list with {count} servers.", fetchResult.Count);
            }

            if (redis.KeyExists("config:status") && redis.KeyExists("config:status_type"))
            {
                var statusText = await redis.StringGetAsync("config:status");
                var statusType = await redis.StringGetAsync("config:status_type");

                try
                {
                    await client.UpdateStatusAsync(new DiscordActivity(statusText, (DiscordActivityType)(long)statusType));
                }
                catch (Exception ex)
                {
                    client.Logger.LogError(ex, "Error updating status to {status}", statusText);
                }
            }

            client.Logger.LogDebug(CliptokEventID, "Ready event: logged in as {user}", $"{DiscordHelpers.UniqueUsername(client.CurrentUser)}");
        }

        public static async Task OnStartup(DiscordClient client)
        {
            try
            {
                homeGuild = await discord.GetGuildAsync(cfgjson.ServerID);
            }
            catch
            {
                discord.Logger.LogCritical("Error retrieving the home guild using the configured serverID! " +
                    "Please check that the bot is in the server and that the configuration is correct.");
                Environment.Exit(1);
            }

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
                commitTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss zzz");
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
                $"**List update success**: `{listSuccess}`\n" +
                $"**Log level**: `{cfgjson.LogLevel}`\n\n" +
                $"Most recent commit message:\n" +
                $"```\n" +
                $"{commitMessage}\n" +
                $"```");

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UPTIME_KUMA_PUSH_URL")))
            {
                HttpResponseMessage response;
                try
                {
                    response = await Program.httpClient.GetAsync(Environment.GetEnvironmentVariable("UPTIME_KUMA_PUSH_URL"));
                }
                catch (Exception ex)
                {
                    discord.Logger.LogError(ex, "Uptime Kuma push failed during startup!");
                    return;
                }
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    discord.Logger.LogDebug("Heartbeat ping succeeded.");
                }
                else
                {
                    discord.Logger.LogError("Heartbeat ping sent: {status} {content}", (int)response.StatusCode, await response.Content.ReadAsStringAsync());
                }
            }

            try
            {
                await Migrations.JoinwatchMigration.MigrateJoinwatchesToNotesAsync();
                await Migrations.LinePardonMigrations.MigrateLinePardonToSetAsync();
            }
            catch (Exception ex)
            {
                client.Logger.LogError(ex, "Failed to run migrations!");
            }

            client.Logger.LogInformation(CliptokEventID, "Startup event complete, logged in as {user}", $"{DiscordHelpers.UniqueUsername(client.CurrentUser)}");
        }

    }
}
