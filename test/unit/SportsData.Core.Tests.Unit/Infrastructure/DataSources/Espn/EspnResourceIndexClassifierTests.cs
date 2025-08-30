using FluentAssertions;

using SportsData.Core.Infrastructure.DataSources.Espn;

using Xunit;

namespace SportsData.Core.Tests.Unit.Infrastructure.DataSources.Espn
{
    public class EspnResourceIndexClassifierTests
    {
        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401767256/competitions/401767256/status?lang=en&region=us", false)]
        [InlineData("http://espn.com/v2/sports/football/events", true)]
        [InlineData("http://espn.com/v2/sports/football/events/401767256", false)]
        [InlineData("http://espn.com/v2/sports/football/events/401767256/competitions", true)]
        [InlineData("http://espn.com/v2/sports/football/events/401767256/competitions/401767256", false)]
        [InlineData("http://espn.com/v2/sports/football/leagues/college-football", false)]
        public void IsResourceIndex_Should_Classify_Uris(string uriString, bool expected)
        {
            var uri = new Uri(uriString);
            var actual = EspnResourceIndexClassifier.IsResourceIndex(uri);
            actual.Should().Be(expected);
        }
    }
}
