namespace Cliptok.APIs
{
    public class UsernameAPI
    {
        public static async Task<(bool match, HttpStatusCode statusCode, string responseString, UsernameScamApiResponseJson? usernameAPIResponseBody)> UsernameAPICheckAsync(string input)
        {
            HttpRequestMessage request = new(HttpMethod.Post, Environment.GetEnvironmentVariable("USERNAME_CHECK_ENDPOINT"));
            request.Headers.Add("User-Agent", "Cliptok (https://github.com/Erisa/Cliptok)");
            MessageEvent.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var bodyObject = new UsernameScamApiRequestJson()
            {
                Username = input
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(bodyObject), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await Program.httpClient.SendAsync(request);
            var httpStatus = response.StatusCode;
            string responseText = await response.Content.ReadAsStringAsync();

            if (httpStatus == HttpStatusCode.OK)
            {
                var usernameAPIResponseBody = JsonConvert.DeserializeObject<UsernameScamApiResponseJson>(responseText);

                if (usernameAPIResponseBody.Match)
                {
                    return (true, httpStatus, responseText, usernameAPIResponseBody);
                }
                return (false, httpStatus, responseText, usernameAPIResponseBody);
            }
            else
            {
                return (false, httpStatus, responseText, null);
            }

        }

    }
}
