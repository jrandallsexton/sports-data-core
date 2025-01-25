using System;
using System.Security.Cryptography;
using System.Text;

namespace SportsData.Core.Common
{
    public interface IProvideHashes
    {
        int GenerateHashFromUrl(string url);
    }

    public class HashProvider : IProvideHashes
    {
        public int GenerateHashFromUrl(string url)
        {
            using SHA256 sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
            var hash = BitConverter.ToInt32(bytes, 0);
            return hash;
        }
    }
}
