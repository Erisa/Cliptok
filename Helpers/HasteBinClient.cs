// Based on https://gist.github.com/b5e14d0c36f5a972060655b1aa875dbf

namespace Cliptok.Helpers
{
    public class HasteBinClient
    {
        private static readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        public readonly string _hasteType = "haste";

        static HasteBinClient()
        {
            _httpClient = new HttpClient();
        }

        public HasteBinClient(string baseUrl, string hasteType = "haste")
        {
            _baseUrl = baseUrl;
            _hasteType = hasteType;
        }

        public async Task<HasteBinResult> PostAsync(string content, string language = default)
        {
            if (_baseUrl is null || _baseUrl == "")
                return new HasteBinResult { IsSuccess = false, StatusCode = 0 };

            switch (_hasteType)
            {
                case "haste":
                    return await PostHastebinAsync(content, language);
                case "tclip":
                    return await PostTclipAsync(content, language);
                default:
                    throw new NotSupportedException($"Haste type '{_hasteType}' is not supported.");
            }
        }

        public async Task<HasteBinResult> PostHastebinAsync(string content, string language)
        {
            string fullUrl = _baseUrl;
            if (!fullUrl.EndsWith("/"))
            {
                fullUrl += "/";
            }
            string postUrl = $"{fullUrl}documents";

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, new Uri(postUrl))
            {
                Content = new StringContent(content)
            };
            HttpResponseMessage result = await _httpClient.SendAsync(request);

            if (result.IsSuccessStatusCode)
            {
                string json = await result.Content.ReadAsStringAsync();
                HasteBinResult hasteBinResult = JsonConvert.DeserializeObject<HasteBinResult>(json);

                if (hasteBinResult?.Key is not null)
                {
                    hasteBinResult.FullUrl = $"{fullUrl}{hasteBinResult.Key}";
                    hasteBinResult.IsSuccess = true;
                    hasteBinResult.StatusCode = 200;
                    hasteBinResult.RawUrl = $"{fullUrl}raw/{hasteBinResult.Key}";

                    if (language != default)
                    {
                        hasteBinResult.FullUrl = $"{hasteBinResult.FullUrl}.{language}";
                    }
                    return hasteBinResult;
                }
            }

            return new HasteBinResult()
            {
                FullUrl = fullUrl,
                IsSuccess = false,
                StatusCode = (int)result.StatusCode
            };
        }

        public async Task<HasteBinResult> PostTclipAsync(string content, string language)
        {
            string fullUrl = _baseUrl;
            if (!fullUrl.EndsWith("/"))
            {
                fullUrl += "/";
            }
            string postUrl = $"{fullUrl}api/post";
            if (language == default || language == "")
                language = "txt";

            var formdata = new MultipartFormDataContent
            {
                { new StringContent(content), "content"},
                { new StringContent(Program.discord.CurrentUser.Username + "." + language), "filename" }
            };

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, new Uri(postUrl))
            {
                Content = formdata,
                Headers = { { "Accept", "text/plain" } }
            };

            HttpResponseMessage result = await _httpClient.SendAsync(request);

            if (result.StatusCode == HttpStatusCode.OK)
            {
                var responseText = await result.Content.ReadAsStringAsync();
                HasteBinResult hasteBinResult = new HasteBinResult
                {
                    FullUrl = responseText,
                    RawUrl = responseText + "/raw",
                    IsSuccess = true,
                    StatusCode = 200
                };
                return hasteBinResult;
            }

            return new HasteBinResult()
            {
                FullUrl = fullUrl,
                IsSuccess = false,
                StatusCode = (int)result.StatusCode
            };
        }

    }

    public class TclipRequest
    {
        [JsonProperty("content")]
        public string Content { get; set; }
        [JsonProperty("filename")]
        public string Filename { get; set; }
    }

    public class HasteBinResult
    {
        public string Key { get; set; } = null;
        public string FullUrl { get; set; }
        public string RawUrl { get; set; }
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
    }
}
