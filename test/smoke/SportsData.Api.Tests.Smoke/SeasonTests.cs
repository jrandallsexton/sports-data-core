using System.Net;
using FluentAssertions;

namespace SportsData.Api.Tests.Smoke;

/// <summary>
/// Season overview requires Firebase authentication (not just API key).
/// These tests verify the endpoint exists and responds appropriately.
/// </summary>
public class SeasonTests : IClassFixture<SmokeTestFixture>
{
    private readonly SmokeTestFixture _fixture;

    public SeasonTests(SmokeTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(2024)]
    [InlineData(2023)]
    public async Task GetSeasonOverview_RequiresAuth(int seasonYear)
    {
        var response = await _fixture.GetRawAsync($"ui/season/{seasonYear}/overview");

        // This endpoint requires Firebase auth, API key alone won't work.
        // 401 is the expected response without a bearer token.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
