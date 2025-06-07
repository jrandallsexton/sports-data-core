using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SportsData.Core.Common.Routing
{
    public interface IGenerateRoutingKeys
    {
        string Generate(SourceDataProvider provider, string url);
    }

    public class RoutingKeyGenerator : IGenerateRoutingKeys
    {
        public string Generate(SourceDataProvider provider, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL must not be null or empty", nameof(url));

            var uri = new Uri(url);
            var path = uri.AbsolutePath;

            var rawSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (rawSegments.Length == 1 && rawSegments[0].Equals("v2", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var cleaned = new List<string>();

            for (int i = 0; i < rawSegments.Length; i++)
            {
                var segment = rawSegments[i];

                // Skip placeholders
                if (IsPlaceholder(segment))
                    continue;

                // Always skip numeric segments that follow known plural containers
                if (IsPureNumber(segment))
                {
                    var previous = i > 0 ? rawSegments[i - 1].ToLowerInvariant() : string.Empty;
                    if (_entityParents.Contains(previous))
                        continue;

                    // Also skip if it's just a lone numeric path like `/colleges/2`
                    continue;
                }

                cleaned.Add(segment.ToLowerInvariant());
            }

            if (cleaned.Count == 0)
            {
                Console.WriteLine($"[Skipped] No usable segments from: {url}");
                return string.Empty;
            }

            var key = string.Join('.', cleaned);
            return $"{provider.ToString().ToLowerInvariant()}.{key}";
        }

        private static bool IsPlaceholder(string segment) =>
            Regex.IsMatch(segment, @"^\{.*\}$");

        private static bool IsPureNumber(string segment) =>
            Regex.IsMatch(segment, @"^\d+$");

        private static readonly HashSet<string> _entityParents = new(StringComparer.OrdinalIgnoreCase)
        {
            "sports", "leagues", "seasons", "types", "groups", "teams", "franchises", "venues",
            "events", "competitions", "coaches", "athletes", "notes", "injuries", "providers",
            "media", "colleges", "statistics", "status", "situation", "score", "linescores",
            "drives", "plays", "record", "odds", "awards", "rankings", "futures", "calendar",
            "weeks", "broadcasts", "tickets", "competitors", "scores", "positions"
        };
    }
}
