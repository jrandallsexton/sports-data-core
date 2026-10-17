using FluentAssertions;

using FluentValidation.Results;

using Moq;

using SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutDrives;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Competition;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Diagnostics.Queries.GetCompetitionsWithoutDrives;

/// <summary>
/// API aggregates each sport's Producer answer (it never queries a Producer
/// database). All or nothing: a missing sport would read as "clean".
/// </summary>
public class GetCompetitionsWithoutDrivesQueryHandlerTests : ApiTestBase<GetCompetitionsWithoutDrivesQueryHandler>
{
    private readonly Dictionary<Sport, Mock<IProvideContests>> _producers = new()
    {
        [Sport.FootballNcaa] = new Mock<IProvideContests>(),
        [Sport.FootballNfl] = new Mock<IProvideContests>()
    };

    public GetCompetitionsWithoutDrivesQueryHandlerTests()
    {
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns((Sport sport) => _producers.TryGetValue(sport, out var producer)
                ? producer.Object
                : throw new InvalidOperationException($"Unexpected Producer call for {sport}"));
    }

    private void ProducerReturns(Sport sport, params CompetitionWithoutDrivesDto[] rows) =>
        _producers[sport]
            .Setup(x => x.GetCompetitionsWithoutDrives(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<CompetitionWithoutDrivesDto>>(rows.ToList()));

    private static CompetitionWithoutDrivesDto Row(Sport sport, string name) => new()
    {
        Sport = sport,
        CompetitionId = Guid.NewGuid(),
        ContestId = Guid.NewGuid(),
        ContestName = name
    };

    [Fact]
    public async Task ExecuteAsync_MergesEveryProducer_InSportOrder()
    {
        var ncaa = Row(Sport.FootballNcaa, "Army at Navy");
        var nfl = Row(Sport.FootballNfl, "Bears at Packers");
        ProducerReturns(Sport.FootballNcaa, ncaa);
        ProducerReturns(Sport.FootballNfl, nfl);

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutDrivesQueryHandler>();

        var result = await handler.ExecuteAsync(new GetCompetitionsWithoutDrivesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(ncaa, nfl);
    }

    [Fact]
    public async Task ExecuteAsync_PassesTheSeasonYearToEveryProducer()
    {
        ProducerReturns(Sport.FootballNcaa);
        ProducerReturns(Sport.FootballNfl);

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutDrivesQueryHandler>();

        await handler.ExecuteAsync(new GetCompetitionsWithoutDrivesQuery(2025), CancellationToken.None);

        foreach (var producer in _producers.Values)
        {
            producer.Verify(x => x.GetCompetitionsWithoutDrives(2025, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task ExecuteAsync_FailsWhole_NamingTheSport_WhenOneProducerFails()
    {
        ProducerReturns(Sport.FootballNcaa, Row(Sport.FootballNcaa, "Army at Navy"));
        _producers[Sport.FootballNfl]
            .Setup(x => x.GetCompetitionsWithoutDrives(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<List<CompetitionWithoutDrivesDto>>(
                [], ResultStatus.Error, [new ValidationFailure("x", "boom")]));

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutDrivesQueryHandler>();

        var result = await handler.ExecuteAsync(new GetCompetitionsWithoutDrivesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        var failure = (Failure<List<CompetitionWithoutDrivesDto>>)result;
        failure.Value.Should().BeEmpty("a partial list would report the failed sport as clean");
        failure.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(Sport.FootballNfl));
    }

    [Fact]
    public async Task ExecuteAsync_FailsWhole_WhenAProducerIsUnreachable()
    {
        ProducerReturns(Sport.FootballNfl, Row(Sport.FootballNfl, "Bears at Packers"));
        _producers[Sport.FootballNcaa]
            .Setup(x => x.GetCompetitionsWithoutDrives(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutDrivesQueryHandler>();

        var result = await handler.ExecuteAsync(new GetCompetitionsWithoutDrivesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        ((Failure<List<CompetitionWithoutDrivesDto>>)result).Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(Sport.FootballNcaa));
    }
}
