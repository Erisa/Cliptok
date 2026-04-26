namespace Cliptok.Helpers
{
    public class SecurityActionHelpers
    {
        public static async Task<JToken> GetCurrentSecurityActions(ulong guildId)
        {
            using HttpRequestMessage getActionsRequest = new(HttpMethod.Get, $"https://discord.com/api/v10/guilds/{guildId}");
            getActionsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bot", Environment.GetEnvironmentVariable("CLIPTOK_TOKEN") ?? Program.cfgjson.Core.Token);

            var getActionsResponse = await Program.httpClient.SendAsync(getActionsRequest);
            return ((JObject)JsonConvert.DeserializeObject(await getActionsResponse.Content.ReadAsStringAsync()))["incidents_data"];
        }

        public static async Task<HttpResponseMessage> SetCurrentSecurityActions(ulong guildId, string newSecurityActions)
        {
            // create & send request

            using HttpRequestMessage setActionsRequest = new(HttpMethod.Put, $"https://discord.com/api/v10/guilds/{guildId}/incident-actions");
            setActionsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bot", Environment.GetEnvironmentVariable("CLIPTOK_TOKEN") ?? Program.cfgjson.Core.Token);

            setActionsRequest.Content = new StringContent(newSecurityActions, Encoding.UTF8, "application/json");
            return await Program.httpClient.SendAsync(setActionsRequest);
        }
    }
}