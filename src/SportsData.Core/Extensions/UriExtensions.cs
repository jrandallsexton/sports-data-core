using System;

namespace SportsData.Core.Extensions
{
    public static class UriExtensions
    {
        /// <summary>
        /// Converts URI to a clean, normalized string for canonical ID generation.
        /// ALWAYS uses HTTP (not HTTPS) to ensure stable canonical IDs regardless of how ESPN returns refs.
        /// </summary>
        public static string ToCleanUrl(this Uri uri)
        {
            return $"http://{uri.Host}{uri.AbsolutePath}".ToLowerInvariant();
        }

        /// <summary>
        /// Converts URI to a clean, normalized URI for canonical ID generation.
        /// ALWAYS uses HTTP (not HTTPS) to ensure stable canonical IDs regardless of how ESPN returns refs.
        /// </summary>
        public static Uri ToCleanUri(this Uri uri)
        {
            return new Uri($"http://{uri.Host}{uri.AbsolutePath}");
        }
    }
}
