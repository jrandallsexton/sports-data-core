﻿using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using Xunit;

namespace SportsData.Core.Tests.Unit.Common
{
    public class HashProviderTests
    {
        [Fact]
        public void GenerateHashFromUrl_ShouldReturnConsistentHash()
        {
            var provider = new HashProvider();
            var url = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams";
            var hash1 = provider.GenerateHashFromUrl(url);
            var hash2 = provider.GenerateHashFromUrl(url);

            Assert.Equal(hash1, hash2); // Deterministic
            Assert.Matches("^[a-f0-9]{64}$", hash1); // SHA256 is 64 chars in hex
        }

    }
}
