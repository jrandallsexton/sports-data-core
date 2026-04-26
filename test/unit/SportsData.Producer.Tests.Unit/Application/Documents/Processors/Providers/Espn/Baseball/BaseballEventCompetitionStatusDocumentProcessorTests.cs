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
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Baseball;

/// <summary>
/// Tests for BaseballEventCompetitionStatusDocumentProcessor — MLB-specific
/// processor that captures the baseball-only payload fields the generic
/// status processor would otherwise drop (HalfInning, PeriodPrefix, and
/// the FeaturedAthletes child collection).
///
/// Uses <see cref="FootballDataContext"/> as the concrete TDataContext
/// (same pragmatic pattern as the other Baseball processor tests in this
/// folder).
/// </summary>
[Collection("Sequential")]
public class BaseballEventCompetitionStatusDocumentProcessorTests
    : ProducerTestBase<BaseballEventCompetitionStatusDocumentProcessor<FootballDataContext>>
{
    private async Task<CompetitionBase> CreateTestCompetitionAsync(Guid competitionId)
    {
        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();

        return competition;
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
    public async Task WhenNoExisting_PersistsStatus_WithMlbFieldsAndFeaturedAthletes_PublishesNothingOnInitialCreate()
    {
        // arrange
        var bus = Mocker.GetMock<IEventBus>();
        var idGen = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(idGen);

        var compId = Guid.NewGuid();
        await CreateTestCompetitionAsync(compId);

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

        var sut = Mocker.CreateInstance<BaseballEventCompetitionStatusDocumentProcessor<FootballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — single status row persisted with MLB-specific fields
        var status = await FootballDataContext.CompetitionStatuses
            .Include(x => x.FeaturedAthletes)
            .Where(x => x.CompetitionId == compId)
            .ToListAsync();

        status.Should().ContainSingle();
        var entity = status[0];
        entity.StatusTypeName.Should().Be("STATUS_FINAL");
        entity.IsCompleted.Should().BeTrue();
        entity.HalfInning.Should().Be(17);
        entity.PeriodPrefix.Should().Be("Bottom");

        // assert — both featured athletes persisted with their refs preserved
        entity.FeaturedAthletes.Should().HaveCount(2);
        var winning = entity.FeaturedAthletes.Single(a => a.Name == "winningPitcher");
        winning.PlayerId.Should().Be(4987924);
        winning.AthleteRef!.AbsoluteUri.Should().Contain("/athletes/4987924");
        winning.TeamRef!.AbsoluteUri.Should().Contain("/teams/5");
        winning.StatisticsRef.Should().NotBeNull();

        // assert — initial create does not flip publishEvent (no prior status to compare to)
        bus.Verify(x => x.Publish(It.IsAny<CompetitionStatusChanged>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenExistingStatusUnchanged_HardReplacesRow_DoesNotPublishStatusChanged()
    {
        // arrange — pre-seed an existing status whose StatusTypeName matches
        // the incoming doc, so the status-name comparison says "unchanged"
        // and no event publishes, but the row is still hard-replaced.
        var bus = Mocker.GetMock<IEventBus>();
        var idGen = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(idGen);

        var compId = Guid.NewGuid();
        await CreateTestCompetitionAsync(compId);

        var existingStatus = new CompetitionStatus
        {
            Id = Guid.NewGuid(),
            CompetitionId = compId,
            StatusTypeName = "STATUS_FINAL",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.CompetitionStatuses.AddAsync(existingStatus);
        await FootballDataContext.SaveChangesAsync();

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

        var sut = Mocker.CreateInstance<BaseballEventCompetitionStatusDocumentProcessor<FootballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — exactly one row remains, but with MLB fields populated
        // (proves the hard-replace happened even when no status change event fires)
        var status = await FootballDataContext.CompetitionStatuses
            .AsNoTracking()
            .Include(x => x.FeaturedAthletes)
            .Where(x => x.CompetitionId == compId)
            .ToListAsync();

        status.Should().ContainSingle();
        status[0].HalfInning.Should().Be(17);
        status[0].PeriodPrefix.Should().Be("Bottom");
        status[0].FeaturedAthletes.Should().HaveCount(2);

        bus.Verify(x => x.Publish(It.IsAny<CompetitionStatusChanged>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenStatusTypeNameChanges_PublishesCompetitionStatusChanged()
    {
        // arrange — pre-seed with a DIFFERENT status name so the comparison
        // detects a change and the event publishes.
        var bus = Mocker.GetMock<IEventBus>();
        var idGen = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(idGen);

        var compId = Guid.NewGuid();
        await CreateTestCompetitionAsync(compId);

        var existingStatus = new CompetitionStatus
        {
            Id = Guid.NewGuid(),
            CompetitionId = compId,
            StatusTypeName = "STATUS_IN_PROGRESS",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.CompetitionStatuses.AddAsync(existingStatus);
        await FootballDataContext.SaveChangesAsync();

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

        var sut = Mocker.CreateInstance<BaseballEventCompetitionStatusDocumentProcessor<FootballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — event published once with the new status name
        bus.Verify(
            x => x.Publish(
                It.Is<CompetitionStatusChanged>(e =>
                    e.CompetitionId == compId &&
                    e.Status == "STATUS_FINAL"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
