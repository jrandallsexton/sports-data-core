using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SportsData.Core.Common.Hashing
{
    public static class DeterministicGuid
    {
        public static Guid Combine(params string[] inputs)
        {
            var input = string.Join("|", inputs);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var bytes = hash.Take(16).ToArray();
            return new Guid(bytes);
        }

        public static Guid Combine(Guid id, int season)
            => Combine(id.ToString(), season.ToString());
    }

}
