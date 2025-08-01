using SportsData.Core.Extensions;

using System;
using System.Security.Cryptography;
using System.Text;

namespace SportsData.Core.Common.Hashing
{
    public static class HashProvider
    {
        public static string GenerateHashFromUri(Uri uri, bool cleanUrl = true)
        {
            var url = cleanUrl ? uri.ToCleanUrl() : uri.AbsoluteUri;
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public static string UrlHash(this string url, bool cleanUrl = true)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return GenerateHashFromUri(uri, cleanUrl);

            return string.Empty;
        }
    }
}