using System.Net;
using FluentAssertions;

namespace SportsData.Api.Tests.Smoke;

public class ConferenceTests : IClassFixture<SmokeTestFixture>
{
    private readonly SmokeTestFixture _fixture;

    public ConferenceTests(SmokeTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetConferences_Returns200()
    {
        var response = await _fixture.GetRawAsync("ui/conferences");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
