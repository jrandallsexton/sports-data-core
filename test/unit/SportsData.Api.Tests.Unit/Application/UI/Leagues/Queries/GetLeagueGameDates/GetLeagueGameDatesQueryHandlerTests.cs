using FluentAssertions;

using FluentValidation.Results;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueGameDates;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Queries.GetLeagueGameDates;

// The handler owns the slug→Sport resolution and the game-dates mapping (the
// controller is thin). The raw SQL sits behind the mocked contest client, so
// this covers the handler's own logic in isolation.
public class GetLeagueGameDatesQueryHandlerTests
{
    private readonly Mock<IContestClientFactory> _factory = new();
    private readonly Mock<IProvideContests> _client = new();

    public GetLeagueGameDatesQueryHandlerTests()
    {
        _factory.Setup(x => x.Resolve(It.IsAny<Sport>())).Returns(_client.Object);
    }

    private GetLeagueGameDatesQueryHandler Sut() =>
        new(NullLogger<GetLeagueGameDatesQueryHandler>.Instance, _factory.Object);

    [Fact]
    public async Task ResolvesSportSlug_AndMapsGameDatesToDto()
    {
        var dates = new List<DateOnly> { new(2026, 7, 15), new(2026, 7, 16) };
        _client
            .Setup(x => x.GetGameDates(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<DateOnly>>(dates));

        var result = await Sut().ExecuteAsync(
            new GetLeagueGameDatesQuery("baseball", "mlb", null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.GameDates.Should().BeEquivalentTo(dates);
        _factory.Verify(x => x.Resolve(Sport.BaseballMlb), Times.Once);
    }

    [Fact]
    public async Task UnsupportedSportLeague_ReturnsBadRequest_WithoutCallingClient()
    {
        var result = await Sut().ExecuteAsync(
            new GetLeagueGameDatesQuery("cricket", "ipl", null, null));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
        _factory.Verify(x => x.Resolve(It.IsAny<Sport>()), Times.Never);
    }

    [Fact]
    public async Task PropagatesClientFailure()
    {
        _client
            .Setup(x => x.GetGameDates(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<List<DateOnly>>(new List<DateOnly>(), ResultStatus.NotFound, new List<ValidationFailure>()));

        var result = await Sut().ExecuteAsync(
            new GetLeagueGameDatesQuery("football", "nfl", null, null));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
