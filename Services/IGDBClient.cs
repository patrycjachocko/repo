using Newtonsoft.Json;

namespace praca_dyplomowa_zesp.Models.API
{
    public class AuthToken //klasa przechowująca dane uwierzytelniające z IGDB api
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } //klucz dostępu

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; } //czas ważności klucza

        public DateTime ExpiryTime { get; set; } //wyliczona data i godzina wygaśnięcia klucza
    }
}