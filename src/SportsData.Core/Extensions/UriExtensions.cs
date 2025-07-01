using System;

namespace SportsData.Core.Extensions
{
    public static class UriExtensions
    {
        public static string ToCleanUrl(this Uri uri)
        {
            return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}".ToLowerInvariant();
        }

        public static Uri ToCleanUri(this Uri uri)
        {
            return new Uri($"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}");
        }
    }
}
