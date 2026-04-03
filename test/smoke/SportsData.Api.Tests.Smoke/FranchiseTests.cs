using System.Net;
using FluentAssertions;

namespace SportsData.Api.Tests.Smoke;

public class FranchiseTests : IClassFixture<SmokeTestFixture>
{
    private readonly SmokeTestFixture _fixture;

    public FranchiseTests(SmokeTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("football", "ncaa")]
    [InlineData("football", "nfl")]
    public async Task GetFranchises_ReturnsResults(string sport, string league)
    {
        var response = await _fixture.GetAsync<PaginatedResponse<FranchiseItem>>(
            $"api/{sport}/{league}/franchises?pageSize=5");

        response.TotalCount.Should().BeGreaterThan(0);
        response.Items.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("football", "ncaa")]
    [InlineData("football", "nfl")]
    public async Task GetFranchises_ItemsHaveRequiredFields(string sport, string league)
    {
        var response = await _fixture.GetAsync<PaginatedResponse<FranchiseItem>>(
            $"api/{sport}/{league}/franchises?pageSize=5");

        foreach (var franchise in response.Items)
        {
            franchise.Id.Should().NotBeEmpty();
            franchise.Slug.Should().NotBeNullOrEmpty();
            franchise.Abbreviation.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData("football", "ncaa", "lsu-tigers")]
    [InlineData("football", "nfl", "dallas-cowboys")]
    public async Task GetFranchiseBySlug_Returns200(string sport, string league, string slug)
    {
        var response = await _fixture.GetRawAsync(
            $"api/{sport}/{league}/franchises/{slug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("football", "ncaa", "lsu-tigers")]
    [InlineData("football", "nfl", "dallas-cowboys")]
    public async Task GetFranchiseSeasons_ReturnsResults(string sport, string league, string slug)
    {
        var response = await _fixture.GetAsync<FranchiseSeasonsResponse>(
            $"api/{sport}/{league}/franchises/{slug}/seasons");

        response.FranchiseId.Should().NotBeEmpty();
        response.Items.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("football", "ncaa", "lsu-tigers", 2024)]
    [InlineData("football", "nfl", "dallas-cowboys", 2012)]
    public async Task GetFranchiseSeason_ReturnsValidData(string sport, string league, string slug, int seasonYear)
    {
        var response = await _fixture.GetAsync<FranchiseSeasonItem>(
            $"api/{sport}/{league}/franchises/{slug}/seasons/{seasonYear}");

        response.Id.Should().NotBeEmpty();
        response.SeasonYear.Should().Be(seasonYear);
        response.Abbreviation.Should().NotBeNullOrEmpty();
    }
}
