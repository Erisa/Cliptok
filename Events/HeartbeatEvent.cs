namespace Cliptok.Events
{
    public class HeartbeatEvent
    {
        public static async Task OnHeartbeat(DiscordClient client, HeartbeatedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UPTIME_KUMA_PUSH_URL")) && client.IsConnected)
            {
                var response = await Program.httpClient.GetAsync(Environment.GetEnvironmentVariable("UPTIME_KUMA_PUSH_URL") + client.Ping);
                if (response.StatusCode == HttpStatusCode.OK) {
                    client.Logger.LogDebug("Heartbeat ping succeeded.");
                } else
                {
                    client.Logger.LogError("Heartbeat ping sent: {status} {content}", (int)response.StatusCode, await response.Content.ReadAsStringAsync());
                }
                return;
            }
        }
    }
}
