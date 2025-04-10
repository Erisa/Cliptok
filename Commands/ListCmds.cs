namespace Cliptok.Commands
{
    internal class ListCmds
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

        [Command("listupdatetextcmd")]
        [TextAlias("listupdate")]
        [Description("Updates the private lists from the GitHub repository, then reloads them into memory.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task ListUpdate(TextCommandContext ctx)
        {
            if (Program.cfgjson.GitListDirectory is null || Program.cfgjson.GitListDirectory == "")
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Private lists directory is not configured in bot config.");
                return;
            }

            string command = $"cd Lists/{Program.cfgjson.GitListDirectory} && git pull";
            await ctx.RespondAsync($"{Program.cfgjson.Emoji.Loading} Updating private lists..");
            DiscordMessage msg = await ctx.GetResponseAsync();

            ShellResult finishedShell = RunShellCommand(command);

            string result = Regex.Replace(finishedShell.result, "(?:ghp)|(?:github_pat)_[0-9a-zA-Z_]+", "ghp_REDACTED").Replace(Environment.GetEnvironmentVariable("CLIPTOK_TOKEN"), "REDACTED");

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

        [Command("listaddtextcmd")]
        [TextAlias("listadd")]
        [Description("Add a piece of text to a public list.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.Moderator)]
        public async Task ListAdd(
            TextCommandContext ctx,
            [Description("The filename of the public list to add to. For example scams.txt")] string fileName,
            [RemainingText, Description("The text to add the list. Can be in a codeblock and across multiple line.")] string content
        )
        {
            var githubToken = Environment.GetEnvironmentVariable("CLIPTOK_GITHUB_TOKEN");
            var githubTokenPrivate = Environment.GetEnvironmentVariable("CLIPTOK_GITHUB_TOKEN_PRIVATE");

            if (githubToken is null)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} `CLIPTOK_GITHUB_TOKEN` was not set, so GitHub API commands cannot be used.");
                return;
            }
            if (!fileName.EndsWith(".txt"))
                fileName += ".txt";


            if (content.Length < 3)
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Your input is too short, please don't add this to the list.");
                return;
            }

            await DiscordHelpers.SafeTyping(ctx.Channel);

            if (content[..3] == "```")
                content = content.Replace("```", "").Trim();

            string[] lines = content.Split(
                ["\r\n", "\r", "\n"],
                StringSplitOptions.None
            );

            if (lines.Any(line => line.Length < 4))
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} One of your lines is shorter than 4 characters.\nTo prevent accidents I can't accept this input over command. Please submit a PR manually.");
                return;
            }

            string nameToSend = $"{DiscordHelpers.UniqueUsername(ctx.User)}";

            var workflowId = Program.cfgjson.GitHubWorkflow.WorkflowId;
            var refName = Program.cfgjson.GitHubWorkflow.Ref;
            var repoName = Program.cfgjson.GitHubWorkflow.Repo;

            if (
                Program.cfgjson.GitHubWorkflowPrivate is not null
                && githubTokenPrivate is not null
                && Directory.GetFiles($"Lists/{Program.cfgjson.GitListDirectory}").Any(x => x.EndsWith(fileName)) )
            {
                workflowId = Program.cfgjson.GitHubWorkflowPrivate.WorkflowId;
                refName = Program.cfgjson.GitHubWorkflowPrivate.Ref;
                repoName = Program.cfgjson.GitHubWorkflowPrivate.Repo;
                githubToken = githubTokenPrivate;
            }

            using HttpClient httpClient = new()
            {
                BaseAddress = new Uri("https://api.github.com/")
            };

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Cliptok", "1.0"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", githubToken);

            HttpRequestMessage request = new(HttpMethod.Post, $"/repos/{repoName}/actions/workflows/{workflowId}/dispatches");

            GitHubDispatchInputs inputs = new()
            {
                File = fileName,
                Text = content,
                User = nameToSend
            };

            GitHubDispatchBody body = new()
            {
                Ref = refName,
                Inputs = inputs
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/vnd.github.v3+json");

            HttpResponseMessage response = await httpClient.SendAsync(request);
            string responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} The request was successful.\n" +
                    $"You should see the result in <#{Program.cfgjson.HomeChannel}> soon or can check at <https://github.com/{repoName}/actions>");
            else
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} An error with code `{response.StatusCode}` was returned when trying to request the Action run.\n" +
                    $"Body: ```json\n{responseText}```");
        }

        [Command("scamcheck")]
        [Description("Check if a link or message is known to the anti-phishing API.")]
        [AllowedProcessors(typeof(SlashCommandProcessor), typeof(TextCommandProcessor))]
        [RequireHomeserverPerm(ServerPermLevel.TrialModerator), RequirePermissions(DiscordPermission.ModerateMembers)]
        public async Task ScamCheck(CommandContext ctx, [Parameter("input"), Description("Domain or message content to scan.")] string content)
        {
            var urlMatches = Constants.RegexConstants.domain_rx.Matches(content);
            if (urlMatches.Count > 0 && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") is not null && Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT") != "useyourimagination")
            {
                var (match, httpStatus, responseText, _) = await APIs.PhishingAPI.PhishingAPICheckAsync(content);

                string responseToSend;
                if (match)
                {
                    responseToSend = $"Match found:\n";
                }
                else
                {
                    responseToSend = $"No valid match found.\nHTTP Status `{(int)httpStatus}`, result:\n";
                }

                responseToSend += await StringHelpers.CodeOrHasteBinAsync(responseText, "json");

                await ctx.RespondAsync(responseToSend);
            }
            else
            {
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Error} Anti-phishing API is not configured, nothing for me to do.");
            }
        }

        [Command("appealblocktextcmd")]
        [TextAlias("appealblock", "superduperban", "ablock")]
        [Description("Prevents a user from submitting ban appeals.")]
        [AllowedProcessors(typeof(TextCommandProcessor))]
        [HomeServer, RequireHomeserverPerm(ServerPermLevel.TrialModerator)]
        public async Task AppealBlock(
            TextCommandContext ctx,
            [Description("The user to block from ban appeals.")] DiscordUser user
        )
        {
            if (Program.db.SetContains("appealBlocks", user.Id))
            {
                // User is already blocked, unblock
                Program.db.SetRemove("appealBlocks", user.Id);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} Successfully unblocked {user.Mention}, since they were already in the list.");
            }
            else
            {
                // User is not blocked, block
                Program.db.SetAdd("appealBlocks", user.Id);
                await ctx.RespondAsync($"{Program.cfgjson.Emoji.Success} {user.Mention} is now blocked from appealing bans.");
            }
        }

    }
}
