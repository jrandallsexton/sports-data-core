#nullable enable

using System.Linq.Expressions;

using AutoFixture;

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Franchises;
using SportsData.Producer.Application.Franchises.Commands;
using SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonMetricsGeneration;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Franchises;

public class FranchiseSeasonEnrichmentJobTests : ProducerTestBase<FranchiseSeasonEnrichmentJob>
{
    private readonly Mock<IEnqueueFranchiseSeasonMetricsGenerationCommandHandler> _metricsHandler;

    public FranchiseSeasonEnrichmentJobTests()
    {
        Mocker.GetMock<IAppMode>()
            .SetupGet(x => x.CurrentSport)
            .Returns(Sport.FootballNcaa);

        _metricsHandler = Mocker.GetMock<IEnqueueFranchiseSeasonMetricsGenerationCommandHandler>();
        _metricsHandler
            .Setup(x => x.ExecuteAsync(
                It.IsAny<EnqueueFranchiseSeasonMetricsGenerationCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<Guid>(Guid.NewGuid(), ResultStatus.Accepted));
    }

    private void SetNow(DateTime utcNow) =>
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(utcNow);

    private void SetSport(Sport sport) =>
        Mocker.GetMock<IAppMode>()
            .SetupGet(x => x.CurrentSport)
            .Returns(sport);

    private async Task SeedFranchiseSeasonAsync(int seasonYear, Sport sport = Sport.FootballNcaa, string? espnUrl = null)
    {
        var franchise = Fixture.Build<Franchise>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Name, "Test Franchise")
            .With(x => x.DisplayName, "Test Franchise")
            .With(x => x.DisplayNameShort, "Test")
            .With(x => x.Location, "Test City")
            .With(x => x.Slug, $"test-franchise-{Guid.NewGuid()}")
            .With(x => x.ColorCodeHex, "#000000")
            .With(x => x.Sport, sport)
            .Create();

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseId, franchise.Id)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.Name, "Test Franchise Season")
            .With(x => x.DisplayName, "Test Franchise Season")
            .With(x => x.DisplayNameShort, "Test")
            .With(x => x.Abbreviation, "TEST")
            .With(x => x.Location, "Test City")
            .With(x => x.Slug, $"test-franchise-season-{Guid.NewGuid()}")
            .With(x => x.ColorCodeHex, "#000000")
            .Create();

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        if (espnUrl is not null)
        {
            await FootballDataContext.Set<FranchiseSeasonExternalId>().AddAsync(new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeason.Id,
                Provider = SourceDataProvider.Espn,
                Value = "50",
                SourceUrl = espnUrl,
                SourceUrlHash = HashProvider.GenerateHashFromUri(new Uri(espnUrl))
            });
        }

        await FootballDataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Execute_AlsoEnqueuesMetricsGeneration_ForResolvedSeasonAndSport()
    {
        // The gap this job closed (2026-09-09): "enrichment" updated records
        // but never generated metrics — an operator triggering the job whose
        // name promises "make franchise seasons current" got an empty War
        // Room. This pins the metrics half so the name cannot silently lie
        // again.
        SetNow(new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));
        await SeedFranchiseSeasonAsync(2026);

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();
        await sut.ExecuteAsync();

        _metricsHandler.Verify(x => x.ExecuteAsync(
            It.Is<EnqueueFranchiseSeasonMetricsGenerationCommand>(c =>
                c.SeasonYear == 2026 && c.Sport == Sport.FootballNcaa),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_FootballInJanuary_TargetsPriorSeasonLabel()
    {
        // Football season-year convention: bowls/playoffs finalize in
        // January but belong to the PRIOR season label. The old
        // calendar-year default targeted a season that didn't exist yet for
        // every Jan-May run.
        SetNow(new DateTime(2027, 1, 15, 12, 0, 0, DateTimeKind.Utc));
        await SeedFranchiseSeasonAsync(2026);

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();
        await sut.ExecuteAsync();

        _metricsHandler.Verify(x => x.ExecuteAsync(
            It.Is<EnqueueFranchiseSeasonMetricsGenerationCommand>(c => c.SeasonYear == 2026),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_BaseballInApril_KeepsCalendarYear()
    {
        // The June rollover is FOOTBALL-only (Vortex, PR #744): an MLB
        // season is current from opening day and labeled by the calendar
        // year — the rollover would spend April/May enriching LAST season.
        // Observable via the enrichment fan-out (metrics are sport-gated
        // off for baseball): only the calendar-year row must be picked up.
        SetSport(Sport.BaseballMlb);
        SetNow(new DateTime(2027, 4, 15, 12, 0, 0, DateTimeKind.Utc));
        await SeedFranchiseSeasonAsync(2027, Sport.BaseballMlb);
        await SeedFranchiseSeasonAsync(2026, Sport.BaseballMlb);

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();
        await sut.ExecuteAsync();

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<EnrichFranchiseSeasonHandler<TeamSportDataContext>, Task>>>()),
            Times.Once); // the single 2027 row, not the 2026 one
    }

    [Fact]
    public async Task Execute_NonFootballSport_SkipsMetricsGeneration()
    {
        // ICalculateFranchiseSeasonMetricsCommandHandler is registered only
        // under ServiceRegistration's football guard (FootballDataContext
        // dependency). On a BaseballMlb pod the enqueue would "succeed" and
        // every fanned-out job would fail at Hangfire activation, weekly,
        // forever (Vortex, PR #744). The job's sport gate mirrors the
        // registration guard; this pins it.
        SetSport(Sport.BaseballMlb);
        SetNow(new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));
        await SeedFranchiseSeasonAsync(2026, Sport.BaseballMlb);

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();
        await sut.ExecuteAsync();

        _metricsHandler.Verify(x => x.ExecuteAsync(
            It.IsAny<EnqueueFranchiseSeasonMetricsGenerationCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_SeasonNotSourcedYet_NoOpsWithoutTouchingMetrics()
    {
        // Off-season window (June rollover -> hierarchy sourced): zero
        // franchise seasons exist for the resolved year, and the NCAA
        // metrics handler THROWS on a missing FBS root. Pre-PR this window
        // was a harmless no-op; the empty guard keeps it one (Vortex round
        // 2, PR #744).
        SetNow(new DateTime(2027, 6, 15, 12, 0, 0, DateTimeKind.Utc)); // resolves 2027; nothing seeded

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();
        await sut.ExecuteAsync();

        _metricsHandler.Verify(x => x.ExecuteAsync(
            It.IsAny<EnqueueFranchiseSeasonMetricsGenerationCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<EnrichFranchiseSeasonHandler<TeamSportDataContext>, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_MetricsHandlerThrows_DoesNotEscapeTheJob()
    {
        // Partial-sourcing window (Vortex round 3, PR #744): FranchiseSeasons
        // exist but the FBS GroupSeason root doesn't yet, so the metrics
        // handler THROWS ("FBS group root(s) not found") after the
        // record-enrichment fan-out already ran. An escaped exception makes
        // Hangfire's AutomaticRetry re-run the whole job — duplicating the
        // full enrichment fan-out up to 10 times. The job must swallow-and-
        // log instead; the missed metrics pass self-heals next week.
        SetNow(new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));
        await SeedFranchiseSeasonAsync(2026);
        _metricsHandler
            .Setup(x => x.ExecuteAsync(
                It.IsAny<EnqueueFranchiseSeasonMetricsGenerationCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("FBS group root(s) not found."));

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();

        var act = async () => await sut.ExecuteAsync();

        await act.Should().NotThrowAsync();
        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<EnrichFranchiseSeasonHandler<TeamSportDataContext>, Task>>>()),
            Times.Once); // the fan-out ran exactly once — no retry storm
    }

    [Fact]
    public async Task Execute_PublishesScopedStatisticsRequest_PerTeamWithEspnRef()
    {
        // The statistics leg (2026-09-10): one DocumentRequested per team
        // for its TeamSeason doc, scoped to spawn ONLY the statistics child
        // (athlete-cascade-scoping vocabulary). Before this, statistics
        // refreshed only as a side effect of poll appearances - FAMU@Miami
        // exposed unranked teams keeping empty initial-sourcing statistics
        // forever.
        SetNow(new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));
        await SeedFranchiseSeasonAsync(2026, espnUrl: "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2026/teams/50");
        await SeedFranchiseSeasonAsync(2026); // no ESPN ref - must be skipped, not crash

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();
        await sut.ExecuteAsync();

        Mocker.GetMock<IEventBus>().Verify(x => x.Publish(
            It.Is<DocumentRequested>(d =>
                d.DocumentType == DocumentType.TeamSeason &&
                d.SourceDataProvider == SourceDataProvider.Espn &&
                d.SeasonYear == 2026 &&
                d.Uri.ToString().Contains("/teams/50") &&
                d.IncludeLinkedDocumentTypes != null &&
                d.IncludeLinkedDocumentTypes.Count == 1 &&
                d.IncludeLinkedDocumentTypes[0] == DocumentType.TeamSeasonStatistics),
            It.IsAny<CancellationToken>()), Times.Once);

        // The publish must run inside an explicit Direct delivery scope:
        // the Producer's ambient EF outbox is always active, and this
        // read-only job never saves, so an unscoped publish is captured
        // and silently discarded at scope disposal (Vortex blocker,
        // PR #749). The mocked bus cannot see routing; the scope call can
        // be pinned.
        Mocker.GetMock<IMessageDeliveryScope>()
            .Verify(x => x.Use(DeliveryMode.Direct), Times.Once);
    }

    [Fact]
    public async Task Execute_NonFootballSport_StillRefreshesStatistics()
    {
        // Metrics are football-gated (no Calculate registration on baseball
        // pods) but the TeamSeasonStatistics processor is registered for
        // every team sport - the statistics leg must NOT share the gate.
        SetSport(Sport.BaseballMlb);
        SetNow(new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));
        await SeedFranchiseSeasonAsync(2026, Sport.BaseballMlb,
            espnUrl: "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/seasons/2026/teams/10");

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();
        await sut.ExecuteAsync();

        Mocker.GetMock<IEventBus>().Verify(x => x.Publish(
            It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.TeamSeason),
            It.IsAny<CancellationToken>()), Times.Once);
        _metricsHandler.Verify(x => x.ExecuteAsync(
            It.IsAny<EnqueueFranchiseSeasonMetricsGenerationCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_OneStatisticsPublishFails_RemainingTeamsStillRequested()
    {
        // Per-team catch (CodeRabbit, PR #749): one team's publish failure
        // must not starve every remaining team's weekly refresh.
        SetNow(new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));
        await SeedFranchiseSeasonAsync(2026, espnUrl: "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2026/teams/50");
        await SeedFranchiseSeasonAsync(2026, espnUrl: "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2026/teams/51");
        Mocker.GetMock<IEventBus>()
            .SetupSequence(x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient broker fault"))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();

        var act = async () => await sut.ExecuteAsync();

        await act.Should().NotThrowAsync();
        Mocker.GetMock<IEventBus>().Verify(x => x.Publish(
            It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)); // second team attempted despite the first failing
    }

    [Fact]
    public async Task Execute_StatisticsPublishThrows_JobStillRunsMetrics()
    {
        // Same never-throw rule as the metrics leg: a broker fault in the
        // statistics fan-out must not escape (Hangfire AutomaticRetry would
        // re-run the whole job) and must not starve the metrics leg.
        SetNow(new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));
        await SeedFranchiseSeasonAsync(2026, espnUrl: "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2026/teams/50");
        Mocker.GetMock<IEventBus>()
            .Setup(x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();

        var act = async () => await sut.ExecuteAsync();

        await act.Should().NotThrowAsync();
        _metricsHandler.Verify(x => x.ExecuteAsync(
            It.Is<EnqueueFranchiseSeasonMetricsGenerationCommand>(c => c.SeasonYear == 2026),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_ExplicitSeasonYear_PassesThroughUnchanged()
    {
        SetNow(new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));
        await SeedFranchiseSeasonAsync(2024);

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();
        await sut.ExecuteAsync(2024);

        _metricsHandler.Verify(x => x.ExecuteAsync(
            It.Is<EnqueueFranchiseSeasonMetricsGenerationCommand>(c => c.SeasonYear == 2024),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
