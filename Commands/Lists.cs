namespace Cliptok.Commands
{
    internal class Lists : BaseCommandModule
    {
        public class GitHubDispatchBody
        {
            [JsonProperty("ref")]
            public string Ref { get; set; }

            [JsonProperty("inputs")]
            public GitHubDispatchInputs Inputs { get; set; }
        }

        public class GitHubDispatchInputs
        {
            [JsonProperty("file")]
            public string File { get; set; }

            [JsonProperty("text")]
            public string Text { get; set; }

            [JsonProperty("user")]
            public string User { get; set; }
        }

        [Command("listupdate")]
        [Description("Updates the private lists from the GitHub repository, then reloads them into memory.")]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task ListUpdate(CommandContext ctx)
        {
            if (Program.cfgjson.GitListDirectory == null || Program.cfgjson.GitListDirectory == "")
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Private lists directory is not configured in bot config.");
                return;
            }

            string command = $"cd Lists/{Program.cfgjson.GitListDirectory} && git pull";
            DiscordMessage msg = await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Updating private lists..");

            ShellResult finishedShell = RunShellCommand(command);

            string result = Regex.Replace(finishedShell.result, "ghp_[0-9a-zA-Z]{36}", "ghp_REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_TOKEN"), "REDACTED");

            if (finishedShell.proc.ExitCode != 0)
            {
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Error} An error occurred trying to update private lists!\n```\n{result}\n```");
            }
            else
            {
                Program.UpdateLists();
                await msg.ModifyAsync($"{Program.cfgjson.Emoji.Success} Successfully updated and reloaded private lists!\n```\n{result}\n```");
            }

        }

        [Command("listadd")]
        [Description("Add a piece of text to a public list.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task ListAdd(
            CommandContext ctx,
            [Description("The filename of the public list to add to. For example scams.txt")] string fileName,
            [RemainingText, Description("The text to add the list. Can be in a codeblock and across multiple line.")] string content
        )
        {
            if (Environment.GetEnvironmentVariable("CLIPTOK_GITHUB_TOKEN") == null)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} `CLIPTOK_GITHUB_TOKEN` was not set, so GitHub API commands cannot be used.");
                return;
            }

            if (!fileName.EndsWith(".txt"))
                fileName += ".txt";

            if (content.Length < 3)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Your input is too short, please reconsider.");
                return;
            }

            await ctx.Channel.TriggerTypingAsync();

            if (content[..3] == "```")
                content = content.Replace("```", "").Trim();

            string[] lines = content.Split(
                new string[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None
            );

            if (lines.Any(line => line.Length < 4))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} One of your lines is shorter than 4 characters.\nFor safety reasons I can't accept this input over command. Please submit a PR manually.");
                return;
            }

            string nameToSend = $"{ctx.User.Username}#{ctx.User.Discriminator}";

            using HttpClient httpClient = new()
            {
                BaseAddress = new Uri("https://api.github.com/")
            };

            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Cliptok", "1.0"));
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", Environment.GetEnvironmentVariable("CLIPTOK_GITHUB_TOKEN"));

            HttpRequestMessage request = new(HttpMethod.Post, $"/repos/{Program.cfgjson.GitHubWorkFlow.Repo}/actions/workflows/{Program.cfgjson.GitHubWorkFlow.WorkflowId}/dispatches");

            GitHubDispatchInputs inputs = new()
            {
                File = fileName,
                Text = content,
                User = nameToSend
            };

            GitHubDispatchBody body = new()
            {
                Ref = Program.cfgjson.GitHubWorkFlow.Ref,
                Inputs = inputs
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/vnd.github.v3+json");

            HttpResponseMessage response = await httpClient.SendAsync(request);
            string responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} The request was successful.\n" +
                    $"You should see the result in <#{Program.cfgjson.HomeChannel}> soon or can check at <https://github.com/{Program.cfgjson.GitHubWorkFlow.Repo}/actions>");
            else
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} An error with code `{response.StatusCode}` was returned when trying to request the Action run.\n" +
                    $"Body: ```json\n{responseText}```");
        }

        [Command("scamcheck")]
        [Description("Check if a link or message is known to the anti-phishing API.")]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task ScamCheck(CommandContext ctx, [RemainingText, Description("Domain or message content to scan.")] string content)
        {
            var urlMatches = Constants.RegexConstants.url_rx.Matches(content);
            if (urlMatches.Count > 0 && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") != null && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") != "useyourimagination")
            {
                var (match, httpStatus, responseText, _) = await APIs.PhishingAPI.PhishingAPICheckAsync(content);

                string responseToSend;
                if (match)
                {
                    responseToSend = $"Match found:\n```json\n{responseText}\n```";

                }
                else
                {
                    responseToSend = $"No valid match found.\nHTTP Status `{(int)httpStatus}`, result:\n```json\n{responseText}\n```";
                }

                if (responseToSend.Length > 1940)
                {
                    try
                    {
                        HasteBinResult hasteURL = await Program.hasteUploader.Post(responseText);
                        if (hasteURL.IsSuccess)
                            responseToSend = hasteURL.FullUrl + ".json";
                        else
                            responseToSend = "Response was too big and Hastebin failed, sorry.";
                    }
                    catch
                    {
                        responseToSend = "Response was too big and Hastebin failed, sorry.";
                    }
                }
                await ctx.RespondAsync(responseToSend);
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Anti-phishing API is not configured, nothing for me to do.");
            }
        }

        [Command("joinwatch")]
        [Aliases("joinnotify", "leavewatch", "leavenotify")]
        [Description("Watch for joins and leaves of a given user. Output goes to #investigations.")]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task JoinWatch(
            CommandContext ctx,
            [Description("The user to watch for joins and leaves of.")] DiscordUser user
        )
        {
            var joinWatchlist = await Program.db.ListRangeAsync("joinWatchedUsers");

            if (joinWatchlist.Contains(user.Id))
            {
                Program.db.ListRemove("joinWatchedUsers", joinWatchlist.First(x => x == user.Id));
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully unwatched {user.Mention}, since they were already in the list.");
            }
            else
            {
                await Program.db.ListRightPushAsync("joinWatchedUsers", user.Id);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Now watching for joins/leaves of {user.Mention} to send to {Program.badMsgLog.Mention}!");
            }
        }

    }
}
