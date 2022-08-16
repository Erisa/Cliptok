#nullable enable

namespace Cliptok.APIs
{
    public class PhishingAPI
    {
        public static async Task<(bool match, HttpStatusCode httpStatus, string responseString, PhishingResponseBody? phishingResponseBody)> PhishingAPICheckAsync(string input)
        {
            HttpRequestMessage request = new(HttpMethod.Post, Environment.GetEnvironmentVariable("CLIPTOK_ANTIPHISHING_ENDPOINT"));
            request.Headers.Add("User-Agent", "Cliptok (https://github.com/Erisa/Cliptok)");
            MessageEvent.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var bodyObject = new PhishingRequestBody()
            {
                Message = input
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(bodyObject), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await Program.httpClient.SendAsync(request);
            var httpStatus = response.StatusCode;
            string responseText = await response.Content.ReadAsStringAsync();

            if (httpStatus == HttpStatusCode.OK)
            {
                var phishingResponse = JsonConvert.DeserializeObject<PhishingResponseBody>(responseText);

                if (phishingResponse.Match)
                {
                    if (phishingResponse.Matches.Exists(x => x.Type == "PHISHING" && x.TrustRating == 1))
                        return (true, httpStatus, responseText, phishingResponse);
                }
                return (false, httpStatus, responseText, phishingResponse);
            }
            else
            {
                return (false, httpStatus, responseText, null);
            }

        }
    }
}
