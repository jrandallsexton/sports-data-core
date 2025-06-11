using System;
using System.Security.Cryptography;
using System.Text;
using SportsData.Core.Extensions;

namespace SportsData.Core.Common.Hashing
{
    public static class HashProvider
    {
        public static string GenerateHashFromUri(Uri uri)
        {
            var url = uri.ToCleanUrl();
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public static string UrlHash(this string url, bool throwOnInvalid = false)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return GenerateHashFromUri(uri);

            if (throwOnInvalid)
                throw new ArgumentException($"Invalid URL: '{url}'");

            return string.Empty;
        }
    }
}