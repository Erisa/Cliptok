namespace Cliptok.Tasks
{
    public class HeartbeatTasks
    {
        public static async Task<bool> HeartbeatAsync()
        {
            var ping = Program.discord.GetConnectionLatency(Program.homeGuild.Id).Milliseconds;
            Program.discord.Logger.LogDebug("Heartbeat ping: {ping}", ping);
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UPTIME_KUMA_PUSH_URL")) && Program.discord.AllShardsConnected)
            {
                HttpResponseMessage response;
                try
                {
                    response = await Program.httpClient.GetAsync(Environment.GetEnvironmentVariable("UPTIME_KUMA_PUSH_URL") + ping);
                }
                catch (Exception ex)
                {
                    Program.discord.Logger.LogError(ex, "Uptime Kuma push failed during heartbeat event!");
                    return false;
                }
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Program.discord.Logger.LogDebug("Heartbeat ping succeeded.");
                    return true;
                }
                else
                {
                    Program.discord.Logger.LogError("Heartbeat ping sent: {status} {content}", (int)response.StatusCode, await response.Content.ReadAsStringAsync());
                    return false;
                }
            }
            return false;
        }
    }
}