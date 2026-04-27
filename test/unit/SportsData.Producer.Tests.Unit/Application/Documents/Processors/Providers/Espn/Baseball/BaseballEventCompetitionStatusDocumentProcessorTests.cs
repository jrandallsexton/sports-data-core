#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Baseball;

/// <summary>
/// Tests for <see cref="BaseballEventCompetitionStatusDocumentProcessor{TDataContext}"/>
/// — the MLB-specific processor that builds <c>BaseballCompetitionStatus</c>
/// (the sport-specific subclass) so the baseball-only fields and the
/// FeaturedAthletes child collection persist alongside the shared
/// status row.
///
/// Wires a class-local <see cref="BaseballDataContext"/> rather than
/// reusing <see cref="ProducerTestBase{T}.FootballDataContext"/> so
/// <c>BaseballCompetitionStatus</c> and
/// <c>BaseballCompetitionStatusFeaturedAthlete</c> are in the model
/// and the persistence path under test is real, not stubbed.
/// </summary>
[Collection("Sequential")]
public class BaseballEventCompetitionStatusDocumentProcessorTests
    : ProducerTestBase<BaseballEventCompetitionStatusDocumentProcessor<BaseballDataContext>>
{
    private static readonly DateTime FixedNow =
        new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc);

    private readonly BaseballDataContext _baseballDataContext;

    public BaseballEventCompetitionStatusDocumentProcessorTests()
    {
        // Class-local BaseballDataContext so the SUT's
        // _dataContext.Set<BaseballCompetitionStatus>() finds a registered
        // subclass. The parent ProducerTestBase still wires Football for
        // the abstract TeamSportDataContext / BaseDataContext slots used
        // by other processors; this Use(...) overlays a concrete
        // BaseballDataContext for direct injection by Mocker.
        _baseballDataContext = new BaseballDataContext(
            new DbContextOptionsBuilder<BaseballDataContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()[..8])
                .Options);
        Mocker.Use(_baseballDataContext);

        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);
    }

    private async Task<(BaseballContest contest, BaseballCompetition competition)>
        SeedContestAndCompetitionAsync(Guid competitionId)
    {
        var contest = new BaseballContest
        {
            Id = Guid.NewGuid(),
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
            ContestId = contest.Id,
            Date = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };

        await _baseballDataContext.Contests.AddAsync(contest);
        await _baseballDataContext.Competitions.AddAsync(competition);
        await _baseballDataContext.SaveChangesAsync();

        return (contest, competition);
    }

    [Fact]
    public async Task EspnBaseballEventCompetitionStatusDto_DeserializesMlbFields()
    {
        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionStatus.json");

        var dto = json.FromJson<EspnBaseballEventCompetitionStatusDto>();

        dto.Should().NotBeNull();
        dto!.Type.Name.Should().Be("STATUS_FINAL");
        dto.HalfInning.Should().Be(17);
        dto.PeriodPrefix.Should().Be("Bottom");
        dto.FeaturedAthletes.Should().HaveCount(2);
        dto.FeaturedAthletes![0].Name.Should().Be("winningPitcher");
        dto.FeaturedAthletes[0].PlayerId.Should().Be(4987924);
        dto.FeaturedAthletes[0].Athlete!.Ref.Should().NotBeNull();
        dto.FeaturedAthletes[1].Name.Should().Be("losingPitcher");
    }

    [Fact]
    public async Task WhenNoExisting_PersistsStatus_WithMlbFieldsAndFeaturedAthletes_AndDoesNotPublishStatusChanged()
    {
        // arrange
        var bus = Mocker.GetMock<IEventBus>();
        var idGen = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(idGen);

        var compId = Guid.NewGuid();
        await SeedContestAndCompetitionAsync(compId);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionStatus.json");

        var cmd = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, compId.ToString())
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.DocumentType, DocumentType.EventCompetitionStatus)
            .With(x => x.Document, json)
            .With(x => x.UrlHash,
                "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844/status?lang=en&region=us"
                    .UrlHash())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .OmitAutoProperties()
            .Create();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionStatusDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — persisted as the MLB subtype with all MLB fields
        var status = await _baseballDataContext.Set<BaseballCompetitionStatus>()
            .Include(x => x.FeaturedAthletes)
            .Where(x => x.CompetitionId == compId)
            .ToListAsync();

        status.Should().ContainSingle();
        var entity = status[0];
        entity.StatusTypeName.Should().Be("STATUS_FINAL");
        entity.IsCompleted.Should().BeTrue();
        entity.HalfInning.Should().Be(17);
        entity.PeriodPrefix.Should().Be("Bottom");

        // FeaturedAthletes children persisted with refs preserved.
        entity.FeaturedAthletes.Should().HaveCount(2);
        var winning = entity.FeaturedAthletes.Single(a => a.Name == "winningPitcher");
        winning.PlayerId.Should().Be(4987924);
        winning.AthleteRef!.AbsoluteUri.Should().Contain("/athletes/4987924");
        winning.TeamRef!.AbsoluteUri.Should().Contain("/teams/5");
        winning.StatisticsRef.Should().NotBeNull();

        // Initial create — no prior status to compare against, so the
        // status-changed branch must not fire.
        bus.Verify(
            x => x.Publish(It.IsAny<CompetitionStatusChanged>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenStatusTypeNameChanges_HardReplacesRowAndPublishesCompetitionStatusChanged()
    {
        // arrange — pre-seed a row with a DIFFERENT status name so the
        // comparison detects a change.
        var bus = Mocker.GetMock<IEventBus>();
        var idGen = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(idGen);

        var compId = Guid.NewGuid();
        await SeedContestAndCompetitionAsync(compId);

        var oldRowId = Guid.NewGuid();
        var existing = new BaseballCompetitionStatus
        {
            Id = oldRowId,
            CompetitionId = compId,
            StatusTypeName = "STATUS_IN_PROGRESS",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };
        await _baseballDataContext.Set<BaseballCompetitionStatus>().AddAsync(existing);
        await _baseballDataContext.SaveChangesAsync();

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionStatus.json");

        var cmd = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, compId.ToString())
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.DocumentType, DocumentType.EventCompetitionStatus)
            .With(x => x.Document, json)
            .With(x => x.UrlHash,
                "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844/status?lang=en&region=us"
                    .UrlHash())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .OmitAutoProperties()
            .Create();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionStatusDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — old row replaced by a new row carrying the new status.
        var saved = await _baseballDataContext.Set<BaseballCompetitionStatus>()
            .AsNoTracking()
            .Where(s => s.CompetitionId == compId)
            .ToListAsync();
        saved.Should().ContainSingle();
        saved[0].Id.Should().NotBe(oldRowId);
        saved[0].StatusTypeName.Should().Be("STATUS_FINAL");

        // assert — CompetitionStatusChanged published exactly once with
        // the new status name.
        bus.Verify(
            x => x.Publish(
                It.Is<CompetitionStatusChanged>(e =>
                    e.CompetitionId == compId &&
                    e.Status == "STATUS_FINAL"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FeaturedAthletes_PersistInEspnSourceOrderViaOrdinal()
    {
        // arrange
        var idGen = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(idGen);

        var compId = Guid.NewGuid();
        await SeedContestAndCompetitionAsync(compId);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionStatus.json");

        var cmd = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, compId.ToString())
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.DocumentType, DocumentType.EventCompetitionStatus)
            .With(x => x.Document, json)
            .With(x => x.UrlHash,
                "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844/status?lang=en&region=us"
                    .UrlHash())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .OmitAutoProperties()
            .Create();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionStatusDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — Ordinal mirrors ESPN's source array index
        // (winningPitcher [0], losingPitcher [1] from the fixture).
        var athletes = await _baseballDataContext.Set<BaseballCompetitionStatusFeaturedAthlete>()
            .AsNoTracking()
            .OrderBy(a => a.Ordinal)
            .ToListAsync();

        athletes.Should().HaveCount(2);
        athletes[0].Ordinal.Should().Be(0);
        athletes[0].Name.Should().Be("winningPitcher");
        athletes[1].Ordinal.Should().Be(1);
        athletes[1].Name.Should().Be("losingPitcher");
    }
}
