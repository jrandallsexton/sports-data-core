using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SportsData.Core.Common;
using SportsData.Core.Common.Routing;

using System.Text.Json;

namespace SportsData.ProcessorGen
{
    internal class Program
    {
        private class Result
        {
            public int Depth { get; set; }
            public string RoutingKey { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("SportsData Processor Generator is running...");

            var results = new List<Result>();
            var visited = new HashSet<string>();
            var allUrls = new List<string>();

            var urls = new List<string>
            {
                "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/2"
            };

            var httpClient = new HttpClient();
            var generator = new RoutingKeyGenerator();
            var fetcher = new EspnJsonFetcher(httpClient, generator);

            foreach (var url in urls)
            {
                await TraverseAsync(url, results, visited, allUrls, fetcher, generator);
            }

            await File.WriteAllTextAsync("C:\\temp\\results.json", FormatRoutingKeyMap(results));
            await File.WriteAllTextAsync("C:\\temp\\urls.json", FormatUrlList(allUrls));

            Console.WriteLine("SportsData Processor Generator completed");
            Console.ReadLine();
        }

        private static async Task TraverseAsync(
            string url,
            List<Result> results,
            HashSet<string> visited,
            List<string> allUrls,
            EspnJsonFetcher fetcher,
            RoutingKeyGenerator generator,
            int depth = 0,
            int maxDepth = 5)
        {
            if (depth > maxDepth)
            {
                Console.WriteLine($"[Depth {depth}] Max depth reached for {url}");
                return;
            }

            var normalizedUrl = NormalizeUrl(url);
            if (!visited.Add(normalizedUrl))
            {
                Console.WriteLine($"[Depth {depth}] Already visited URL: {normalizedUrl}");
                return;
            }

            allUrls.Add(normalizedUrl); // Record for logging

            Console.WriteLine($"[Depth {depth}] Attempting to generate routing key for: {url}");
            var routingKey = generator.Generate(SourceDataProvider.Espn, url);
            Console.WriteLine($"[Depth {depth}] RoutingKey: {routingKey}");

            var json = await fetcher.FetchAndSaveAsync(url, "D:\\Dropbox\\Code\\sports-data\\data", SourceDataProvider.Espn);

            // === SHIM: Generate DTO using Ollama ===
            //var dtoGen = new DtoGenerator("http://localhost:11434", "deepseek-coder-v2");
            //var generatedDto = await dtoGen.GenerateDtoFromJsonAsyncV2(json);
            //Console.WriteLine($"Generated DTO: {generatedDto}");
            // === END SHIM ===

            var extractor = new RefExtractor();
            var refs = extractor.ExtractRefs(json);

            Console.WriteLine($"[Depth {depth}] Extracted {refs.Count()} refs from: {normalizedUrl}");

            int countBefore = results.Count;

            foreach (var childUrl in refs)
            {
                var childKey = generator.Generate(SourceDataProvider.Espn, childUrl);

                if (results.Any(r => r.RoutingKey.Equals(childKey, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"[Depth {depth}] Skipping duplicate routing key: {childKey}");
                    continue;
                }

                results.Add(new Result
                {
                    RoutingKey = childKey,
                    Url = childUrl,
                    Depth = depth + 1
                });

                Console.WriteLine($"[Depth {depth + 1}] → {childKey}");
                await TraverseAsync(childUrl, results, visited, allUrls, fetcher, generator, depth + 1, maxDepth);
            }

            if (results.Count - countBefore >= 100)
            {
                Console.WriteLine($"[Depth {depth}] Saving intermediate results...");
                await File.WriteAllTextAsync("C:\\temp\\results.partial.json", FormatRoutingKeyMap(results));
            }
        }

        private static string NormalizeUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            }
            catch
            {
                return url.Split('?')[0].TrimEnd('/');
            }
        }

        private static string FormatRoutingKeyMap(List<Result> results)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            return JsonSerializer.Serialize(results, options);
        }

        private static string FormatUrlList(List<string> urls)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            return JsonSerializer.Serialize(urls.Distinct().OrderBy(u => u), options);
        }
    }
}
