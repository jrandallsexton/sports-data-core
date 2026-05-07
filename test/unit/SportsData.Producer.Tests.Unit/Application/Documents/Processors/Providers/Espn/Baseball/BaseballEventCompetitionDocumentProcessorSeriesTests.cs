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
/// Tests for the inline series ingestion in
/// <see cref="BaseballEventCompetitionDocumentProcessor{TDataContext}.ProcessSportSpecificCompetitionData"/>.
///
/// ESPN's MLB EventCompetition payload carries series state inline (no
/// separate $ref). The processor extracts it and persists Series +
/// SeasonSeries (with their respective Competitor join rows), and links
/// the competition via CurrentSeriesId / SeasonSeriesId. Unknown series
/// types (e.g. "preseason" in the EventCompetition.json fixture) are
/// logged and skipped.
///
/// See docs/mlb-series-ingestion-plan.md.
/// </summary>
[Collection("Sequential")]
public class BaseballEventCompetitionDocumentProcessorSeriesTests
    : ProducerTestBase<BaseballEventCompetitionDocumentProcessor<BaseballDataContext>>
{
    private readonly BaseballDataContext _baseballDataContext;

    private static readonly Uri Team5Ref = new(
        "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/seasons/2026/teams/5?lang=en&region=us");
    private static readonly Uri Team7Ref = new(
        "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/seasons/2026/teams/7?lang=en&region=us");
    private static readonly DateTime FixedNow = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);

    public BaseballEventCompetitionDocumentProcessorSeriesTests()
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
    public async Task WhenSeriesPayloadProcessed_PersistsCurrent_SeasonSeries_AndLinksCompetition()
    {
        // arrange
        var idGen = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(idGen);

        var competitionRef = new Uri(
            "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844?lang=en&region=us");
        var competitionUrlHash = HashProvider.GenerateHashFromUri(competitionRef);
        // Seed the competition with the canonical Id derived from the URL,
        // matching what AsBaseballEntity produces during the update path.
        var competitionId = idGen.Generate(competitionRef).CanonicalId;

        var contestId = await SeedContestAndCompetition(competitionId);
        var (fs5Id, fs7Id) = await SeedFranchiseSeasons();

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetition.json");

        var sut = Mocker.CreateInstance<BaseballEventCompetitionDocumentProcessor<BaseballDataContext>>();

        // Seed the existing competition's external id so the processor takes the update path.
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

        // act
        await sut.ProcessAsync(cmd);

        // assert — Series row created + linked
        var seriesRows = await _baseballDataContext.Series
            .Include(x => x.Competitors)
            .ToListAsync();
        seriesRows.Should().ContainSingle();
        var series = seriesRows[0];
        series.EspnSeriesId.Should().Be("600055560");
        series.Summary.Should().Be("Series tied 1-1");
        series.TotalCompetitions.Should().Be(3);
        series.Competitors.Should().HaveCount(2);
        series.Competitors.Should().Contain(c => c.FranchiseSeasonId == fs5Id && c.Wins == 1);
        series.Competitors.Should().Contain(c => c.FranchiseSeasonId == fs7Id && c.Wins == 1);

        // SeasonSeries row created with sorted-pair identity
        var seasonRows = await _baseballDataContext.SeasonSeries
            .Include(x => x.Competitors)
            .ToListAsync();
        seasonRows.Should().ContainSingle();
        var seasonSeries = seasonRows[0];
        seasonSeries.SeasonYear.Should().Be(2026);
        seasonSeries.TotalCompetitions.Should().Be(13);
        var sortedLow = fs5Id.CompareTo(fs7Id) < 0 ? fs5Id : fs7Id;
        var sortedHigh = fs5Id.CompareTo(fs7Id) < 0 ? fs7Id : fs5Id;
        seasonSeries.FranchiseSeasonALowId.Should().Be(sortedLow);
        seasonSeries.FranchiseSeasonBHighId.Should().Be(sortedHigh);
        seasonSeries.Competitors.Should().HaveCount(2);

        // Competition FK columns set
        var refreshed = await _baseballDataContext.Competitions
            .OfType<BaseballCompetition>()
            .FirstAsync(x => x.Id == competitionId);
        refreshed.CurrentSeriesId.Should().Be(series.Id);
        refreshed.SeasonSeriesId.Should().Be(seasonSeries.Id);

        // "preseason" entry hits the unrecognized branch and is skipped — no third row.
        seriesRows.Should().HaveCount(1);
        seasonRows.Should().HaveCount(1);
    }

    [Fact]
    public async Task WhenReprocessed_DoesNotDuplicateRows_AndUpdatesCounters()
    {
        // arrange — process once, then process again with the same payload.
        var idGen = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(idGen);

        var competitionRef = new Uri(
            "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844?lang=en&region=us");
        var competitionUrlHash = HashProvider.GenerateHashFromUri(competitionRef);
        var competitionId = idGen.Generate(competitionRef).CanonicalId;

        var contestId = await SeedContestAndCompetition(competitionId);
        await SeedFranchiseSeasons();

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetition.json");

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

        var sut = Mocker.CreateInstance<BaseballEventCompetitionDocumentProcessor<BaseballDataContext>>();

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

        // act — process twice
        await sut.ProcessAsync(cmd);
        await sut.ProcessAsync(cmd);

        // assert — exactly one row of each, counters intact
        (await _baseballDataContext.Series.CountAsync()).Should().Be(1);
        (await _baseballDataContext.SeriesCompetitors.CountAsync()).Should().Be(2);
        (await _baseballDataContext.SeasonSeries.CountAsync()).Should().Be(1);
        (await _baseballDataContext.SeasonSeriesCompetitors.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task WhenSeriesArrayMissing_ClearsStaleSeriesFKs()
    {
        // arrange — seed a competition that already has both FKs populated,
        // then re-process with a payload whose Series array is empty.
        var idGen = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(idGen);

        var competitionRef = new Uri(
            "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844?lang=en&region=us");
        var competitionUrlHash = HashProvider.GenerateHashFromUri(competitionRef);
        var competitionId = idGen.Generate(competitionRef).CanonicalId;

        var contestId = await SeedContestAndCompetition(competitionId);
        await SeedFranchiseSeasons();

        // Seed pre-existing FK state on the competition.
        var staleSeriesId = Guid.NewGuid();
        var staleSeasonSeriesId = Guid.NewGuid();
        var seeded = await _baseballDataContext.Competitions
            .OfType<BaseballCompetition>()
            .FirstAsync(x => x.Id == competitionId);
        seeded.CurrentSeriesId = staleSeriesId;
        seeded.SeasonSeriesId = staleSeasonSeriesId;
        await _baseballDataContext.SaveChangesAsync();

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

        // Load the real fixture and rewrite "series" to an empty array.
        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetition.json");
        var dto = json.FromJson<EspnBaseballEventCompetitionDto>()!;
        dto.Series = new List<EspnBaseballSeriesDto>();
        dto.SeriesId = null;
        var emptySeriesJson = dto.ToJson();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionDocumentProcessor<BaseballDataContext>>();

        var cmd = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, contestId.ToString())
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.DocumentType, DocumentType.EventCompetition)
            .With(x => x.Document, emptySeriesJson)
            .With(x => x.UrlHash, competitionUrlHash)
            .With(x => x.IncludeLinkedDocumentTypes, (IReadOnlyCollection<DocumentType>?)null)
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(cmd);

        // assert — both FKs cleared
        var refreshed = await _baseballDataContext.Competitions
            .OfType<BaseballCompetition>()
            .FirstAsync(x => x.Id == competitionId);
        refreshed.CurrentSeriesId.Should().BeNull();
        refreshed.SeasonSeriesId.Should().BeNull();
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

    private async Task<(Guid fs5Id, Guid fs7Id)> SeedFranchiseSeasons()
    {
        var fs5Id = Guid.NewGuid();
        var fs7Id = Guid.NewGuid();

        var fs5 = new FranchiseSeason
        {
            Id = fs5Id,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2026,
            DisplayName = "Team 5",
            DisplayNameShort = "T5",
            Name = "Team 5",
            Slug = "team-5",
            Location = "Test City",
            Abbreviation = "T5",
            ColorCodeHex = "#000000",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds =
            [
                new FranchiseSeasonExternalId
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = fs5Id,
                    Provider = SourceDataProvider.Espn,
                    Value = "5",
                    SourceUrl = Team5Ref.AbsoluteUri,
                    SourceUrlHash = HashProvider.GenerateHashFromUri(Team5Ref),
                    CreatedUtc = FixedNow,
                    CreatedBy = Guid.NewGuid()
                }
            ]
        };

        var fs7 = new FranchiseSeason
        {
            Id = fs7Id,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2026,
            DisplayName = "Team 7",
            DisplayNameShort = "T7",
            Name = "Team 7",
            Slug = "team-7",
            Location = "Test City",
            Abbreviation = "T7",
            ColorCodeHex = "#000000",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds =
            [
                new FranchiseSeasonExternalId
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = fs7Id,
                    Provider = SourceDataProvider.Espn,
                    Value = "7",
                    SourceUrl = Team7Ref.AbsoluteUri,
                    SourceUrlHash = HashProvider.GenerateHashFromUri(Team7Ref),
                    CreatedUtc = FixedNow,
                    CreatedBy = Guid.NewGuid()
                }
            ]
        };

        await _baseballDataContext.FranchiseSeasons.AddRangeAsync(fs5, fs7);
        await _baseballDataContext.SaveChangesAsync();

        return (fs5Id, fs7Id);
    }
}
