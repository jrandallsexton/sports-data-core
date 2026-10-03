#nullable enable

using System.Linq.Expressions;

using AutoFixture;

using FluentAssertions;

using FluentValidation;

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

    private readonly Guid _correlationId = Guid.NewGuid();

    // The enqueued lambdas, captured so the tests can inspect WHAT was
    // enqueued (franchise season, correlation id), not just that something was.
    private Expression<Func<IEnrichFranchiseSeasons, Task>>? _enrichCall;
    private Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>? _metricsCall;

    public EnqueueSingleFranchiseSeasonEnrichmentCommandHandlerTests()
    {
        SetSport(Sport.FootballNcaa);
        Mocker.Use<IValidator<EnqueueSingleFranchiseSeasonEnrichmentCommand>>(
            new EnqueueSingleFranchiseSeasonEnrichmentCommandValidator());

        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue(It.IsAny<Expression<Func<IEnrichFranchiseSeasons, Task>>>()))
            .Callback<Expression<Func<IEnrichFranchiseSeasons, Task>>>(e => _enrichCall = e);
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue(It.IsAny<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>>()))
            .Callback<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>>(e => _metricsCall = e);
    }

    private void SetSport(Sport sport) =>
        Mocker.GetMock<IAppMode>().SetupGet(x => x.CurrentSport).Returns(sport);

    private EnqueueSingleFranchiseSeasonEnrichmentCommand Command(Guid franchiseSeasonId) =>
        new(franchiseSeasonId, _correlationId);

    /// <summary>First argument of the captured enqueue lambda, evaluated.</summary>
    private static T FirstArgument<T>(LambdaExpression captured)
    {
        var call = (MethodCallExpression)captured.Body;
        return (T)Expression.Lambda(call.Arguments[0]).Compile().DynamicInvoke()!;
    }

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
                // A deliberately unparseable ref still needs a stored hash.
                SourceUrlHash = Uri.TryCreate(espnUrl, UriKind.Absolute, out var parsed)
                    ? HashProvider.GenerateHashFromUri(parsed)
                    : "unparseable"
            });
        }

        await FootballDataContext.SaveChangesAsync();
        return franchiseSeason.Id;
    }

    [Fact]
    public async Task Execute_Football_RunsAllThreeLegs_ForThatSeason_UnderTheCallersCorrelationId()
    {
        var id = await SeedFranchiseSeasonAsync(2026, espnUrl: EspnUrl);
        var sut = Mocker.CreateInstance<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>();

        var result = await sut.ExecuteAsync(Command(id));

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);
        // The caller's id is echoed back, never a fresh one: API and
        // Producer must log under the same Seq handle.
        result.Value.Should().Be(_correlationId);

        // Leg 1: record enrichment for THIS franchise season, THIS id.
        _enrichCall.Should().NotBeNull();
        var enrich = FirstArgument<EnrichFranchiseSeasonCommand>(_enrichCall!);
        enrich.FranchiseSeasonId.Should().Be(id);
        enrich.SeasonYear.Should().Be(2026);
        enrich.CorrelationId.Should().Be(_correlationId);

        // Leg 2: one scoped statistics request, published Direct, same id.
        Mocker.GetMock<IEventBus>().Verify(x => x.PublishBatch(
            It.Is<IEnumerable<DocumentRequested>>(batch =>
                batch.Count() == 1 &&
                batch.All(d =>
                    d.DocumentType == DocumentType.TeamSeason &&
                    d.SourceDataProvider == SourceDataProvider.Espn &&
                    d.SeasonYear == 2026 &&
                    d.CorrelationId == _correlationId &&
                    d.Uri.ToString().Contains("/teams/50") &&
                    d.IncludeLinkedDocumentTypes != null &&
                    d.IncludeLinkedDocumentTypes.Count == 1 &&
                    d.IncludeLinkedDocumentTypes[0] == DocumentType.TeamSeasonStatistics)),
            It.IsAny<CancellationToken>()), Times.Once);
        Mocker.GetMock<IMessageDeliveryScope>().Verify(x => x.Use(DeliveryMode.Direct), Times.Once);

        // Leg 3: metrics for THIS franchise season.
        _metricsCall.Should().NotBeNull();
        var metrics = FirstArgument<CalculateFranchiseSeasonMetricsCommand>(_metricsCall!);
        metrics.FranchiseSeasonId.Should().Be(id);
        metrics.SeasonYear.Should().Be(2026);
    }

    [Fact]
    public async Task Execute_NoEspnRef_SkipsStatistics_StillEnrichesAndComputesMetrics()
    {
        var id = await SeedFranchiseSeasonAsync(2026); // no ESPN ref
        var sut = Mocker.CreateInstance<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>();

        var result = await sut.ExecuteAsync(Command(id));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(x => x.PublishBatch(
            It.IsAny<IEnumerable<DocumentRequested>>(), It.IsAny<CancellationToken>()), Times.Never);
        FirstArgument<EnrichFranchiseSeasonCommand>(_enrichCall!).FranchiseSeasonId.Should().Be(id);
        FirstArgument<CalculateFranchiseSeasonMetricsCommand>(_metricsCall!).FranchiseSeasonId.Should().Be(id);
    }

    [Fact]
    public async Task Execute_UnparseableEspnRef_SkipsStatistics_LikeNoRef()
    {
        // The ref exists but is not an absolute URI: the publish is skipped
        // and the summary must say so (the log line is the operator's record).
        var id = await SeedFranchiseSeasonAsync(2026, espnUrl: "not a url");
        var sut = Mocker.CreateInstance<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>();

        var result = await sut.ExecuteAsync(Command(id));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(x => x.PublishBatch(
            It.IsAny<IEnumerable<DocumentRequested>>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var result = await sut.ExecuteAsync(Command(id));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(x => x.PublishBatch(
            It.Is<IEnumerable<DocumentRequested>>(b => b.Count() == 1), It.IsAny<CancellationToken>()), Times.Once);
        _metricsCall.Should().BeNull();
    }

    [Fact]
    public async Task Execute_UnknownFranchiseSeason_ReturnsNotFound_AndEnqueuesNothing()
    {
        var sut = Mocker.CreateInstance<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>();

        var result = await sut.ExecuteAsync(Command(Guid.NewGuid()));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        _enrichCall.Should().BeNull();
        _metricsCall.Should().BeNull();
        Mocker.GetMock<IEventBus>().Verify(x => x.PublishBatch(
            It.IsAny<IEnumerable<DocumentRequested>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true, false)]  // empty franchise season id
    [InlineData(false, true)]  // empty correlation id
    public async Task Execute_InvalidCommand_ReturnsValidationFailure_WithoutTouchingTheDatabase(bool emptyId, bool emptyCorrelation)
    {
        var sut = Mocker.CreateInstance<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>();
        var command = new EnqueueSingleFranchiseSeasonEnrichmentCommand(
            emptyId ? Guid.Empty : Guid.NewGuid(),
            emptyCorrelation ? Guid.Empty : Guid.NewGuid());

        var result = await sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        _enrichCall.Should().BeNull();
    }

    [Fact]
    public async Task Execute_StatisticsPublishThrows_ReportsFailure_NamingTheCorrelationId_NotTheException()
    {
        // Unlike the weekly job (which must never throw into Hangfire's
        // retry), this is a synchronous admin request: the operator should
        // see the failure AND the id leg 1 already runs under, and never the
        // raw exception text (the UI renders the error message).
        var id = await SeedFranchiseSeasonAsync(2026, espnUrl: EspnUrl);
        Mocker.GetMock<IEventBus>()
            .Setup(x => x.PublishBatch(It.IsAny<IEnumerable<DocumentRequested>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down: amqp://secret@host"));
        var sut = Mocker.CreateInstance<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler>();

        var result = await sut.ExecuteAsync(Command(id));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        var message = ((Failure<Guid>)result).Errors.Single().ErrorMessage;
        message.Should().Contain(_correlationId.ToString());
        message.Should().NotContain("amqp://");
    }
}
