using System.Net;
using FluentAssertions;

namespace SportsData.Api.Tests.Smoke;

/// <summary>
/// TeamCard endpoint tests. These depend on the canonical data provider
/// having fully enriched team card data for the given slug/season.
/// </summary>
public class TeamCardTests : IClassFixture<SmokeTestFixture>
{
    private readonly SmokeTestFixture _fixture;

    public TeamCardTests(SmokeTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("football", "ncaa", "lsu-tigers", 2024)]
    [InlineData("football", "ncaa", "alabama-crimson-tide", 2024)]
    [InlineData("football", "nfl", "dallas-cowboys", 2024)]
    [InlineData("football", "nfl", "kansas-city-chiefs", 2024)]
    public async Task GetTeamCard_DoesNotReturn500(string sport, string league, string slug, int seasonYear)
    {
        var response = await _fixture.GetRawAsync(
            $"ui/teamcard/sport/{sport}/league/{league}/team/{slug}/{seasonYear}");

        // TeamCard may 404 if data hasn't been fully enriched — that's acceptable.
        // A 500 indicates a broken query or missing config.
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetTeamCard_InvalidSlug_Returns404()
    {
        var response = await _fixture.GetRawAsync(
            "ui/teamcard/sport/football/league/ncaa/team/nonexistent-team/2024");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
