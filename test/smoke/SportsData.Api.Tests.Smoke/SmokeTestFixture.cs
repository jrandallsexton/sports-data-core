using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.Extensions.Configuration;

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
        // Env vars provide the baseline (also the sole source in CI); user-secrets are
        // added last so developers can override an ambient env var per-project without
        // touching their shell profile. Secrets live under %APPDATA%\Microsoft\UserSecrets
        // and never hit the repo. Seed locally with:
        //     dotnet user-secrets set SMOKE_TEST_API_KEY <value> \
        //         --project test/smoke/SportsData.Api.Tests.Smoke
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets<SmokeTestFixture>(optional: true)
            .Build();

        BaseUrl = config["SMOKE_TEST_BASE_URL"]
            ?? "https://api.sportdeets.com";

        var apiKey = config["SMOKE_TEST_API_KEY"]
            ?? throw new InvalidOperationException(
                "SMOKE_TEST_API_KEY is required. Set it via:\n" +
                "  dotnet user-secrets set SMOKE_TEST_API_KEY <value> " +
                "--project test/smoke/SportsData.Api.Tests.Smoke\n" +
                "or as an environment variable before running smoke tests.");

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
