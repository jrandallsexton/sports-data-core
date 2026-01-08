using AutoFixture;

using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonMetricsById;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasons.Queries;

public class GetFranchiseSeasonMetricsByIdQueryHandlerTests :
    ProducerTestBase<GetFranchiseSeasonMetricsByIdQueryHandler>
{
    [Fact]
    public async Task WhenMetricExists_ShouldReturnSuccessWithMetric()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetFranchiseSeasonMetricsByIdQueryHandler>();

        var franchise = CreateFranchise("Test Team", "test-team");
        await FootballDataContext.Franchises.AddAsync(franchise);

        var franchiseSeasonId = Guid.NewGuid();
        var franchiseSeason = CreateFranchiseSeason(franchise, 2024, franchiseSeasonId);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        var metric = Fixture.Build<FranchiseSeasonMetric>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseSeasonId, franchiseSeasonId)
            .With(x => x.FranchiseSeason, franchiseSeason)
            .With(x => x.Season, 2024)
            .With(x => x.GamesPlayed, 12)
            .With(x => x.Ypp, 6.2m)
            .Create();

        await FootballDataContext.FranchiseSeasonMetrics.AddAsync(metric);
        await FootballDataContext.SaveChangesAsync();

        var query = new GetFranchiseSeasonMetricsByIdQuery(franchiseSeasonId);

        // Act
        var result = await sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<FranchiseSeasonMetricsDto>>();
        result.IsSuccess.Should().BeTrue();
        result.Value.FranchiseSlug.Should().Be("test-team");
        result.Value.GamesPlayed.Should().Be(12);
        result.Value.SeasonYear.Should().Be(2024);
    }

    [Fact]
    public async Task WhenMetricDoesNotExist_ShouldReturnFailureNotFound()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetFranchiseSeasonMetricsByIdQueryHandler>();
        var nonExistentId = Guid.NewGuid();
        var query = new GetFranchiseSeasonMetricsByIdQuery(nonExistentId);

        // Act
        var result = await sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Failure<FranchiseSeasonMetricsDto>>();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
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

    private FranchiseSeason CreateFranchiseSeason(Franchise franchise, int seasonYear, Guid? id = null)
    {
        return Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, id ?? Guid.NewGuid())
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
