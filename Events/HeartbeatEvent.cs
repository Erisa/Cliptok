using DSharpPlus.Net.Gateway;

namespace Cliptok.Events
{
    public class HeartbeatEvent
    {
        public static async Task OnHeartbeat(IGatewayClient client)
        {
            Program.discord.Logger.LogDebug("Heartbeat ping: {ping}", client.Ping.Milliseconds);
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UPTIME_KUMA_PUSH_URL")) && client.IsConnected)
            {
                HttpResponseMessage response;
                try
                {
                    response = await Program.httpClient.GetAsync(Environment.GetEnvironmentVariable("UPTIME_KUMA_PUSH_URL") + client.Ping.Milliseconds);
                }
                catch (Exception ex)
                {
                    Program.discord.Logger.LogError(ex, "Uptime Kuma push failed during heartbeat event!");
                    return;
                }
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
