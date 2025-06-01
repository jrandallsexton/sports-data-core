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

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        Directory.CreateDirectory(outputDirectory); // Ensure path exists
        await File.WriteAllTextAsync(filePath, json);

        Console.WriteLine($"[Saved] {routingKey} → {filePath}");

        return json;
    }
}