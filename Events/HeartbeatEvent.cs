namespace Cliptok.Events
{
    public class HeartbeatEvent
    {
        public static async Task OnHeartbeat(DiscordClient client, HeartbeatEventArgs e)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UPTIME_KUMA_PUSH_URL")))
            {
                var response = await Program.httpClient.GetAsync(Environment.GetEnvironmentVariable("UPTIME_KUMA_PUSH_URL") + client.Ping);
                client.Logger.LogDebug("Heartbeat ping sent: {status} {content}", (int)response.StatusCode, await response.Content.ReadAsStringAsync());
                return;
            }
        }
    }
}
