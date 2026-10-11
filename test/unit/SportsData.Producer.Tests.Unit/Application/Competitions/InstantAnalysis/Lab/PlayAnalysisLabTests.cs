#nullable enable
using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Producer.Application.Competitions.InstantAnalysis;
using SportsData.Producer.Application.Competitions.InstantAnalysis.Lab;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions.InstantAnalysis.Lab;

public class PlayAnalysisLabTests : ProducerTestBase<RunPlayAnalysisLabCommandHandler>
{
    private static readonly DateTime FixedTestNow = new(2026, 10, 10, 18, 0, 0, DateTimeKind.Utc);

    public PlayAnalysisLabTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedTestNow);
    }

    [Fact]
    public async Task Run_AnalysesEveryPlayInSequenceOrder_AsALabRunWithPreKickoffDocuments()
    {
        var (contestId, playIds) = await SeedGame("10", "2", "33");
        var analysed = new List<Guid>();
        Guid? runId = null;

        Mocker.GetMock<IPlayAnalyser>()
            .Setup(x => x.Analyse(It.IsAny<Guid>(), contestId, It.IsAny<Guid>(), It.IsAny<Guid?>(), SeasonDocumentRule.BeforeKickoff))
            .Callback<Guid, Guid, Guid, Guid?, SeasonDocumentRule>((playId, _, _, run, _) =>
            {
                analysed.Add(playId);
                runId = run;
            })
            .ReturnsAsync(new PlayAnalysisOutcome(PlayAnalysisStatus.Analysed, 1, 1,
                [new DroppedFact(FactTypes.SeasonCount, PlayKind.Sack, Guid.NewGuid(), "No season document written before kickoff.")]));

        var result = await Mocker.CreateInstance<RunPlayAnalysisLabCommandHandler>().ExecuteAsync(contestId, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        analysed.Should().Equal(playIds["2"], playIds["10"], playIds["33"]);
        runId.Should().Be(result.Value.RunId);

        var run = FootballDataContext.PlayAnalysisRuns.Single();
        run.Id.Should().Be(result.Value.RunId);
        run.PlayCount.Should().Be(3);
        run.FactCount.Should().Be(3);
        run.DroppedCount.Should().Be(3);
        run.CompletedUtc.Should().Be(FixedTestNow);
        run.DroppedFacts.Should().Contain("No season document written before kickoff.").And.Contain(playIds["2"].ToString());
    }

    [Fact]
    public async Task Run_UnknownContest_IsNotFound()
    {
        var result = await Mocker.CreateInstance<RunPlayAnalysisLabCommandHandler>().ExecuteAsync(Guid.NewGuid(), Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Run_GameNotFinalized_IsRejectedWithoutARun()
    {
        var (contestId, _) = await SeedGame(finalized: false, "1", "2");

        var result = await Mocker.CreateInstance<RunPlayAnalysisLabCommandHandler>().ExecuteAsync(contestId, Guid.NewGuid());

        result.Status.Should().Be(ResultStatus.Validation);
        FootballDataContext.PlayAnalysisRuns.Should().BeEmpty();
        Mocker.GetMock<IPlayAnalyser>().VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetRun_ListsEveryPlay_WithFactsRatingsAndDrops()
    {
        var (contestId, playIds) = await SeedGame("1", "2");
        var competitionId = FootballDataContext.Competitions.Single().Id;
        var runId = Guid.NewGuid();
        var factId = Guid.NewGuid();

        FootballDataContext.PlayAnalysisRuns.Add(new PlayAnalysisRun
        {
            Id = runId,
            ContestId = contestId,
            CompetitionId = competitionId,
            PlayCount = 2,
            FactCount = 1,
            DroppedCount = 1,
            DroppedFacts = $"[{{\"playId\":\"{playIds["2"]}\",\"factType\":\"SeasonCount\",\"kind\":\"Sack\",\"athleteSeasonId\":null,\"reason\":\"No season document written before kickoff.\"}}]",
            CreatedUtc = FixedTestNow
        });
        FootballDataContext.PlayAnalyses.Add(new PlayAnalysis
        {
            Id = factId,
            ContestId = contestId,
            CompetitionId = competitionId,
            PlayId = playIds["2"],
            AnalysisRunId = runId,
            FactType = FactTypes.GameCount,
            Text = "T. Cooley's first sack of the game.",
            CreatedUtc = FixedTestNow
        });
        FootballDataContext.PlayAnalysisRatings.Add(new PlayAnalysisRating { PlayAnalysisId = factId, Stars = 3, CreatedUtc = FixedTestNow });
        await FootballDataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<GetPlayAnalysisRunQueryHandler>().ExecuteAsync(runId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Plays.Select(p => p.SequenceNumber).Should().Equal("1", "2");
        result.Value.Plays[0].Facts.Should().BeEmpty();
        result.Value.Plays[1].Facts.Should().ContainSingle(f => f.Id == factId && f.Stars == 3);
        result.Value.Plays[1].Dropped.Should().ContainSingle(d => d.Reason == "No season document written before kickoff.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public async Task Rate_OutOfRange_IsRejected(int stars)
    {
        var result = await Mocker.CreateInstance<RatePlayAnalysisCommandHandler>().ExecuteAsync(Guid.NewGuid(), stars, Guid.NewGuid());

        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task Rate_Twice_UpdatesTheOneRating()
    {
        var (contestId, playIds) = await SeedGame("1");
        var factId = Guid.NewGuid();
        FootballDataContext.PlayAnalyses.Add(new PlayAnalysis
        {
            Id = factId,
            ContestId = contestId,
            CompetitionId = FootballDataContext.Competitions.Single().Id,
            PlayId = playIds["1"],
            AnalysisRunId = Guid.NewGuid(),
            FactType = FactTypes.GameCount,
            Text = "x",
            CreatedUtc = FixedTestNow
        });
        await FootballDataContext.SaveChangesAsync();
        var sut = Mocker.CreateInstance<RatePlayAnalysisCommandHandler>();

        await sut.ExecuteAsync(factId, 1, Guid.NewGuid());
        var second = await sut.ExecuteAsync(factId, 4, Guid.NewGuid());

        second.IsSuccess.Should().BeTrue();
        FootballDataContext.PlayAnalysisRatings.Should().ContainSingle(r => r.PlayAnalysisId == factId && r.Stars == 4);
    }

    [Fact]
    public async Task Rate_LiveFact_IsNotFound()
    {
        var (contestId, playIds) = await SeedGame("1");
        var factId = Guid.NewGuid();
        FootballDataContext.PlayAnalyses.Add(new PlayAnalysis
        {
            Id = factId,
            ContestId = contestId,
            CompetitionId = FootballDataContext.Competitions.Single().Id,
            PlayId = playIds["1"],
            AnalysisRunId = null,
            FactType = FactTypes.GameCount,
            Text = "x",
            CreatedUtc = FixedTestNow
        });
        await FootballDataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<RatePlayAnalysisCommandHandler>().ExecuteAsync(factId, 3, Guid.NewGuid());

        result.Status.Should().Be(ResultStatus.NotFound);
        FootballDataContext.PlayAnalysisRatings.Should().BeEmpty();
    }

    [Fact]
    public async Task Rate_UnknownFact_IsNotFound()
    {
        var result = await Mocker.CreateInstance<RatePlayAnalysisCommandHandler>().ExecuteAsync(Guid.NewGuid(), 2, Guid.NewGuid());

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    private Task<(Guid ContestId, Dictionary<string, Guid> PlayIds)> SeedGame(params string[] sequences) =>
        SeedGame(finalized: true, sequences);

    private async Task<(Guid ContestId, Dictionary<string, Guid> PlayIds)> SeedGame(bool finalized, params string[] sequences)
    {
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();

        FootballDataContext.Contests.Add(new FootballContest
        {
            Id = contestId,
            Name = "Test",
            ShortName = "TST",
            SeasonYear = 2026,
            Sport = Sport.FootballNcaa,
            StartDateUtc = FixedTestNow.AddHours(-4),
            FinalizedUtc = finalized ? FixedTestNow.AddHours(-1) : null,
            HomeTeamFranchiseSeasonId = Guid.NewGuid(),
            AwayTeamFranchiseSeasonId = Guid.NewGuid(),
            CreatedUtc = FixedTestNow
        });

        FootballDataContext.Competitions.Add(new FootballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Date = FixedTestNow,
            CreatedUtc = FixedTestNow
        });

        var playIds = new Dictionary<string, Guid>();
        foreach (var sequence in sequences)
        {
            var playId = Guid.NewGuid();
            playIds[sequence] = playId;
            FootballDataContext.CompetitionPlays.Add(new FootballCompetitionPlay
            {
                Id = playId,
                CompetitionId = competitionId,
                EspnId = sequence,
                SequenceNumber = sequence,
                Type = PlayType.Rush,
                TypeId = "5",
                Text = $"play {sequence}",
                CreatedUtc = FixedTestNow
            });
        }

        await FootballDataContext.SaveChangesAsync();
        return (contestId, playIds);
    }
}
