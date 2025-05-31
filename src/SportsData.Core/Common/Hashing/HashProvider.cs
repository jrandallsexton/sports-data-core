using System;
using System.Security.Cryptography;
using System.Text;

namespace SportsData.Core.Common.Hashing
{
    public interface IProvideHashes
    {
        string GenerateHashFromUrl(string url);
    }

    public class HashProvider : IProvideHashes
    {
        public string GenerateHashFromUrl(string url)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
            return Convert.ToHexString(hashBytes).ToLowerInvariant(); // or just .ToUpperInvariant()
        }
    }
}
