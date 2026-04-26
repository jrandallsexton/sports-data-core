using System;
using System.Collections.Generic;
using System.Linq;

using SportsData.Core.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    public class EspnResourceIndexClassifier
    {
        private static readonly HashSet<string> KnownLeafSuffixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "broadcasts",
            "futures",
            "leaders",
            "predictor",
            "roster",
            "score",
            "situation",
            "statistics",
            "status"
        };

        /// <summary>
        /// Per-sport leaf overrides for endpoints whose response shape is
        /// sport-specific. MLB's <c>.../odds</c> endpoint returns a paged
        /// collection where items lack both <c>$ref</c> and a top-level
        /// <c>id</c>, so the generic ResourceIndex extraction path in
        /// <c>DocumentRequestedHandler.ProcessResourceIndex</c> can't break
        /// items out individually the way it can for NCAAFB/NFL (where each
        /// item carries its own <c>$ref</c>). Routing the URL as a leaf for
        /// MLB sends the full wrapper to a sport-specific processor that
        /// splits items downstream.
        /// </summary>
        private static readonly Dictionary<Sport, HashSet<string>> SportSpecificLeafSuffixes = new()
        {
            [Sport.BaseballMlb] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "odds"
            }
        };

        /// <summary>
        /// Classify a URI as a resource index (paginated collection) or a leaf
        /// document. Sport-aware so endpoints that return collection-shaped
        /// responses for one sport but require single-document handling for
        /// another can be routed correctly. The default rules (numeric tail,
        /// shared <see cref="KnownLeafSuffixes"/>) apply to every sport;
        /// <see cref="SportSpecificLeafSuffixes"/> layers per-sport overrides
        /// on top.
        /// </summary>
        public static bool IsResourceIndex(Uri uri, Sport sport)
        {
            var last = uri.Segments
                           .LastOrDefault(s => !string.IsNullOrWhiteSpace(s))
                           ?.Trim('/')
                       ?? string.Empty;

            // Case 1: Ends in number → definitely a leaf
            if (long.TryParse(last, out _))
                return false;

            // Case 2: Ends in one of the shared known leaf suffixes → treat as leaf
            if (KnownLeafSuffixes.Contains(last))
                return false;

            // Case 3: Sport-specific leaf override (e.g. MLB odds wrapper) → treat as leaf
            if (SportSpecificLeafSuffixes.TryGetValue(sport, out var sportSuffixes) &&
                sportSuffixes.Contains(last))
                return false;

            // Default: assume resource index
            return true;
        }
    }
}
