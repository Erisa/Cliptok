using DSharpPlus.Net.Gateway;

namespace Cliptok.Events
{
    public class HeartbeatEvent
    {
        public static async Task OnHeartbeat(IGatewayClient client)
        {
            Program.discord.Logger.LogDebug("Heartbeat ping: {ping}", client.Ping.TotalMicroseconds);
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UPTIME_KUMA_PUSH_URL")) && client.IsConnected)
            {
                var response = await Program.httpClient.GetAsync(Environment.GetEnvironmentVariable("UPTIME_KUMA_PUSH_URL") + client.Ping.TotalMicroseconds);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Program.discord.Logger.LogDebug("Heartbeat ping succeeded.");
                }
                else
                {
                    Program.discord.Logger.LogError("Heartbeat ping sent: {status} {content}", (int)response.StatusCode, await response.Content.ReadAsStringAsync());
                }
                return;
            }
        }
    }
}
