using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.Seasons.Queries.GetSeasonOverview;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Seasons.Queries.GetSeasonOverview;

public class GetSeasonOverviewQueryHandlerTests : ProducerTestBase<GetSeasonOverviewQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnSeasonData_WhenSeasonExists()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var seasonYear = 2024;

        var season = new Season
        {
            Id = seasonId,
            Year = seasonYear,
            Name = "2024 College Football",
            StartDate = new DateTime(2024, 8, 24, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var seasonPhaseId = Guid.NewGuid();

        var week1 = new SeasonWeek
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            SeasonPhaseId = seasonPhaseId,
            Number = 1,
            StartDate = new DateTime(2024, 8, 24, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var week2 = new SeasonWeek
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            SeasonPhaseId = seasonPhaseId,
            Number = 2,
            StartDate = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 9, 7, 0, 0, 0, DateTimeKind.Utc),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var poll = new SeasonPoll
        {
            Id = Guid.NewGuid(),
            Name = "AP Top 25",
            ShortName = "AP",
            Slug = "ap-top-25",
            SeasonYear = seasonYear,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await TeamSportDataContext.Seasons.AddAsync(season);
        await TeamSportDataContext.SeasonWeeks.AddRangeAsync(week1, week2);
        await TeamSportDataContext.SeasonPolls.AddAsync(poll);
        await TeamSportDataContext.SaveChangesAsync();
        TeamSportDataContext.ChangeTracker.Clear();

        var handler = Mocker.CreateInstance<GetSeasonOverviewQueryHandler>();
        var query = new GetSeasonOverviewQuery(seasonYear);

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SeasonYear.Should().Be(seasonYear);
        result.Value.Name.Should().Be("2024 College Football");
        result.Value.Weeks.Should().HaveCount(2);
        result.Value.Weeks[0].Number.Should().Be(1);
        result.Value.Weeks[0].Label.Should().Be("Week 1");
        result.Value.Weeks[1].Number.Should().Be(2);
        result.Value.Weeks[1].Label.Should().Be("Week 2");
        result.Value.Polls.Should().HaveCount(1);
        result.Value.Polls[0].Name.Should().Be("AP Top 25");
        result.Value.Polls[0].ShortName.Should().Be("AP");
        result.Value.Polls[0].Slug.Should().Be("ap-top-25");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenSeasonDoesNotExist()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetSeasonOverviewQueryHandler>();
        var query = new GetSeasonOverviewQuery(1999);

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        var failure = result as Failure<SeasonOverviewDto>;
        failure!.Errors.Should().ContainSingle(e =>
            e.PropertyName == "SeasonYear" &&
            e.ErrorMessage.Contains("Season with year 1999 not found"));
    }
}
