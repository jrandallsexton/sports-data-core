#nullable enable

using System.Linq.Expressions;

using AutoFixture;

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Franchises.Commands;
using SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;
using SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueSingleFranchiseSeasonEnrichment;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasons.Commands;

public class EnqueueSingleFranchiseSeasonEnrichmentCommandHandlerTests
    : ProducerTestBase<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>
{
    private const string EspnUrl =
        "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2026/teams/50";

    public EnqueueSingleFranchiseSeasonEnrichmentCommandHandlerTests()
    {
        SetSport(Sport.FootballNcaa);
    }

    private void SetSport(Sport sport) =>
        Mocker.GetMock<IAppMode>().SetupGet(x => x.CurrentSport).Returns(sport);

    private async Task<Guid> SeedFranchiseSeasonAsync(int seasonYear, Sport sport = Sport.FootballNcaa, string? espnUrl = null)
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
        return franchiseSeason.Id;
    }

    [Fact]
    public async Task Execute_Football_RunsAllThreeLegs_UnderOneCorrelationId()
    {
        var id = await SeedFranchiseSeasonAsync(2026, espnUrl: EspnUrl);
        var sut = Mocker.CreateInstance<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>();

        var result = await sut.ExecuteAsync(new EnqueueSingleFranchiseSeasonEnrichmentCommand(id));

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);
        result.Value.Should().NotBe(Guid.Empty);

        // Leg 1: record enrichment for THIS franchise season.
        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IEnrichFranchiseSeasons, Task>>>()),
            Times.Once);

        // Leg 2: one scoped statistics request, published Direct.
        Mocker.GetMock<IEventBus>().Verify(x => x.PublishBatch(
            It.Is<IEnumerable<DocumentRequested>>(batch =>
                batch.Count() == 1 &&
                batch.All(d =>
                    d.DocumentType == DocumentType.TeamSeason &&
                    d.SourceDataProvider == SourceDataProvider.Espn &&
                    d.SeasonYear == 2026 &&
                    d.CorrelationId == result.Value &&
                    d.Uri.ToString().Contains("/teams/50") &&
                    d.IncludeLinkedDocumentTypes != null &&
                    d.IncludeLinkedDocumentTypes.Count == 1 &&
                    d.IncludeLinkedDocumentTypes[0] == DocumentType.TeamSeasonStatistics)),
            It.IsAny<CancellationToken>()), Times.Once);
        Mocker.GetMock<IMessageDeliveryScope>().Verify(x => x.Use(DeliveryMode.Direct), Times.Once);

        // Leg 3: metrics for THIS franchise season.
        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_NoEspnRef_SkipsStatistics_StillEnrichesAndComputesMetrics()
    {
        var id = await SeedFranchiseSeasonAsync(2026); // no ESPN ref
        var sut = Mocker.CreateInstance<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>();

        var result = await sut.ExecuteAsync(new EnqueueSingleFranchiseSeasonEnrichmentCommand(id));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(x => x.PublishBatch(
            It.IsAny<IEnumerable<DocumentRequested>>(), It.IsAny<CancellationToken>()), Times.Never);
        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IEnrichFranchiseSeasons, Task>>>()), Times.Once);
        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>>()), Times.Once);
    }

    [Fact]
    public async Task Execute_Baseball_SkipsMetrics_StillRefreshesStatistics()
    {
        // Mirrors the weekly job's sport gate: the Calculate handler is not
        // registered on a baseball pod; the statistics processor is.
        SetSport(Sport.BaseballMlb);
        var id = await SeedFranchiseSeasonAsync(2026, Sport.BaseballMlb,
            espnUrl: "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/seasons/2026/teams/10");
        var sut = Mocker.CreateInstance<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>();

        var result = await sut.ExecuteAsync(new EnqueueSingleFranchiseSeasonEnrichmentCommand(id));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(x => x.PublishBatch(
            It.Is<IEnumerable<DocumentRequested>>(b => b.Count() == 1), It.IsAny<CancellationToken>()), Times.Once);
        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>>()), Times.Never);
    }

    [Fact]
    public async Task Execute_UnknownFranchiseSeason_ReturnsNotFound_AndEnqueuesNothing()
    {
        var sut = Mocker.CreateInstance<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>();

        var result = await sut.ExecuteAsync(new EnqueueSingleFranchiseSeasonEnrichmentCommand(Guid.NewGuid()));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IEnrichFranchiseSeasons, Task>>>()), Times.Never);
        Mocker.GetMock<IEventBus>().Verify(x => x.PublishBatch(
            It.IsAny<IEnumerable<DocumentRequested>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_EmptyId_ReturnsValidationFailure()
    {
        var sut = Mocker.CreateInstance<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>();

        var result = await sut.ExecuteAsync(new EnqueueSingleFranchiseSeasonEnrichmentCommand(Guid.Empty));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task Execute_StatisticsPublishThrows_ReportsFailure()
    {
        // Unlike the weekly job (which must never throw into Hangfire's
        // retry), this is a synchronous admin request: the operator should
        // see the failure, and every leg is safe to re-request.
        var id = await SeedFranchiseSeasonAsync(2026, espnUrl: EspnUrl);
        Mocker.GetMock<IEventBus>()
            .Setup(x => x.PublishBatch(It.IsAny<IEnumerable<DocumentRequested>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));
        var sut = Mocker.CreateInstance<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>();

        var result = await sut.ExecuteAsync(new EnqueueSingleFranchiseSeasonEnrichmentCommand(id));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
    }
}
