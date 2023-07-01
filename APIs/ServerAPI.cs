namespace Cliptok.APIs
{
    public class ServerAPI
    {
        static readonly string serverCheckUrl = "https://api.phish.gg/server";
        static readonly string serverListUrl = "https://api.phish.gg/servers/all";

        public static async Task<(bool match, HttpStatusCode httpStatus, string responseString, ServerApiResponseJson? serverResponse)> ServerAPICheckAsync(ulong serverID)
        {
            var builder = new UriBuilder(serverCheckUrl);
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["id"] = serverID.ToString();
            builder.Query = query.ToString();
            string url = builder.ToString();

            HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Cliptok (https://github.com/Erisa/Cliptok)");

            HttpResponseMessage response = await Program.httpClient.SendAsync(request);
            var httpStatus = response.StatusCode;
            string responseText = await response.Content.ReadAsStringAsync();

            if (httpStatus == HttpStatusCode.OK)
            {
                var serverResponse = JsonConvert.DeserializeObject<ServerApiResponseJson>(responseText);
                return (serverResponse.Match, httpStatus, responseText, serverResponse);
            }
            else
            {
                return (false, httpStatus, responseText, null);
            }

        }

        public static async Task<List<ServerApiResponseJson>>? FetchMaliciousServersList()
        {
            HttpRequestMessage request = new(HttpMethod.Get, serverListUrl);
            request.Headers.Add("User-Agent", "Cliptok (https://github.com/Erisa/Cliptok)");

            HttpResponseMessage response = await Program.httpClient.SendAsync(request);
            var httpStatus = response.StatusCode;
            string responseText = await response.Content.ReadAsStringAsync();

            if (httpStatus != HttpStatusCode.OK)
            {
                Program.discord.Logger.LogError("Failed to fetch malicious server list: {code}\n{response}", (int)httpStatus, responseText);
                return null;
            }
            else
            {
                return JsonConvert.DeserializeObject<List<ServerApiResponseJson>>(responseText);
            }
        }

    }
}
