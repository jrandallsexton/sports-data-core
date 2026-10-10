#nullable enable
using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

using SportsData.Core.Common;
using SportsData.Producer.Application.Competitions.InstantAnalysis;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions.InstantAnalysis;

public class FootballPlayCompletedConsumerHandlerTests : ProducerTestBase<FootballPlayCompletedConsumerHandler>
{
    // Seed timestamp for NOT NULL columns only; never asserted against.
    private static readonly DateTime FixedTestNow = new(2026, 10, 10, 18, 0, 0, DateTimeKind.Utc);

    private readonly FootballPlayCompletedConsumerHandler _sut;

    public FootballPlayCompletedConsumerHandlerTests()
    {
        _sut = Mocker.CreateInstance<FootballPlayCompletedConsumerHandler>();
    }

    [Fact]
    public async Task Process_WhenGameNotOptedIn_DoesNotAnalyse()
    {
        var (competitionId, playId) = await SeedPlay(liveAnalysisEnabled: false);

        await _sut.Process(FootballPlayCompletedConsumerTests.BuildEvent(competitionId, playId));

        Mocker.GetMock<IPlayAnalyser>().Verify(x => x.Analyse(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<SeasonDocumentRule>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenGameOptedIn_AnalysesLive_WithTheNewestSeasonDocuments()
    {
        var (competitionId, playId) = await SeedPlay(liveAnalysisEnabled: true);
        var evt = FootballPlayCompletedConsumerTests.BuildEvent(competitionId, playId);

        await _sut.Process(evt);

        Mocker.GetMock<IPlayAnalyser>().Verify(x => x.Analyse(
                playId, evt.ContestId, evt.CorrelationId, null, SeasonDocumentRule.Newest),
            Times.Once);
    }

    private async Task<(Guid CompetitionId, Guid PlayId)> SeedPlay(bool liveAnalysisEnabled)
    {
        var competitionId = Guid.NewGuid();
        var playId = Guid.NewGuid();
        var passer = Guid.NewGuid();
        var defender = Guid.NewGuid();

        await FootballDataContext.AthleteSeasons.AddRangeAsync(
            new FootballAthleteSeason { Id = passer, ShortName = "J. Smith", CreatedUtc = FixedTestNow },
            new FootballAthleteSeason { Id = defender, ShortName = "T. Cooley", CreatedUtc = FixedTestNow });

        await FootballDataContext.CompetitionStreams.AddAsync(new CompetitionStream
        {
            CompetitionId = competitionId,
            SeasonWeekId = Guid.NewGuid(),
            ScheduledTimeUtc = FixedTestNow,
            BackgroundJobId = "job-1",
            LiveAnalysisEnabled = liveAnalysisEnabled,
            CreatedUtc = FixedTestNow
        });

        await FootballDataContext.CompetitionPlays.AddAsync(new FootballCompetitionPlay
        {
            Id = playId,
            CompetitionId = competitionId,
            EspnId = "4017",
            SequenceNumber = "512",
            Type = PlayType.PassInterceptionReturn,
            TypeId = "26",
            Text = "pass intercepted by #23",
            StatYardage = 21,
            CreatedUtc = FixedTestNow,
            Participants =
            [
                new FootballCompetitionPlayParticipant { CompetitionPlayId = playId, Type = "passer", Order = 0, AthleteSeasonId = passer, CreatedUtc = FixedTestNow },
                new FootballCompetitionPlayParticipant { CompetitionPlayId = playId, Type = "passDefender", Order = 1, AthleteSeasonId = defender, CreatedUtc = FixedTestNow }
            ]
        });

        await FootballDataContext.SaveChangesAsync();
        return (competitionId, playId);
    }
}
