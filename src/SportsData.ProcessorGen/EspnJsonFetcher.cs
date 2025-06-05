using SportsData.Core.Common;
using SportsData.Core.Common.Routing;

namespace SportsData.ProcessorGen;

public class EspnJsonFetcher
{
    private readonly HttpClient _httpClient;
    private readonly IGenerateRoutingKeys _routingKeyGenerator;

    public EspnJsonFetcher(HttpClient httpClient, IGenerateRoutingKeys routingKeyGenerator)
    {
        _httpClient = httpClient;
        _routingKeyGenerator = routingKeyGenerator;
    }

    /// <summary>
    /// Fetches the raw JSON from a given URL without saving it.
    /// </summary>
    public async Task<string> FetchJsonAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Saves the given JSON to disk based on its routing key.
    /// </summary>
    public async Task SaveJsonAsync(string json, string url, string outputDirectory, SourceDataProvider provider)
    {
        var routingKey = _routingKeyGenerator.Generate(provider, url);

        if (string.IsNullOrWhiteSpace(routingKey))
        {
            Console.WriteLine($"[Skipped] Could not generate routing key from: {url}");
            throw new Exception($"[Skipped] Could not generate routing key from: {url}");
        }

        var filePath = Path.Combine(outputDirectory, $"{routingKey}.json");

        Directory.CreateDirectory(outputDirectory); // Ensure path exists
        await File.WriteAllTextAsync(filePath, json);

        Console.WriteLine($"[Saved] {routingKey} → {filePath}");
    }

    /// <summary>
    /// Convenience method to fetch and save a JSON document only if not already present.
    /// </summary>
    public async Task<string> FetchAndSaveAsync(string url, string outputDirectory, SourceDataProvider provider)
    {
        var routingKey = _routingKeyGenerator.Generate(provider, url);

        if (string.IsNullOrWhiteSpace(routingKey))
        {
            Console.WriteLine($"[Skipped] Could not generate routing key from: {url}");
            throw new Exception($"[Skipped] Could not generate routing key from: {url}");
        }

        var filePath = Path.Combine(outputDirectory, $"{routingKey}.json");

        if (File.Exists(filePath))
        {
            Console.WriteLine($"[Cached] {routingKey} ← {filePath}");
            return await File.ReadAllTextAsync(filePath);
        }

        var json = await FetchJsonAsync(url);
        await SaveJsonAsync(json, url, outputDirectory, provider);

        return json;
    }
}
