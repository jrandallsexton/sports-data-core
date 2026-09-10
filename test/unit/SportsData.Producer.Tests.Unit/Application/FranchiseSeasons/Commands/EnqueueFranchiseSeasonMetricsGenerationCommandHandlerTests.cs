using System.Linq.Expressions;

using AutoFixture;

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;
using SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonMetricsGeneration;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasons.Commands;

public class EnqueueFranchiseSeasonMetricsGenerationCommandHandlerTests :
    ProducerTestBase<EnqueueFranchiseSeasonMetricsGenerationCommandHandler>
{
    private async Task SeedTeamAsync(Sport sport, int seasonYear, Guid? groupSeasonId, string slug)
    {
        var franchise = Fixture.Build<Franchise>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Sport, sport)
            .With(x => x.Name, slug)
            .With(x => x.Abbreviation, "TM")
            .With(x => x.Location, "Test City")
            .With(x => x.DisplayName, slug)
            .With(x => x.DisplayNameShort, slug)
            .With(x => x.ColorCodeHex, "#000000")
            .With(x => x.Slug, slug)
            .Create();
        await FootballDataContext.Franchises.AddAsync(franchise);

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseId, franchise.Id)
            .With(x => x.Franchise, franchise)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.GroupSeasonId, groupSeasonId)
            .With(x => x.Slug, slug)
            .With(x => x.Location, "Test City")
            .With(x => x.Name, slug)
            .With(x => x.Abbreviation, "TM")
            .With(x => x.DisplayName, slug)
            .With(x => x.DisplayNameShort, slug)
            .With(x => x.ColorCodeHex, "#000000")
            .Create();
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
    }

    [Fact]
    public async Task WhenFranchiseSeasonsExist_EnqueuesEveryTeam_NoFbsScoping()
    {
        // The FBS gate is GONE (2026-09-10): it starved every FCS team of
        // season metrics despite their FBS matchups producing per-game
        // CompetitionMetric rows, which nulled BOTH sides of FBS-vs-FCS
        // previews via the both-or-nothing rule. This seeds an FBS-shaped
        // team (GroupSeasonId set), an FCS-shaped team (different group),
        // and a groupless team — all three must enqueue.
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<EnqueueFranchiseSeasonMetricsGenerationCommandHandler>();

        await SeedTeamAsync(Sport.FootballNcaa, 2026, Guid.NewGuid(), "fbs-team");
        await SeedTeamAsync(Sport.FootballNcaa, 2026, Guid.NewGuid(), "fcs-team");
        await SeedTeamAsync(Sport.FootballNcaa, 2026, null, "independent-team");
        await FootballDataContext.SaveChangesAsync();

        var result = await sut.ExecuteAsync(
            new EnqueueFranchiseSeasonMetricsGenerationCommand(2026, Sport.FootballNcaa),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);
        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task WhenSportIsNfl_EnqueuesAllFranchiseSeasons()
    {
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<EnqueueFranchiseSeasonMetricsGenerationCommandHandler>();

        await SeedTeamAsync(Sport.FootballNfl, 2026, null, "nfl-team-0");
        await SeedTeamAsync(Sport.FootballNfl, 2026, null, "nfl-team-1");
        await FootballDataContext.SaveChangesAsync();

        var result = await sut.ExecuteAsync(
            new EnqueueFranchiseSeasonMetricsGenerationCommand(2026, Sport.FootballNfl),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task WhenNoFranchiseSeasonsExist_ShouldReturnSuccessWithNoEnqueues()
    {
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<EnqueueFranchiseSeasonMetricsGenerationCommandHandler>();

        var result = await sut.ExecuteAsync(
            new EnqueueFranchiseSeasonMetricsGenerationCommand(2024, Sport.FootballNcaa),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);
        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenOtherSportOrSeason_IsExcluded()
    {
        // Sport + season predicates survive the FBS-gate removal.
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<EnqueueFranchiseSeasonMetricsGenerationCommandHandler>();

        await SeedTeamAsync(Sport.FootballNcaa, 2026, null, "right-team");
        await SeedTeamAsync(Sport.FootballNfl, 2026, null, "wrong-sport");
        await SeedTeamAsync(Sport.FootballNcaa, 2025, null, "wrong-season");
        await FootballDataContext.SaveChangesAsync();

        await sut.ExecuteAsync(
            new EnqueueFranchiseSeasonMetricsGenerationCommand(2026, Sport.FootballNcaa),
            CancellationToken.None);

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>>()),
            Times.Once);
    }
}
