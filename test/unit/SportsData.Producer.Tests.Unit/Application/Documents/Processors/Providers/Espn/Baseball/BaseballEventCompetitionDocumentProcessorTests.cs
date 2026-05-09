#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Baseball;

/// <summary>
/// Tests for <see cref="BaseballEventCompetitionDocumentProcessor{TDataContext}"/>.
///
/// Today the only sport-specific behavior on this processor is the
/// inline series snapshot (current-series and season-series state ships
/// on the EventCompetition payload), so the suite is currently
/// dominated by series scenarios. Snapshot columns lock on first
/// non-null write — subsequent reprocessing does not overwrite, so
/// historical matchup pages render at-game-start state instead of
/// current rolled-up state. EspnSeriesId is the grouping key (not
/// historical state) and refreshes every pass.
///
/// See docs/series-snapshot-redesign.md.
/// </summary>
[Collection("Sequential")]
public class BaseballEventCompetitionDocumentProcessorTests
    : ProducerTestBase<BaseballEventCompetitionDocumentProcessor<BaseballDataContext>>
{
    private readonly BaseballDataContext _baseballDataContext;
    private static readonly DateTime FixedNow = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);

    public BaseballEventCompetitionDocumentProcessorTests()
    {
        _baseballDataContext = new BaseballDataContext(
            new DbContextOptionsBuilder<BaseballDataContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()[..8])
                .Options);
        Mocker.Use(_baseballDataContext);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use(dateTimeProvider.Object);
    }

    [Fact]
    public async Task DTO_Deserializes_Series_Fields_From_Fixture()
    {
        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetition.json");

        var dto = json.FromJson<EspnBaseballEventCompetitionDto>();

        dto.Should().NotBeNull();
        dto!.SeriesId.Should().Be("600055560");
        dto.Series.Should().HaveCount(3);
        dto.Series![0].Type.Should().Be("current");
        dto.Series[0].Summary.Should().Be("Series tied 1-1");
        dto.Series[0].TotalCompetitions.Should().Be(3);
        dto.Series[0].Competitors.Should().HaveCount(2);
        dto.Series[1].Type.Should().Be("preseason");
        dto.Series[2].Type.Should().Be("season");
        dto.Series[2].TotalCompetitions.Should().Be(13);
    }

    [Fact]
    public async Task WhenSeriesPayloadProcessed_SnapshotColumnsAreSet()
    {
        var (competitionId, _, cmd) = await SetupAndBuildCommand();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionDocumentProcessor<BaseballDataContext>>();

        await sut.ProcessAsync(cmd);

        var refreshed = await _baseballDataContext.Competitions
            .OfType<BaseballCompetition>()
            .FirstAsync(x => x.Id == competitionId);

        refreshed.EspnSeriesId.Should().Be("600055560");

        // Current series snapshot — fixture's Team 5 is home, Team 7 is away
        // (per the EventCompetition.json parent competitors).
        refreshed.CurrentSeriesSummary.Should().Be("Series tied 1-1");
        refreshed.CurrentSeriesTotalCompetitions.Should().Be(3);
        refreshed.CurrentSeriesCompleted.Should().BeFalse();
        refreshed.CurrentSeriesHomeWins.Should().Be(1);
        refreshed.CurrentSeriesAwayWins.Should().Be(1);
        refreshed.CurrentSeriesHomeTies.Should().Be(0);
        refreshed.CurrentSeriesAwayTies.Should().Be(0);

        // Season series snapshot
        refreshed.SeasonSeriesSummary.Should().NotBeNullOrEmpty();
        refreshed.SeasonSeriesTotalCompetitions.Should().Be(13);
        refreshed.SeasonSeriesCompleted.Should().BeFalse();
        refreshed.SeasonSeriesHomeWins.Should().NotBeNull();
        refreshed.SeasonSeriesAwayWins.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenReprocessed_SnapshotIsLocked_AndDoesNotOverwrite()
    {
        // arrange — process once with the real fixture.
        var (competitionId, _, cmd) = await SetupAndBuildCommand();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionDocumentProcessor<BaseballDataContext>>();

        await sut.ProcessAsync(cmd);

        var firstSnapshot = await _baseballDataContext.Competitions
            .OfType<BaseballCompetition>()
            .AsNoTracking()
            .FirstAsync(x => x.Id == competitionId);

        // Mutate the fixture: bump current-series wins/summary as if the
        // series had advanced, then reprocess.
        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetition.json");
        var dto = json.FromJson<EspnBaseballEventCompetitionDto>()!;
        var current = dto.Series!.First(s => s.Type == "current");
        current.Summary = "Mutated lead 5-0";
        foreach (var c in current.Competitors!)
        {
            c.Wins = 99;
            c.Ties = 99;
        }

        var mutatedJson = dto.ToJson();
        var mutatedCmd = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, cmd.ParentId)
            .With(x => x.SeasonYear, cmd.SeasonYear)
            .With(x => x.SourceDataProvider, cmd.SourceDataProvider)
            .With(x => x.Sport, cmd.Sport)
            .With(x => x.DocumentType, cmd.DocumentType)
            .With(x => x.Document, mutatedJson)
            .With(x => x.UrlHash, cmd.UrlHash)
            .With(x => x.IncludeLinkedDocumentTypes, cmd.IncludeLinkedDocumentTypes)
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(mutatedCmd);

        // assert — snapshot did NOT change, despite the mutated payload.
        var refreshed = await _baseballDataContext.Competitions
            .OfType<BaseballCompetition>()
            .AsNoTracking()
            .FirstAsync(x => x.Id == competitionId);

        refreshed.CurrentSeriesSummary.Should().Be(firstSnapshot.CurrentSeriesSummary);
        refreshed.CurrentSeriesHomeWins.Should().Be(firstSnapshot.CurrentSeriesHomeWins);
        refreshed.CurrentSeriesAwayWins.Should().Be(firstSnapshot.CurrentSeriesAwayWins);
        refreshed.CurrentSeriesSummary.Should().NotBe("Mutated lead 5-0");
    }

    [Fact]
    public async Task WhenSeriesArrayMissing_DoesNotClearLockedSnapshot()
    {
        // arrange — seed a competition with a snapshot already in place,
        // then reprocess with a payload whose Series array is empty.
        var (competitionId, _, cmd) = await SetupAndBuildCommand();
        var sut = Mocker.CreateInstance<BaseballEventCompetitionDocumentProcessor<BaseballDataContext>>();
        await sut.ProcessAsync(cmd);

        var locked = await _baseballDataContext.Competitions
            .OfType<BaseballCompetition>()
            .AsNoTracking()
            .FirstAsync(x => x.Id == competitionId);
        locked.CurrentSeriesSummary.Should().NotBeNullOrEmpty();

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetition.json");
        var dto = json.FromJson<EspnBaseballEventCompetitionDto>()!;
        dto.Series = new List<EspnBaseballSeriesDto>();
        dto.SeriesId = null;
        var emptyJson = dto.ToJson();

        var emptyCmd = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, cmd.ParentId)
            .With(x => x.SeasonYear, cmd.SeasonYear)
            .With(x => x.SourceDataProvider, cmd.SourceDataProvider)
            .With(x => x.Sport, cmd.Sport)
            .With(x => x.DocumentType, cmd.DocumentType)
            .With(x => x.Document, emptyJson)
            .With(x => x.UrlHash, cmd.UrlHash)
            .With(x => x.IncludeLinkedDocumentTypes, cmd.IncludeLinkedDocumentTypes)
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(emptyCmd);

        // assert — snapshot persists; empty payload doesn't blow it away.
        var refreshed = await _baseballDataContext.Competitions
            .OfType<BaseballCompetition>()
            .AsNoTracking()
            .FirstAsync(x => x.Id == competitionId);
        refreshed.CurrentSeriesSummary.Should().Be(locked.CurrentSeriesSummary);
        refreshed.SeasonSeriesSummary.Should().Be(locked.SeasonSeriesSummary);
    }

    private async Task<(Guid CompetitionId, Guid ContestId, ProcessDocumentCommand Cmd)> SetupAndBuildCommand()
    {
        var idGen = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(idGen);

        var competitionRef = new Uri(
            "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844?lang=en&region=us");
        var competitionUrlHash = HashProvider.GenerateHashFromUri(competitionRef);
        var competitionId = idGen.Generate(competitionRef).CanonicalId;

        var contestId = await SeedContestAndCompetition(competitionId);

        _baseballDataContext.Set<CompetitionExternalId>().Add(new CompetitionExternalId
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            Provider = SourceDataProvider.Espn,
            Value = "401814844",
            SourceUrl = competitionRef.AbsoluteUri,
            SourceUrlHash = competitionUrlHash,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await _baseballDataContext.SaveChangesAsync();

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetition.json");

        var cmd = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, contestId.ToString())
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.DocumentType, DocumentType.EventCompetition)
            .With(x => x.Document, json)
            .With(x => x.UrlHash, competitionUrlHash)
            .With(x => x.IncludeLinkedDocumentTypes, (IReadOnlyCollection<DocumentType>?)null)
            .OmitAutoProperties()
            .Create();

        return (competitionId, contestId, cmd);
    }

    private async Task<Guid> SeedContestAndCompetition(Guid competitionId)
    {
        var contestId = Guid.NewGuid();

        var contest = new BaseballContest
        {
            Id = contestId,
            Name = "Test Contest",
            ShortName = "Test",
            SeasonYear = 2026,
            Sport = Sport.BaseballMlb,
            StartDateUtc = FixedNow,
            HomeTeamFranchiseSeasonId = Guid.NewGuid(),
            AwayTeamFranchiseSeasonId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };

        var competition = new BaseballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Date = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };

        await _baseballDataContext.Contests.AddAsync(contest);
        await _baseballDataContext.Competitions.AddAsync(competition);
        await _baseballDataContext.SaveChangesAsync();

        return contestId;
    }
}
