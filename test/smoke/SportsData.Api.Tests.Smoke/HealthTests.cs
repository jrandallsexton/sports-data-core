using System.Net;
using FluentAssertions;

namespace SportsData.Api.Tests.Smoke;

public class HealthTests : IClassFixture<SmokeTestFixture>
{
    private readonly SmokeTestFixture _fixture;

    public HealthTests(SmokeTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Health_Endpoint_ReturnsSuccessOrDegraded()
    {
        var response = await _fixture.GetRawAsync("api/health");

        // Health check may return 200 (healthy) or 503 (degraded/unhealthy)
        // A 500 indicates the health check itself is broken
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task LivenessProbe_Returns200()
    {
        var response = await _fixture.GetRawAsync("api/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
