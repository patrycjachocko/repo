using Newtonsoft.Json;
using System;

namespace praca_dyplomowa_zesp.Models.API//miejsce dla struktur danych api
{
    public class AuthToken//klasa przechowująca dane uwierzytelniające z twitch api
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        public DateTime ExpiryTime { get; set; }
    }
}