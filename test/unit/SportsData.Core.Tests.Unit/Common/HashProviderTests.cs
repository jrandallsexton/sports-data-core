using SportsData.Core.Common.Hashing;

using Xunit;

namespace SportsData.Core.Tests.Unit.Common
{
    public class HashProviderTests
    {
        [Fact]
        public void GenerateHashFromUrl_ShouldReturnConsistentHash()
        {
            var url = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams";

            var uri = new Uri(url);

            var hash1 = HashProvider.GenerateHashFromUri(uri);
            var hash2 = HashProvider.GenerateHashFromUri(uri);

            Assert.Equal(hash1, hash2); // Deterministic
            Assert.Matches("^[a-f0-9]{64}$", hash1); // SHA256 is 64 chars in hex
        }

        [Fact]
        public void SameBaseUri_DifferentQuery_HashShouldDiffer_WhenCleanFalse()
        {
            var uri1 = new Uri("http://example.com/data?page=1");
            var uri2 = new Uri("http://example.com/data?page=2");

            var hash1 = HashProvider.GenerateHashFromUri(uri1, cleanUrl: false);
            var hash2 = HashProvider.GenerateHashFromUri(uri2, cleanUrl: false);

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void SameBaseUri_DifferentQuery_HashShouldMatch_WhenCleanTrue()
        {
            var uri1 = new Uri("http://example.com/data?page=1");
            var uri2 = new Uri("http://example.com/data?page=2");

            var hash1 = HashProvider.GenerateHashFromUri(uri1, cleanUrl: true);
            var hash2 = HashProvider.GenerateHashFromUri(uri2, cleanUrl: true);

            Assert.Equal(hash1, hash2);
        }


    }
}
