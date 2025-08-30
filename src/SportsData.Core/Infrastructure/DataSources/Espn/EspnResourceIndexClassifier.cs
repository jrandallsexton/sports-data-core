using System;
using System.Collections.Generic;
using System.Linq;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    public class EspnResourceIndexClassifier
    {
        private static readonly HashSet<string> KnownLeafSuffixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "status",
            // Add more false-positive suffixes here as you find them
        };

        public static bool IsResourceIndex(Uri uri)
        {
            var last = uri.Segments
                           .LastOrDefault(s => !string.IsNullOrWhiteSpace(s))
                           ?.Trim('/')
                       ?? string.Empty;

            // Case 1: Ends in number → definitely a leaf
            if (long.TryParse(last, out _))
                return false;

            // Case 2: Ends in one of the known leaf suffixes (even though it's alpha) → treat as leaf
            if (KnownLeafSuffixes.Contains(last))
                return false;

            // Default: assume resource index
            return true;
        }
    }
}
