using AutoFixture;

using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetCurrentPolls;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasonRankings.Queries.GetCurrentPolls;

public class GetCurrentPollsQueryHandlerTests : ProducerTestBase<GetCurrentPollsQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenPollsExist()
    {
        // Arrange
        var seasonYear = 2024;
        var seasonWeekId = Guid.NewGuid();
        
        var seasonWeek = Fixture.Build<SeasonWeek>()
            .WithAutoProperties()
            .With(x => x.Id, seasonWeekId)
            .With(x => x.Number, 14)
            .Create();

        var franchise = Fixture.Build<Franchise>()
            .WithAutoProperties()
            .With(x => x.DisplayNameShort, "Oregon")
            .With(x => x.Slug, "oregon")
            .Create();

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.FranchiseId, franchise.Id)
            .With(x => x.Franchise, franchise)
            .With(x => x.Wins, 13)
            .With(x => x.Losses, 0)
            .Create();

        var logo = Fixture.Build<FranchiseSeasonLogo>()
            .WithAutoProperties()
            .With(x => x.FranchiseSeasonId, franchiseSeason.Id)
            .With(x => x.Uri, new Uri("https://example.com/logo.png"))
            .Create();

        franchiseSeason.Logos = new List<FranchiseSeasonLogo> { logo };

        var ranking = Fixture.Build<FranchiseSeasonRanking>()
            .WithAutoProperties()
            .With(x => x.FranchiseSeasonId, franchiseSeason.Id)
            .With(x => x.FranchiseSeason, franchiseSeason)
            .With(x => x.FranchiseId, franchise.Id)
            .With(x => x.Franchise, franchise)
            .With(x => x.SeasonWeekId, seasonWeekId)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.Type, "cfp")
            .With(x => x.ShortHeadline, "CFP Rankings")
            .With(x => x.Date, DateTime.UtcNow.AddDays(-1))
            .With(x => x.Rank, Fixture.Build<FranchiseSeasonRankingDetail>()
                .WithAutoProperties()
                .With(r => r.Current, 1)
                .With(r => r.Previous, 1)
                .With(r => r.Points, 1625)
                .With(r => r.FirstPlaceVotes, 62)
                .With(r => r.Trend, "0")
                .Create())
            .Create();

        await TeamSportDataContext.SeasonWeeks.AddAsync(seasonWeek);
        await TeamSportDataContext.Franchises.AddAsync(franchise);
        await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await TeamSportDataContext.FranchiseSeasonLogos.AddAsync(logo);
        await TeamSportDataContext.FranchiseSeasonRankings.AddAsync(ranking);
        await TeamSportDataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetCurrentPollsQueryHandler>();
        var query = new GetCurrentPollsQuery { SeasonYear = seasonYear };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].PollId.Should().Be("cfp");
        result.Value[0].PollName.Should().Be("CFP Rankings");
        result.Value[0].SeasonYear.Should().Be(seasonYear);
        result.Value[0].Week.Should().Be(14);
        result.Value[0].Entries.Should().HaveCount(1);
        result.Value[0].Entries[0].Rank.Should().Be(1);
        result.Value[0].Entries[0].FranchiseName.Should().Be("Oregon");
        result.Value[0].Entries[0].Wins.Should().Be(13);
        result.Value[0].Entries[0].Losses.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenNoPollsExist()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetCurrentPollsQueryHandler>();
        var query = new GetCurrentPollsQuery { SeasonYear = 2020 };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        var failure = result as Failure<List<FranchiseSeasonPollDto>>;
        failure!.Errors.Should().ContainSingle(e => 
            e.PropertyName == "seasonYear" && 
            e.ErrorMessage.Contains("No polls found for season year 2020"));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnMultiplePolls_WhenAllPollsExist()
    {
        // Arrange
        var seasonYear = 2024;
        var seasonWeekId = Guid.NewGuid();
        
        var seasonWeek = Fixture.Build<SeasonWeek>()
            .WithAutoProperties()
            .With(x => x.Id, seasonWeekId)
            .With(x => x.Number, 14)
            .Create();

        var franchise = Fixture.Build<Franchise>()
            .WithAutoProperties()
            .With(x => x.DisplayNameShort, "Oregon")
            .With(x => x.Slug, "oregon")
            .Create();

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.FranchiseId, franchise.Id)
            .With(x => x.Franchise, franchise)
            .With(x => x.Wins, 13)
            .With(x => x.Losses, 0)
            .Create();

        var logo = Fixture.Build<FranchiseSeasonLogo>()
            .WithAutoProperties()
            .With(x => x.FranchiseSeasonId, franchiseSeason.Id)
            .With(x => x.Uri, new Uri("https://example.com/logo.png"))
            .Create();

        franchiseSeason.Logos = new List<FranchiseSeasonLogo> { logo };

        // Create all three polls: cfp, ap, usa
        var polls = new[] { "cfp", "ap", "usa" };
        var rankings = polls.Select(pollType =>
            Fixture.Build<FranchiseSeasonRanking>()
                .WithAutoProperties()
                .With(x => x.FranchiseSeasonId, franchiseSeason.Id)
                .With(x => x.FranchiseSeason, franchiseSeason)
                .With(x => x.FranchiseId, franchise.Id)
                .With(x => x.Franchise, franchise)
                .With(x => x.SeasonWeekId, seasonWeekId)
                .With(x => x.SeasonYear, seasonYear)
                .With(x => x.Type, pollType)
                .With(x => x.ShortHeadline, $"{pollType.ToUpper()} Rankings")
                .With(x => x.Date, DateTime.UtcNow.AddDays(-1))
                .With(x => x.Rank, Fixture.Build<FranchiseSeasonRankingDetail>()
                    .WithAutoProperties()
                    .With(r => r.Current, 1)
                    .Create())
                .Create()
        ).ToList();

        await TeamSportDataContext.SeasonWeeks.AddAsync(seasonWeek);
        await TeamSportDataContext.Franchises.AddAsync(franchise);
        await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await TeamSportDataContext.FranchiseSeasonLogos.AddAsync(logo);
        await TeamSportDataContext.FranchiseSeasonRankings.AddRangeAsync(rankings);
        await TeamSportDataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetCurrentPollsQueryHandler>();
        var query = new GetCurrentPollsQuery { SeasonYear = seasonYear };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Should().Contain(p => p.PollId == "cfp");
        result.Value.Should().Contain(p => p.PollId == "ap");
        result.Value.Should().Contain(p => p.PollId == "usa");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenExceptionThrown()
    {
        // Arrange - Force an exception by disposing the context
        await TeamSportDataContext.DisposeAsync();

        var handler = Mocker.CreateInstance<GetCurrentPollsQueryHandler>();
        var query = new GetCurrentPollsQuery { SeasonYear = 2024 };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        var failure = result as Failure<List<FranchiseSeasonPollDto>>;
        failure!.Errors.Should().ContainSingle(e => e.PropertyName == "Error");
    }
}
