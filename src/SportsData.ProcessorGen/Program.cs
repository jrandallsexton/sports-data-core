using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SportsData.Core.Common;
using SportsData.Core.Common.Routing;

using System.Text.Json;
using SportsData.Core.Extensions;

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
                await TraverseAsync(new Uri(url), results, visited, allUrls, fetcher, generator);
            }

            await File.WriteAllTextAsync("C:\\temp\\results.json", FormatRoutingKeyMap(results));
            await File.WriteAllTextAsync("C:\\temp\\urls.json", FormatUrlList(allUrls));

            Console.WriteLine("SportsData Processor Generator completed");
            Console.ReadLine();
        }

        private static async Task TraverseAsync(
            Uri uri,
            List<Result> results,
            HashSet<string> visited,
            List<string> allUrls,
            EspnJsonFetcher fetcher,
            RoutingKeyGenerator generator,
            int depth = 0,
            int maxDepth = 6)
        {
            if (depth > maxDepth)
            {
                Console.WriteLine($"[Depth {depth}] Max depth reached for {uri.ToCleanUrl()}");
                return;
            }

            var normalizedUrl = NormalizeUrl(uri.ToCleanUrl());
            if (!visited.Add(normalizedUrl))
            {
                Console.WriteLine($"[Depth {depth}] Already visited URL: {normalizedUrl}");
                return;
            }

            allUrls.Add(normalizedUrl); // Record for logging

            Console.WriteLine($"[Depth {depth}] Fetching: {uri}");
            var json = await fetcher.FetchJsonAsync(uri);

            // === SHIM: Generate DTO using Ollama ===
            //var dtoGen = new DtoGenerator("http://localhost:11434", "deepseek-coder-v2");
            //var generatedDto = await dtoGen.GenerateDtoFromJsonAsyncV2(json);
            //Console.WriteLine($"Generated DTO: {generatedDto}");
            // === END SHIM ===

            var extractor = new RefExtractor();
            var refs = extractor.ExtractRefs(json);

            // Heuristic: treat documents with only $refs and no ID as index containers
            bool isResourceIndex = refs.Count > 0 &&
                (json.Contains("\"items\"") || json.Contains("\"entries\"")) &&
                !json.Contains("\"id\"");

            if (isResourceIndex)
            {
                Console.WriteLine($"[Depth {depth}] Skipped save — index document: {uri}");
            }
            else
            {
                var routingKey = generator.Generate(SourceDataProvider.Espn, uri);
                if (!string.IsNullOrWhiteSpace(routingKey))
                {
                    await fetcher.SaveJsonAsync(json, uri, "D:\\Dropbox\\Code\\sports-data\\data", SourceDataProvider.Espn);

                    results.Add(new Result
                    {
                        RoutingKey = routingKey,
                        Url = normalizedUrl,
                        Depth = depth
                    });
                }
                else
                {
                    Console.WriteLine($"[Depth {depth}] Could not generate routing key for: {uri}");
                }
            }

            Console.WriteLine($"[Depth {depth}] Extracted {refs.Count} refs from: {normalizedUrl}");

            foreach (var childUrl in refs)
            {
                Console.WriteLine($"[Depth {depth + 1}] → {childUrl}");
                await TraverseAsync(new Uri(childUrl), results, visited, allUrls, fetcher, generator, depth + 1, maxDepth);
            }

            // Save partial results occasionally (optional)
            if (results.Count % 100 == 0)
            {
                Console.WriteLine($"[Depth {depth}] Saving intermediate results...");
                await File.WriteAllTextAsync("C:\\temp\\results.partial.json", Program.FormatRoutingKeyMap(results));
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
