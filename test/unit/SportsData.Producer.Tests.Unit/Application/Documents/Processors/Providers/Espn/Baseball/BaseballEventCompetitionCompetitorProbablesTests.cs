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
/// Tests for inline MLB Probables ingestion driven by
/// <see cref="BaseballEventCompetitionCompetitorDocumentProcessor{TDataContext}.ProcessSportSpecificCompetitorData"/>.
///
/// ESPN ships probable starting pitchers inline on the
/// EventCompetitionCompetitor payload. Each Probable carries a hard FK to
/// AthleteSeason — when the athlete isn't in the DB yet, the processor
/// publishes a dependency request and throws
/// ExternalDocumentNotSourcedException so Hangfire retries. No partial
/// rows; an empty Probable is worthless on the matchup card.
///
/// See docs/competition-competitor-probables.md.
/// </summary>
[Collection("Sequential")]
public class BaseballEventCompetitionCompetitorProbablesTests
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

    public BaseballEventCompetitionCompetitorProbablesTests()
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
        second.ModifiedUtc.Should().NotBeNull();
        second.ModifiedBy.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenProbableHasMissingName_IsSkippedWithoutThrowing()
    {
        // arrange — happy-path setup, then mutate the fixture to drop Name.
        var (cmd, _) = await SetupHappyPath();
        var dto = cmd.Document.FromJson<EspnBaseballEventCompetitionCompetitorDto>()!;
        dto.Probables![0].Name = null;
        var mutatedJson = dto.ToJson();
        var mutatedCmd = RebuildCommand(cmd, mutatedJson);

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
        var mutatedJson = dto.ToJson();
        var mutatedCmd = RebuildCommand(cmd, mutatedJson);

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

        var cmd = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, competitionId.ToString())
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitor)
            .With(x => x.Document, json)
            .With(x => x.UrlHash, competitorIdentity.UrlHash)
            .With(x => x.IncludeLinkedDocumentTypes, (IReadOnlyCollection<DocumentType>?)null)
            .OmitAutoProperties()
            .Create();

        return (cmd, athleteSeasonIdentity.CanonicalId);
    }

    private ProcessDocumentCommand RebuildCommand(ProcessDocumentCommand source, string newDocument)
    {
        return Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, source.ParentId)
            .With(x => x.SeasonYear, source.SeasonYear)
            .With(x => x.SourceDataProvider, source.SourceDataProvider)
            .With(x => x.Sport, source.Sport)
            .With(x => x.DocumentType, source.DocumentType)
            .With(x => x.Document, newDocument)
            .With(x => x.UrlHash, source.UrlHash)
            .With(x => x.IncludeLinkedDocumentTypes, source.IncludeLinkedDocumentTypes)
            .OmitAutoProperties()
            .Create();
    }
}
