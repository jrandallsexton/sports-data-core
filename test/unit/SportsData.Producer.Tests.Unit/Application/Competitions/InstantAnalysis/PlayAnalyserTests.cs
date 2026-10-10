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

public class PlayAnalyserTests : ProducerTestBase<PlayAnalyser>
{
    // Seed timestamp for NOT NULL columns only; never asserted against.
    private static readonly DateTime FixedTestNow = new(2026, 10, 10, 18, 0, 0, DateTimeKind.Utc);

    private readonly PlayAnalyser _sut;

    public PlayAnalyserTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedTestNow);
        _sut = Mocker.CreateInstance<PlayAnalyser>();
    }

    private static readonly Guid ContestId = Guid.NewGuid();
    private static readonly Guid CorrelationId = Guid.NewGuid();

    [Fact]
    public async Task Analyse_PersistsTheOrderedFactSet()
    {
        var (competitionId, playId) = await SeedPlay(liveAnalysisEnabled: false);

        var outcome = await Live(playId);

        outcome.Status.Should().Be(PlayAnalysisStatus.Analysed);
        outcome.Persisted.Should().Be(2);
        VerifyLogged("Play analysed", Times.Once(), "Kinds=Interception");

        // No season documents are seeded, so only game facts survive:
        // the defender's first interception today and the passer's first thrown.
        var rows = FootballDataContext.PlayAnalyses.OrderBy(x => x.Ordinal).ToList();
        rows.Select(x => x.Text).Should().Equal(
            "T. Cooley's first interception of the game.",
            "J. Smith's first interception thrown this game.");
        rows.Select(x => x.Ordinal).Should().Equal(0, 1);
        rows.Should().OnlyContain(x =>
            x.PlayId == playId &&
            x.CompetitionId == competitionId &&
            x.ContestId == ContestId &&
            x.AnalysisRunId == null &&
            x.TemplateVersion == FactPhraser.TemplateVersion &&
            x.CreatedUtc == FixedTestNow &&
            x.CreatedBy == CorrelationId);
        rows[0].Facts.Should().Contain("\"kind\":\"Interception\"").And.Contain("\"countedThroughSequence\":512");

        outcome.Dropped.Should().Contain(d => d.Reason == "No season document.");
        VerifyLogged("Fact dropped", Times.AtLeastOnce(), "No season document.");
        VerifyLogged("Play analysis timing", Times.Once(), "Persisted=2");
    }

    [Fact]
    public async Task Analyse_SamePlayTwice_DoesNotDuplicateRows()
    {
        var (_, playId) = await SeedPlay(liveAnalysisEnabled: false);

        await Live(playId);
        var second = await Live(playId);

        second.Should().Be(PlayAnalysisOutcome.Skipped);
        FootballDataContext.PlayAnalyses.Count(x => x.PlayId == playId).Should().Be(2);
        VerifyLogged("already has a fact set for this run", Times.Once());
    }

    [Fact]
    public async Task Analyse_LabRun_WritesItsOwnSet_BesideTheLiveOne()
    {
        var (_, playId) = await SeedPlay(liveAnalysisEnabled: false);
        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();

        await Live(playId);
        await _sut.Analyse(playId, ContestId, CorrelationId, runA, SeasonDocumentRule.BeforeKickoff);
        await _sut.Analyse(playId, ContestId, CorrelationId, runB, SeasonDocumentRule.BeforeKickoff);

        FootballDataContext.PlayAnalyses.Count(x => x.PlayId == playId && x.AnalysisRunId == null).Should().Be(2);
        FootballDataContext.PlayAnalyses.Count(x => x.PlayId == playId && x.AnalysisRunId == runA).Should().Be(2);
        FootballDataContext.PlayAnalyses.Count(x => x.PlayId == playId && x.AnalysisRunId == runB).Should().Be(2);
    }

    [Fact]
    public async Task Analyse_LabRun_ExplainsMissingPreKickoffDocuments()
    {
        var (_, playId) = await SeedPlay(liveAnalysisEnabled: false);

        var outcome = await _sut.Analyse(playId, ContestId, CorrelationId, Guid.NewGuid(), SeasonDocumentRule.BeforeKickoff);

        outcome.Dropped.Should().Contain(d => d.Reason == "No season document written before kickoff.");
    }

    [Fact]
    public async Task Analyse_UnknownPlay_IsNotFound()
    {
        (await Live(Guid.NewGuid())).Should().Be(PlayAnalysisOutcome.NotFound);
    }

    private Task<PlayAnalysisOutcome> Live(Guid playId) =>
        _sut.Analyse(playId, ContestId, CorrelationId, analysisRunId: null, SeasonDocumentRule.Newest);

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

    private void VerifyLogged(string fragment, Times times, string? alsoContains = null)
    {
        Mock.Get(Mocker.Get<ILogger<PlayAnalyser>>()).Verify(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains(fragment) &&
                    (alsoContains == null || v.ToString()!.Contains(alsoContains))),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
