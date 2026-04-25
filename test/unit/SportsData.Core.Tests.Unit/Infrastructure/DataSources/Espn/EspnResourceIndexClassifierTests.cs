using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.DataSources.Espn;

using Xunit;

namespace SportsData.Core.Tests.Unit.Infrastructure.DataSources.Espn
{
    public class EspnResourceIndexClassifierTests
    {
        // Sport-agnostic classification rules. These must hold for every sport
        // since they don't touch SportSpecificLeafSuffixes — verifying with
        // FootballNcaa is sufficient. Add a sport-parameterized companion test
        // below if any of these ever start to disagree across sports.
        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401767256/competitions/401767256/status?lang=en&region=us", false)]
        [InlineData("http://espn.com/v2/sports/football/events", true)]
        [InlineData("http://espn.com/v2/sports/football/events/401767256", false)]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2021/types/3/groups/12", false)]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401634255/competitions/401634255/competitors/2010/roster", false)]
        [InlineData("http://espn.com/v2/sports/football/events/401767256/competitions", true)]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401767256/competitions/401767256", false)]
        [InlineData("http://espn.com/v2/sports/football/leagues/college-football", true)]
        public void IsResourceIndex_Should_Classify_Uris(string uriString, bool expected)
        {
            var uri = new Uri(uriString);
            var actual = EspnResourceIndexClassifier.IsResourceIndex(uri, Sport.FootballNcaa);
            actual.Should().Be(expected);
        }

        // Sport-specific leaf-suffix override. MLB's `.../odds` endpoint returns
        // a paged collection where items lack `$ref` and top-level `id`; the
        // generic resource-index extraction path can't process those, so MLB
        // routes odds as a leaf and a sport-specific processor splits items
        // downstream. NCAAFB/NFL keep the original index behavior — items in
        // their odds wrappers each have a `$ref` that the generic path follows.
        [Theory]
        [InlineData(Sport.BaseballMlb, false)]
        [InlineData(Sport.FootballNcaa, true)]
        [InlineData(Sport.FootballNfl, true)]
        public void IsResourceIndex_OddsEndpoint_OnlyMlbIsLeaf(Sport sport, bool expected)
        {
            var uri = new Uri(
                "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844/odds?lang=en&region=us");

            var actual = EspnResourceIndexClassifier.IsResourceIndex(uri, sport);

            actual.Should().Be(expected);
        }
    }
}
