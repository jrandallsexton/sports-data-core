using System;
using System.Security.Cryptography;
using System.Text;

namespace SportsData.Core.Common.Hashing
{
    public static class HashProvider
    {
        public static string GenerateHashFromUrl(string url)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public static string UrlHash(this string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            return GenerateHashFromUrl(url);
        }
    }
}