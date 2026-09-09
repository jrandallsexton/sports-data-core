using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Producer.Application.Franchises;
using SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonMetricsGeneration;

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

    [Fact]
    public async Task Execute_AlsoEnqueuesMetricsGeneration_ForResolvedSeasonAndSport()
    {
        // The gap this job closed (2026-09-09): "enrichment" updated records
        // but never generated metrics — an operator triggering the job whose
        // name promises "make franchise seasons current" got an empty War
        // Room. This pins the metrics half so the name cannot silently lie
        // again.
        SetNow(new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();
        await sut.ExecuteAsync();

        _metricsHandler.Verify(x => x.ExecuteAsync(
            It.Is<EnqueueFranchiseSeasonMetricsGenerationCommand>(c =>
                c.SeasonYear == 2026 && c.Sport == Sport.FootballNcaa),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_InJanuary_TargetsPriorSeasonLabel()
    {
        // Season-year convention: bowls/playoffs finalize in January but
        // belong to the PRIOR season label. The old calendar-year default
        // targeted a season that didn't exist yet for every Jan-May run.
        SetNow(new DateTime(2027, 1, 15, 12, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();
        await sut.ExecuteAsync();

        _metricsHandler.Verify(x => x.ExecuteAsync(
            It.Is<EnqueueFranchiseSeasonMetricsGenerationCommand>(c => c.SeasonYear == 2026),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_ExplicitSeasonYear_PassesThroughUnchanged()
    {
        SetNow(new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentJob>();
        await sut.ExecuteAsync(2024);

        _metricsHandler.Verify(x => x.ExecuteAsync(
            It.Is<EnqueueFranchiseSeasonMetricsGenerationCommand>(c => c.SeasonYear == 2024),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
