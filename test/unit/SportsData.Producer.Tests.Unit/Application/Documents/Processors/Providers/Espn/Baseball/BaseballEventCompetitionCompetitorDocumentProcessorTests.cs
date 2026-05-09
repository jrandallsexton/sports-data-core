#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Baseball;

/// <summary>
/// Tests for <see cref="BaseballEventCompetitionCompetitorDocumentProcessor{TDataContext}"/>.
///
/// Today the only sport-specific behavior on this processor is the MLB
/// Probables ingestion path, so the suite is currently dominated by
/// Probables scenarios. Each Probable carries a hard FK to AthleteSeason —
/// when the athlete isn't in the DB yet, the processor publishes a
/// dependency request and throws ExternalDocumentNotSourcedException so
/// Hangfire retries. No partial rows; an empty Probable is worthless on
/// the matchup card.
///
/// See docs/competition-competitor-probables.md.
/// </summary>
[Collection("Sequential")]
public class BaseballEventCompetitionCompetitorDocumentProcessorTests
    : ProducerTestBase<BaseballEventCompetitionCompetitorDocumentProcessor<BaseballDataContext>>
{
    private readonly BaseballDataContext _baseballDataContext;
    private static readonly DateTime FixedNow = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);

    // Refs that match the Data/EspnBaseballMlb/EventCompetitionCompetitor.json fixture.
    private static readonly Uri CompetitorRef = new(
        "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401815254/competitions/401815254/competitors/1?lang=en&region=us");
    private static readonly Uri TeamRef = new(
        "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/seasons/2026/teams/1?lang=en&region=us");
    private static readonly Uri AthleteSeasonRef = new(
        "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/seasons/2026/athletes/4311625?lang=en&region=us");

    public BaseballEventCompetitionCompetitorDocumentProcessorTests()
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
    public async Task DTO_Deserializes_Probables_From_Fixture()
    {
        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionCompetitor.json");

        var dto = json.FromJson<EspnBaseballEventCompetitionCompetitorDto>();

        dto.Should().NotBeNull();
        dto!.Probables.Should().NotBeNull().And.HaveCount(1);
        var probable = dto.Probables![0];
        probable.Name.Should().Be("probableStartingPitcher");
        probable.DisplayName.Should().Be("Probable Starting Pitcher");
        probable.ShortDisplayName.Should().Be("Starter");
        probable.Abbreviation.Should().Be("SP");
        probable.PlayerId.Should().Be(4311625);
        probable.Athlete.Should().NotBeNull();
        probable.Athlete!.Ref.Should().Be(AthleteSeasonRef);
    }

    [Fact]
    public async Task WhenAthleteSeasonExists_ProbableIsPersisted()
    {
        var (cmd, athleteSeasonId) = await SetupHappyPath();
        var sut = Mocker.CreateInstance<BaseballEventCompetitionCompetitorDocumentProcessor<BaseballDataContext>>();

        await sut.ProcessAsync(cmd);

        var probables = await _baseballDataContext.CompetitionCompetitorProbables
            .AsNoTracking()
            .ToListAsync();

        probables.Should().HaveCount(1);
        var probable = probables.Single();
        probable.AthleteSeasonId.Should().Be(athleteSeasonId);
        probable.EspnPlayerId.Should().Be(4311625);
        probable.Name.Should().Be("probableStartingPitcher");
        probable.DisplayName.Should().Be("Probable Starting Pitcher");
        probable.ShortDisplayName.Should().Be("Starter");
        probable.Abbreviation.Should().Be("SP");
        probable.CompetitionCompetitorId.Should().NotBeEmpty();
        // Pin audit fields to the test harness's known values — these
        // assertions fail loudly if someone reverts to DateTime.UtcNow
        // or breaks correlation-id propagation.
        probable.CreatedUtc.Should().Be(FixedNow);
        probable.CreatedBy.Should().Be(cmd.CorrelationId);
    }

    [Fact]
    public async Task WhenExistingProbable_AndCascadeFilterExcludesStats_DoesNotRequestStatistics()
    {
        // arrange — first pass creates the row (new path always fans out).
        var (cmd, _) = await SetupHappyPath();
        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<BaseballEventCompetitionCompetitorDocumentProcessor<BaseballDataContext>>();

        await sut.ProcessAsync(cmd);
        bus.Invocations.Clear();

        // Second pass with a narrow IncludeLinkedDocumentTypes that does
        // NOT include AthleteSeasonStatistics — mirrors a Refresh Contest
        // narrowing the cascade. Existing-row path must respect the filter.
        var narrowedCmd = BuildCommand(
            cmd.Document,
            cmd.ParentId!,
            cmd.UrlHash,
            includeLinkedDocumentTypes: new[] { DocumentType.EventCompetitionCompetitorScore });

        // act
        await sut.ProcessAsync(narrowedCmd);

        // assert — the stats fan-out is suppressed on the update path.
        bus.Verify(x => x.Publish(
                It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.AthleteSeasonStatistics),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenProbableIsPersisted_RequestsAthleteSeasonStatistics()
    {
        // arrange — happy-path setup wires AthleteSeason; the probable's
        // statistics.$ref should fan out as a child doc request so the
        // matchup card has season stats downstream.
        var (cmd, athleteSeasonId) = await SetupHappyPath();
        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<BaseballEventCompetitionCompetitorDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — child request fired with parent = AthleteSeasonId.
        bus.Verify(x => x.Publish(
                It.Is<DocumentRequested>(d =>
                    d.DocumentType == DocumentType.AthleteSeasonStatistics &&
                    d.ParentId == athleteSeasonId.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WhenAthleteSeasonMissing_PublishesDependencyAndRequestsRetry()
    {
        // arrange — competition + franchise season seeded, but NOT AthleteSeason.
        var (cmd, _) = await SetupHappyPath(seedAthleteSeason: false);
        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<BaseballEventCompetitionCompetitorDocumentProcessor<BaseballDataContext>>();

        // The processor throws ExternalDocumentNotSourcedException, which
        // DocumentProcessorBase.ProcessAsync catches and converts into a
        // DocumentCreated republish for Hangfire retry — see the base
        // catch block. So we observe the contract via published events,
        // not a bubbled exception.
        await sut.ProcessAsync(cmd);

        // Dependency request for the missing AthleteSeason was published.
        bus.Verify(x => x.Publish(
                It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.AthleteSeason),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Retry-republish: a DocumentCreated was emitted with the retry
        // headers (the base catch path).
        bus.Verify(x => x.Publish(
                It.IsAny<DocumentCreated>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // The stats child fan-out is gated on AthleteSeason existing —
        // the throw happens before the publish, so nothing fires.
        bus.Verify(x => x.Publish(
                It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.AthleteSeasonStatistics),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // No probable row should have been persisted (the throw happened
        // before any AddAsync on the probables DbSet).
        var probables = await _baseballDataContext.CompetitionCompetitorProbables
            .AsNoTracking()
            .ToListAsync();
        probables.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenReprocessed_RowIsIdempotent_AndAuditUpdates()
    {
        // arrange — first pass creates the probable row.
        var (cmd, athleteSeasonId) = await SetupHappyPath();
        var sut = Mocker.CreateInstance<BaseballEventCompetitionCompetitorDocumentProcessor<BaseballDataContext>>();

        await sut.ProcessAsync(cmd);

        var first = await _baseballDataContext.CompetitionCompetitorProbables
            .AsNoTracking()
            .SingleAsync();

        // act — reprocess the same document. The deterministic Id from
        // (competitorId, role-name) means the upsert hits the same row.
        await sut.ProcessAsync(cmd);

        // assert — still one row, same Id; audit fields reflect the update path.
        var probables = await _baseballDataContext.CompetitionCompetitorProbables
            .AsNoTracking()
            .ToListAsync();
        probables.Should().HaveCount(1);

        var second = probables.Single();
        second.Id.Should().Be(first.Id);
        second.AthleteSeasonId.Should().Be(athleteSeasonId);
        second.EspnPlayerId.Should().Be(first.EspnPlayerId);
        // Tighter than NotBeNull — pinned to harness values so the test
        // fails if DateTime.UtcNow or a stray CreatedBy slips back in.
        second.ModifiedUtc.Should().Be(FixedNow);
        second.ModifiedBy.Should().Be(cmd.CorrelationId);
    }

    [Fact]
    public async Task WhenProbableHasMissingName_IsSkippedWithoutThrowing()
    {
        // arrange — happy-path setup, then mutate the fixture to drop Name.
        var (cmd, _) = await SetupHappyPath();
        var dto = cmd.Document.FromJson<EspnBaseballEventCompetitionCompetitorDto>()!;
        dto.Probables![0].Name = null;
        var mutatedCmd = BuildCommand(dto.ToJson(), cmd.ParentId!, cmd.UrlHash);

        var sut = Mocker.CreateInstance<BaseballEventCompetitionCompetitorDocumentProcessor<BaseballDataContext>>();

        // act — must NOT throw; missing-Name is a soft skip.
        await sut.ProcessAsync(mutatedCmd);

        // assert — competitor saved, but no probable row.
        var probables = await _baseballDataContext.CompetitionCompetitorProbables
            .AsNoTracking()
            .ToListAsync();
        probables.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenProbableHasNoAthleteRef_IsSkippedWithoutThrowing()
    {
        // arrange — happy-path setup, then mutate the fixture to drop the athlete ref.
        var (cmd, _) = await SetupHappyPath();
        var dto = cmd.Document.FromJson<EspnBaseballEventCompetitionCompetitorDto>()!;
        dto.Probables![0].Athlete = null;
        var mutatedCmd = BuildCommand(dto.ToJson(), cmd.ParentId!, cmd.UrlHash);

        var sut = Mocker.CreateInstance<BaseballEventCompetitionCompetitorDocumentProcessor<BaseballDataContext>>();

        // act — must NOT throw; no athlete to resolve means a soft skip.
        await sut.ProcessAsync(mutatedCmd);

        // assert — no probable row, and no AthleteSeason dependency request fired.
        var probables = await _baseballDataContext.CompetitionCompetitorProbables
            .AsNoTracking()
            .ToListAsync();
        probables.Should().BeEmpty();

        var bus = Mocker.GetMock<IEventBus>();
        bus.Verify(x => x.Publish(
                It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.AthleteSeason),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private async Task<(ProcessDocumentCommand Cmd, Guid AthleteSeasonId)> SetupHappyPath(
        bool seedAthleteSeason = true)
    {
        var idGen = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(idGen);

        var competitorIdentity = idGen.Generate(CompetitorRef);
        var teamIdentity = idGen.Generate(TeamRef);
        var athleteSeasonIdentity = idGen.Generate(AthleteSeasonRef);

        // Parent competition (FK from CompetitionCompetitor).
        var competitionId = Guid.NewGuid();
        await _baseballDataContext.Competitions.AddAsync(new BaseballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        // FranchiseSeason resolved by team URL hash.
        await _baseballDataContext.FranchiseSeasons.AddAsync(new FranchiseSeason
        {
            Id = Guid.NewGuid(),
            Abbreviation = "TST",
            DisplayName = "Test FS",
            DisplayNameShort = "TFS",
            Slug = teamIdentity.CanonicalId.ToString(),
            Location = "Test Location",
            Name = "Test Franchise Season",
            ColorCodeHex = "#FFFFFF",
            ColorCodeAltHex = "#000000",
            IsActive = true,
            SeasonYear = 2026,
            FranchiseId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds =
            [
                new FranchiseSeasonExternalId
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = teamIdentity.CleanUrl,
                    SourceUrlHash = teamIdentity.UrlHash,
                    Value = teamIdentity.UrlHash
                }
            ]
        });

        if (seedAthleteSeason)
        {
            await _baseballDataContext.AthleteSeasons.AddAsync(new BaseballAthleteSeason
            {
                Id = athleteSeasonIdentity.CanonicalId,
                AthleteId = Guid.NewGuid(),
                FranchiseSeasonId = null,
                PositionId = Guid.NewGuid(),
                CreatedUtc = FixedNow,
                CreatedBy = Guid.NewGuid()
            });
        }

        await _baseballDataContext.SaveChangesAsync();

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionCompetitor.json");

        var cmd = BuildCommand(json, competitionId.ToString(), competitorIdentity.UrlHash);

        return (cmd, athleteSeasonIdentity.CanonicalId);
    }

    // Single source of truth for ProcessDocumentCommand construction in
    // this test class. Sport/year/document-type are baked since this
    // suite only exercises the MLB EventCompetitionCompetitor pipeline.
    // includeLinkedDocumentTypes lets a test pin the cascade-filter
    // behavior (Refresh Contest narrowing).
    private ProcessDocumentCommand BuildCommand(
        string document,
        string parentId,
        string urlHash,
        IReadOnlyCollection<DocumentType>? includeLinkedDocumentTypes = null)
    {
        return Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, parentId)
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitor)
            .With(x => x.Document, document)
            .With(x => x.UrlHash, urlHash)
            .With(x => x.IncludeLinkedDocumentTypes, includeLinkedDocumentTypes)
            .OmitAutoProperties()
            .Create();
    }
}
