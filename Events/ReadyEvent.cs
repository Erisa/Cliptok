using static Cliptok.Program;

namespace Cliptok.Events
{
    public class ReadyEvent
    {
        public static async Task OnReady(DiscordClient client, ReadyEventArgs _)
        {
            Task.Run(async () =>
            {
                client.Logger.LogInformation(CliptokEventID, "Logged in as {user}", $"{client.CurrentUser.Username}#{client.CurrentUser.Discriminator}");

                if (cfgjson.ErrorLogChannelId == 0)
                {
                    errorLogChannel = await client.GetChannelAsync(cfgjson.HomeChannel);
                }
                else
                {
                    errorLogChannel = await client.GetChannelAsync(cfgjson.ErrorLogChannelId);
                    await errorLogChannel.SendMessageAsync($"{cfgjson.Emoji.Connected} {discord.CurrentUser.Username} has connected to Discord!");
                }

                if (cfgjson.UsernameAPILogChannel != 0)
                    usernameAPILogChannel = await client.GetChannelAsync(cfgjson.UsernameAPILogChannel);

                if (cfgjson.MysteryLogChannelId == 0)
                    mysteryLogChannel = errorLogChannel;
                else
                {
                    Console.Write(cfgjson.MysteryLogChannelId);
                    mysteryLogChannel = await client.GetChannelAsync(cfgjson.MysteryLogChannelId);
                }
            });
        }

        public static async Task OnStartup(DiscordClient client)
        {
            logChannel = await discord.GetChannelAsync(cfgjson.LogChannel);
            userLogChannel = await discord.GetChannelAsync(cfgjson.UserLogChannel);
            badMsgLog = await discord.GetChannelAsync(cfgjson.InvestigationsChannelId);
            homeGuild = await discord.GetGuildAsync(cfgjson.ServerID);

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

            if (Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA") != null)
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

            if (Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_MESSAGE") != null)
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
            if (cfgjson.GitListDirectory != null && cfgjson.GitListDirectory != "")
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

            var cliptokChannel = await client.GetChannelAsync(cfgjson.HomeChannel);
            cliptokChannel.SendMessageAsync($"{cfgjson.Emoji.On} {discord.CurrentUser.Username} started successfully!\n\n" +
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
