using FluentAssertions;

using FluentValidation.Results;

using Moq;

using SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutCompetitors;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Competition;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Diagnostics.Queries.GetCompetitionsWithoutCompetitors;

/// <summary>
/// API aggregates each sport's Producer answer (it never queries a Producer
/// database). All or nothing: a missing sport would read as "clean".
/// </summary>
public class GetCompetitionsWithoutCompetitorsQueryHandlerTests : ApiTestBase<GetCompetitionsWithoutCompetitorsQueryHandler>
{
    private readonly Dictionary<Sport, Mock<IProvideContests>> _producers = new()
    {
        [Sport.FootballNcaa] = new Mock<IProvideContests>(),
        [Sport.FootballNfl] = new Mock<IProvideContests>()
    };

    public GetCompetitionsWithoutCompetitorsQueryHandlerTests()
    {
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns((Sport sport) => _producers.TryGetValue(sport, out var producer)
                ? producer.Object
                : throw new InvalidOperationException($"Unexpected Producer call for {sport}"));
    }

    private void ProducerReturns(Sport sport, params CompetitionWithoutCompetitorsDto[] rows) =>
        _producers[sport]
            .Setup(x => x.GetCompetitionsWithoutCompetitors(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<CompetitionWithoutCompetitorsDto>>(rows.ToList()));

    private static CompetitionWithoutCompetitorsDto Row(Sport sport, string name, int competitorCount = 0) => new()
    {
        Sport = sport,
        CompetitionId = Guid.NewGuid(),
        ContestId = Guid.NewGuid(),
        CompetitionName = name,
        CompetitorCount = competitorCount
    };

    [Fact]
    public async Task ExecuteAsync_MergesEveryProducer_InSportOrder()
    {
        var ncaa = Row(Sport.FootballNcaa, "Army at Navy");
        var nfl1 = Row(Sport.FootballNfl, "Bears at Packers", 1);
        var nfl2 = Row(Sport.FootballNfl, "Jets at Bills", 3);
        ProducerReturns(Sport.FootballNcaa, ncaa);
        ProducerReturns(Sport.FootballNfl, nfl1, nfl2);

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutCompetitorsQueryHandler>();

        var result = await handler.ExecuteAsync(new GetCompetitionsWithoutCompetitorsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(ncaa, nfl1, nfl2);
        Mocker.GetMock<IContestClientFactory>().Verify(x => x.Resolve(Sport.FootballNcaa), Times.Once);
        Mocker.GetMock<IContestClientFactory>().Verify(x => x.Resolve(Sport.FootballNfl), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsEmptyList_WhenEveryProducerIsClean()
    {
        ProducerReturns(Sport.FootballNcaa);
        ProducerReturns(Sport.FootballNfl);

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutCompetitorsQueryHandler>();

        var result = await handler.ExecuteAsync(new GetCompetitionsWithoutCompetitorsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_FailsWhole_NamingTheSport_WhenOneProducerReturnsAFailure()
    {
        ProducerReturns(Sport.FootballNcaa, Row(Sport.FootballNcaa, "Army at Navy"));
        _producers[Sport.FootballNfl]
            .Setup(x => x.GetCompetitionsWithoutCompetitors(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<List<CompetitionWithoutCompetitorsDto>>(
                [], ResultStatus.Error, [new ValidationFailure("x", "boom")]));

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutCompetitorsQueryHandler>();

        var result = await handler.ExecuteAsync(new GetCompetitionsWithoutCompetitorsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        var failure = (Failure<List<CompetitionWithoutCompetitorsDto>>)result;
        failure.Value.Should().BeEmpty("a partial list would report the failed sport as clean");
        failure.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(Sport.FootballNfl));
    }

    [Fact]
    public async Task ExecuteAsync_FailsWhole_WhenAProducerIsUnreachable()
    {
        ProducerReturns(Sport.FootballNfl, Row(Sport.FootballNfl, "Bears at Packers"));
        _producers[Sport.FootballNcaa]
            .Setup(x => x.GetCompetitionsWithoutCompetitors(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutCompetitorsQueryHandler>();

        var result = await handler.ExecuteAsync(new GetCompetitionsWithoutCompetitorsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        ((Failure<List<CompetitionWithoutCompetitorsDto>>)result).Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(Sport.FootballNcaa));
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        foreach (var producer in _producers.Values)
        {
            producer
                .Setup(x => x.GetCompetitionsWithoutCompetitors(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TaskCanceledException());
        }

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutCompetitorsQueryHandler>();

        var act = () => handler.ExecuteAsync(new GetCompetitionsWithoutCompetitorsQuery(), cts.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }
}
