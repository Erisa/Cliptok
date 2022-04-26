#nullable enable

namespace Cliptok.APIs
{
    public class AvatarAPI
    {
        static string avatarAPIBaseURL = "https://ravy.org/api/v1/avatars";

        public static async Task<(HttpStatusCode httpStatus, string responseString, AvatarResponseBody? responseObject)> CheckAvatarUrlAsync(string avatarUrl)
        {
            var builder = new UriBuilder(avatarAPIBaseURL);
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["avatar"] = avatarUrl;
            builder.Query = query.ToString();
            string url = builder.ToString();

            HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Cliptok (https://github.com/Erisa/Cliptok)");
            request.Headers.Add("Authorization", Environment.GetEnvironmentVariable("RAVY_API_TOKEN"));

            HttpResponseMessage response = await Program.httpClient.SendAsync(request);
            var httpStatus = response.StatusCode;
            string responseText = await response.Content.ReadAsStringAsync();

            if (httpStatus == HttpStatusCode.OK)
            {
                var avatarResponse = JsonConvert.DeserializeObject<AvatarResponseBody>(responseText);
                return (httpStatus, responseText, avatarResponse);
            }
            else
            {
                return (httpStatus, responseText, null);
            }

        }
    }
}
