using System.Net.Http.Headers;
using System.Text.Json;

namespace SportsData.Api.Tests.Smoke;

public class SmokeTestFixture : IDisposable
{
    public HttpClient Client { get; }
    public string BaseUrl { get; }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public SmokeTestFixture()
    {
        BaseUrl = Environment.GetEnvironmentVariable("SMOKE_TEST_BASE_URL")
            ?? "https://api.sportdeets.com";

        var apiKey = Environment.GetEnvironmentVariable("SMOKE_TEST_API_KEY")
            ?? throw new InvalidOperationException(
                "SMOKE_TEST_API_KEY environment variable is required. " +
                "Set it before running smoke tests.");

        Client = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        Client.DefaultRequestHeaders.Add("X-Admin-Token", apiKey);
        Client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<T> GetAsync<T>(string url)
    {
        var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {url}");
    }

    public async Task<HttpResponseMessage> GetRawAsync(string url)
    {
        return await Client.GetAsync(url);
    }

    public void Dispose()
    {
        Client.Dispose();
    }
}
