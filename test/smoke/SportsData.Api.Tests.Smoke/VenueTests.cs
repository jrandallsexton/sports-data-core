using System.Net;
using FluentAssertions;

namespace SportsData.Api.Tests.Smoke;

public class VenueTests : IClassFixture<SmokeTestFixture>
{
    private readonly SmokeTestFixture _fixture;

    public VenueTests(SmokeTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("football", "ncaa")]
    [InlineData("football", "nfl")]
    public async Task GetVenues_ReturnsResults(string sport, string league)
    {
        var response = await _fixture.GetAsync<PaginatedResponse<VenueItem>>(
            $"api/{sport}/{league}/venues?pageSize=5");

        response.TotalCount.Should().BeGreaterThan(0);
        response.Items.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("football", "ncaa")]
    [InlineData("football", "nfl")]
    public async Task GetVenues_ItemsHaveRequiredFields(string sport, string league)
    {
        var response = await _fixture.GetAsync<PaginatedResponse<VenueItem>>(
            $"api/{sport}/{league}/venues?pageSize=5");

        foreach (var venue in response.Items)
        {
            venue.Id.Should().NotBeEmpty();
            venue.Name.Should().NotBeNullOrEmpty();
            venue.Slug.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData("football", "ncaa")]
    [InlineData("football", "nfl")]
    public async Task GetVenues_HateoasLinksPresent(string sport, string league)
    {
        var response = await _fixture.GetAsync<PaginatedResponse<VenueItem>>(
            $"api/{sport}/{league}/venues?pageSize=5");

        foreach (var venue in response.Items)
        {
            venue.Ref.Should().NotBeNull("every venue should have a self-ref URI");
            venue.Links.Should().NotBeNullOrEmpty("every venue should have HATEOAS links");
        }
    }

    [Theory]
    [InlineData("football", "ncaa")]
    [InlineData("football", "nfl")]
    public async Task GetVenues_HateoasLinksAreWellFormed(string sport, string league)
    {
        var response = await _fixture.GetAsync<PaginatedResponse<VenueItem>>(
            $"api/{sport}/{league}/venues?pageSize=1");

        response.Items.Should().NotBeEmpty("need at least one venue to validate HATEOAS links");
        var firstVenue = response.Items.First();
        firstVenue.Ref.Should().NotBeNull();

        // Verify the self-ref is a well-formed absolute URL
        var isValidUri = Uri.TryCreate(firstVenue.Ref, UriKind.Absolute, out var uri);
        isValidUri.Should().BeTrue($"Ref '{firstVenue.Ref}' should be a valid absolute URI");
        uri!.PathAndQuery.Should().Contain($"venues/{firstVenue.Id}");
    }
}
