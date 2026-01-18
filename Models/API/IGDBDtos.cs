using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

internal class AuthToken//klasa przechowująca dane uwierzytelniające z twitch api
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; }

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }

    public DateTime ExpiryTime { get; set; }
}

public class IGDBClient//klient obsługujący komunikację z zewnętrznym api igdb
{
    private readonly string _clientId;
    private readonly string _clientSecret;
    private static readonly HttpClient _httpClient = new HttpClient();
    private AuthToken _token;

    public IGDBClient(string clientId, string clientSecret)
    {
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));

        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private bool IsTokenValid() => _token != null && _token.ExpiryTime > DateTime.UtcNow;//weryfikacja czy aktualny token nie wygasł

    private async Task GetAccessTokenAsync()//procedura uzyskiwania nowego tokena dostępu
    {
        var authUrl = $"https://id.twitch.tv/oauth2/token?client_id={_clientId}&client_secret={_clientSecret}&grant_type=client_credentials";

        try
        {
            var response = await _httpClient.PostAsync(authUrl, null);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            _token = JsonConvert.DeserializeObject<AuthToken>(jsonString);

            if (_token != null)
            {
                _token.ExpiryTime = DateTime.UtcNow.AddSeconds(_token.ExpiresIn - 10);//odjęcie marginesu błędu od czasu wygaśnięcia
            }
        }
        catch (HttpRequestException)
        {
            _token = null;
        }
    }

    public virtual async Task<string> ApiRequestAsync(string endpoint, string queryBody)//wysyłanie zapytań do konkretnych modułów api
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
            Content = new StringContent(queryBody, Encoding.UTF8, "text/plain")
        };

        request.Headers.Add("Client-ID", _clientId);
        request.Headers.Add("Authorization", $"Bearer {_token.AccessToken}");//nagłówek wymagany do autoryzacji zapytania

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException e)
        {
            if (e.StatusCode == System.Net.HttpStatusCode.Unauthorized || e.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _token = null;//wymuszenie odświeżenia tokena przy następnej próbie
            }
            return null;
        }
    }
}