using AutoFixture;

using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonMetricsBySeasonYear;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasons.Queries;

public class GetFranchiseSeasonMetricsBySeasonYearQueryHandlerTests :
    ProducerTestBase<GetFranchiseSeasonMetricsBySeasonYearQueryHandler>
{
    [Fact]
    public async Task WhenMetricsExist_ShouldReturnSuccessWithMetrics()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetFranchiseSeasonMetricsBySeasonYearQueryHandler>();
        var seasonYear = 2024;

        var franchise = CreateFranchise("Test Team", "test-team");
        await FootballDataContext.Franchises.AddAsync(franchise);

        var franchiseSeason = CreateFranchiseSeason(franchise, seasonYear);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        var metric = Fixture.Build<FranchiseSeasonMetric>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseSeasonId, franchiseSeason.Id)
            .With(x => x.FranchiseSeason, franchiseSeason)
            .With(x => x.Season, seasonYear)
            .With(x => x.GamesPlayed, 10)
            .With(x => x.Ypp, 5.5m)
            .With(x => x.SuccessRate, 0.45m)
            .Create();

        await FootballDataContext.FranchiseSeasonMetrics.AddAsync(metric);
        await FootballDataContext.SaveChangesAsync();

        var query = new GetFranchiseSeasonMetricsBySeasonYearQuery(seasonYear);

        // Act
        var result = await sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<List<Core.Dtos.Canonical.FranchiseSeasonMetricsDto>>>();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].FranchiseSlug.Should().Be("test-team");
        result.Value[0].GamesPlayed.Should().Be(10);
    }

    [Fact]
    public async Task WhenNoMetricsExist_ShouldReturnSuccessWithEmptyList()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetFranchiseSeasonMetricsBySeasonYearQueryHandler>();
        var query = new GetFranchiseSeasonMetricsBySeasonYearQuery(2024);

        // Act
        var result = await sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<List<Core.Dtos.Canonical.FranchiseSeasonMetricsDto>>>();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenMultipleMetricsExist_ShouldReturnAll()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetFranchiseSeasonMetricsBySeasonYearQueryHandler>();
        var seasonYear = 2024;

        for (int i = 0; i < 3; i++)
        {
            var franchise = CreateFranchise($"Team {i}", $"team-{i}");
            await FootballDataContext.Franchises.AddAsync(franchise);

            var franchiseSeason = CreateFranchiseSeason(franchise, seasonYear);
            await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

            var metric = Fixture.Build<FranchiseSeasonMetric>()
                .OmitAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.FranchiseSeasonId, franchiseSeason.Id)
                .With(x => x.FranchiseSeason, franchiseSeason)
                .With(x => x.Season, seasonYear)
                .Create();

            await FootballDataContext.FranchiseSeasonMetrics.AddAsync(metric);
        }

        await FootballDataContext.SaveChangesAsync();

        var query = new GetFranchiseSeasonMetricsBySeasonYearQuery(seasonYear);

        // Act
        var result = await sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }

    private Franchise CreateFranchise(string name, string slug)
    {
        return Fixture.Build<Franchise>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Name, name)
            .With(x => x.Abbreviation, "TM")
            .With(x => x.DisplayName, name)
            .With(x => x.DisplayNameShort, name)
            .With(x => x.Location, "City")
            .With(x => x.ColorCodeHex, "#000000")
            .With(x => x.Slug, slug)
            .Create();
    }

    private FranchiseSeason CreateFranchiseSeason(Franchise franchise, int seasonYear)
    {
        return Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseId, franchise.Id)
            .With(x => x.Franchise, franchise)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.Slug, franchise.Slug)
            .With(x => x.Location, franchise.Location)
            .With(x => x.Name, franchise.Name)
            .With(x => x.Abbreviation, franchise.Abbreviation ?? "TM")
            .With(x => x.DisplayName, franchise.DisplayName)
            .With(x => x.DisplayNameShort, franchise.DisplayNameShort)
            .With(x => x.ColorCodeHex, franchise.ColorCodeHex)
            .Create();
    }
}
