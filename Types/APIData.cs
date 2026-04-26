namespace Cliptok.Types
{
    public class PhishingRequestBody
    {
        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class PhishingResponseBody
    {
        [JsonProperty("match")]
        public bool Match { get; set; }

        [JsonProperty("matches")]
        public List<PhishingMatch> Matches { get; set; }
    }

    public class PhishingMatch
    {
        [JsonProperty("followed")]
        public bool Followed { get; set; }

        [JsonProperty("domain")]
        public string Domain { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("trust_rating")]
        public float TrustRating { get; set; }
    }

    public class UsernameScamApiRequestJson
    {
        [JsonProperty("username")]
        public string Username { get; set; }
    }

    public class UsernameScamApiResponseJson
    {
        [JsonProperty("match")]
        public bool Match { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class ServerApiResponseJson
    {
        [JsonProperty("serverID")]
        public string ServerID { get; set; }

        [JsonProperty("match")]
        public bool Match { get; set; } = true;

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("vanity")]
        public string Vanity { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("invite")]
        public string Invite { get; set; }
    }
}
