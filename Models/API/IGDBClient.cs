// Plik: IGDBClient.cs
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

// Prosta klasa do przechowywania tokena i czasu jego wygaśnięcia
internal class AuthToken
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; }

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }

    public DateTime ExpiryTime { get; set; }
}

public class IGDBClient
{
    private readonly string _clientId;
    private readonly string _clientSecret;
    private static readonly HttpClient _httpClient = new HttpClient();
    private AuthToken _token;

    public IGDBClient(string clientId, string clientSecret)
    {
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private bool IsTokenValid() => _token != null && _token.ExpiryTime > DateTime.UtcNow;

    private async Task GetAccessTokenAsync()
    {
        Console.WriteLine("Pobieram nowy token dostępu...");
        var authUrl = $"https://id.twitch.tv/oauth2/token?client_id={_clientId}&client_secret={_clientSecret}&grant_type=client_credentials";
        try
        {
            var response = await _httpClient.PostAsync(authUrl, null);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            _token = JsonConvert.DeserializeObject<AuthToken>(jsonString);

            if (_token != null)
            {
                _token.ExpiryTime = DateTime.UtcNow.AddSeconds(_token.ExpiresIn - 10);
                Console.WriteLine("Token został pomyślnie uzyskany.");
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Błąd podczas uzyskiwania tokena: {e.Message}");
            _token = null;
        }
    }

    public async Task<string> ApiRequestAsync(string endpoint, string queryBody)
    {
        if (!IsTokenValid())
        {
            await GetAccessTokenAsync();
            if (!IsTokenValid()) return null;
        }

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"https://api.igdb.com/v4/{endpoint}"),
            Content = new StringContent(queryBody, Encoding.UTF8, "text/plain"),
            Headers =
            {
                { "Client-ID", _clientId },
                { "Authorization", $"Bearer {_token.AccessToken}" }
            }
        };

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Błąd zapytania do API: {e.Message}");
            if (e.StatusCode == System.Net.HttpStatusCode.Unauthorized || e.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _token = null;
                Console.WriteLine("Token unieważniony. Następne zapytanie spróbuje go odświeżyć.");
            }
            return null;
        }
    }
}