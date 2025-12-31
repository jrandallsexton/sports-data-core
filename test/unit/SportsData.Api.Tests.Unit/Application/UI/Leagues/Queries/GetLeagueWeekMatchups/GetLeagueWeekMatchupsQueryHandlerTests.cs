using FluentAssertions;

using Moq;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using League = SportsData.Api.Application.League;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;

public class GetLeagueWeekMatchupsQueryHandlerTests : ApiTestBase<GetLeagueWeekMatchupsQueryHandler>
{
    private readonly Mock<IProvideCanonicalData> _canonicalDataProviderMock;

    public GetLeagueWeekMatchupsQueryHandlerTests()
    {
        _canonicalDataProviderMock = Mocker.GetMock<IProvideCanonicalData>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenLeagueNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetLeagueWeekMatchupsQueryHandler>();
        var query = new GetLeagueWeekMatchupsQuery
        {
            UserId = Guid.NewGuid(),
            LeagueId = Guid.NewGuid(),
            Week = 1
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        var failure = result as Failure<LeagueWeekMatchupsDto>;
        failure!.Errors.Should().ContainSingle(e => e.PropertyName == nameof(query.LeagueId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoMatchupsInWeek_ReturnsEmptyMatchups()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();

        _canonicalDataProviderMock
            .Setup(x => x.GetMatchupsByContestIds(It.IsAny<List<Guid>>()))
            .ReturnsAsync([]);

        var handler = Mocker.CreateInstance<GetLeagueWeekMatchupsQueryHandler>();
        var query = new GetLeagueWeekMatchupsQuery
        {
            UserId = Guid.NewGuid(),
            LeagueId = league.Id,
            Week = 1
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Matchups.Should().BeEmpty();
        result.Value.PickType.Should().Be(league.PickType);
        result.Value.UseConfidencePoints.Should().Be(league.UseConfidencePoints);
        result.Value.WeekNumber.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMatchupsExist_ReturnsMatchupsOrderedByStartDate()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);

        var contestId1 = Guid.NewGuid();
        var contestId2 = Guid.NewGuid();

        var matchup1 = CreateMatchup(league.Id, contestId1, weekNumber: 5);
        matchup1.StartDateUtc = DateTime.UtcNow.AddHours(2);
        var matchup2 = CreateMatchup(league.Id, contestId2, weekNumber: 5);
        matchup2.StartDateUtc = DateTime.UtcNow.AddHours(1); // Earlier

        DataContext.PickemGroupMatchups.AddRange(matchup1, matchup2);
        await DataContext.SaveChangesAsync();

        var canonicalMatchup1 = CreateCanonicalMatchup(contestId1);
        var canonicalMatchup2 = CreateCanonicalMatchup(contestId2);

        _canonicalDataProviderMock
            .Setup(x => x.GetMatchupsByContestIds(It.IsAny<List<Guid>>()))
            .ReturnsAsync([canonicalMatchup1, canonicalMatchup2]);

        var handler = Mocker.CreateInstance<GetLeagueWeekMatchupsQueryHandler>();
        var query = new GetLeagueWeekMatchupsQuery
        {
            UserId = Guid.NewGuid(),
            LeagueId = league.Id,
            Week = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Matchups.Should().HaveCount(2);
        // Should be ordered by start date (matchup2 first since it starts earlier)
        result.Value.Matchups[0].ContestId.Should().Be(contestId2);
        result.Value.Matchups[1].ContestId.Should().Be(contestId1);
    }

    [Fact]
    public async Task ExecuteAsync_EnrichesMatchupsWithCanonicalData()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);

        var contestId = Guid.NewGuid();
        var matchup = CreateMatchup(league.Id, contestId, weekNumber: 5);
        DataContext.PickemGroupMatchups.Add(matchup);
        await DataContext.SaveChangesAsync();

        var canonicalMatchup = CreateCanonicalMatchup(contestId);
        canonicalMatchup.Away = "Alabama";
        canonicalMatchup.AwayShort = "ALA";
        canonicalMatchup.Home = "Georgia";
        canonicalMatchup.HomeShort = "UGA";
        canonicalMatchup.SpreadCurrent = -7.5m;
        canonicalMatchup.OverUnderCurrent = 52.5m;

        _canonicalDataProviderMock
            .Setup(x => x.GetMatchupsByContestIds(It.IsAny<List<Guid>>()))
            .ReturnsAsync([canonicalMatchup]);

        var handler = Mocker.CreateInstance<GetLeagueWeekMatchupsQueryHandler>();
        var query = new GetLeagueWeekMatchupsQuery
        {
            UserId = Guid.NewGuid(),
            LeagueId = league.Id,
            Week = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Matchups.Should().HaveCount(1);

        var enrichedMatchup = result.Value.Matchups[0];
        enrichedMatchup.Away.Should().Be("Alabama");
        enrichedMatchup.AwayShort.Should().Be("ALA");
        enrichedMatchup.Home.Should().Be("Georgia");
        enrichedMatchup.HomeShort.Should().Be("UGA");
        enrichedMatchup.SpreadCurrent.Should().Be(-7.5m);
        enrichedMatchup.OverUnderCurrent.Should().Be(52.5m);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoCanonicalMatchupsReturned_StillReturnsMatchups()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);

        var contestId = Guid.NewGuid();
        var matchup = CreateMatchup(league.Id, contestId, weekNumber: 5);
        DataContext.PickemGroupMatchups.Add(matchup);
        await DataContext.SaveChangesAsync();

        _canonicalDataProviderMock
            .Setup(x => x.GetMatchupsByContestIds(It.IsAny<List<Guid>>()))
            .ReturnsAsync([]);

        var handler = Mocker.CreateInstance<GetLeagueWeekMatchupsQueryHandler>();
        var query = new GetLeagueWeekMatchupsQuery
        {
            UserId = Guid.NewGuid(),
            LeagueId = league.Id,
            Week = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Matchups.Should().HaveCount(1);
        result.Value.Matchups[0].ContestId.Should().Be(contestId);
    }

    [Fact]
    public async Task ExecuteAsync_IncludesContestPredictions()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);

        var contestId = Guid.NewGuid();
        var matchup = CreateMatchup(league.Id, contestId, weekNumber: 5);
        DataContext.PickemGroupMatchups.Add(matchup);

        var prediction = new ContestPrediction
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            ModelVersion = "v1.0",
            PredictionType = PickType.AgainstTheSpread,
            WinProbability = 0.65m,
            WinnerFranchiseSeasonId = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow
        };
        DataContext.ContestPredictions.Add(prediction);
        await DataContext.SaveChangesAsync();

        var canonicalMatchup = CreateCanonicalMatchup(contestId);
        _canonicalDataProviderMock
            .Setup(x => x.GetMatchupsByContestIds(It.IsAny<List<Guid>>()))
            .ReturnsAsync([canonicalMatchup]);

        var handler = Mocker.CreateInstance<GetLeagueWeekMatchupsQueryHandler>();
        var query = new GetLeagueWeekMatchupsQuery
        {
            UserId = Guid.NewGuid(),
            LeagueId = league.Id,
            Week = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Matchups.Should().HaveCount(1);
        result.Value.Matchups[0].Predictions.Should().HaveCount(1);
        result.Value.Matchups[0].Predictions[0].ModelVersion.Should().Be("v1.0");
        result.Value.Matchups[0].Predictions[0].WinProbability.Should().Be(0.65m);
    }

    [Fact]
    public async Task ExecuteAsync_SetsPreviewFlagsCorrectly()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);

        var contestId = Guid.NewGuid();
        var matchup = CreateMatchup(league.Id, contestId, weekNumber: 5);
        DataContext.PickemGroupMatchups.Add(matchup);

        var preview = new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            PredictedStraightUpWinner = Guid.NewGuid(),
            ApprovedUtc = DateTime.UtcNow,
            RejectedUtc = null,
            CreatedBy = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow
        };
        DataContext.MatchupPreviews.Add(preview);
        await DataContext.SaveChangesAsync();

        var canonicalMatchup = CreateCanonicalMatchup(contestId);
        _canonicalDataProviderMock
            .Setup(x => x.GetMatchupsByContestIds(It.IsAny<List<Guid>>()))
            .ReturnsAsync([canonicalMatchup]);

        var handler = Mocker.CreateInstance<GetLeagueWeekMatchupsQueryHandler>();
        var query = new GetLeagueWeekMatchupsQuery
        {
            UserId = Guid.NewGuid(),
            LeagueId = league.Id,
            Week = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Matchups.Should().HaveCount(1);
        result.Value.Matchups[0].IsPreviewAvailable.Should().BeTrue();
        result.Value.Matchups[0].IsPreviewReviewed.Should().BeTrue();
    }

    #region Helper Methods

    private static PickemGroup CreateLeague(string name = "Test League")
    {
        var commissionerId = Guid.NewGuid();
        return new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = name,
            CommissionerUserId = commissionerId,
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            IsPublic = false,
            UseConfidencePoints = false,
            CreatedBy = commissionerId,
            CreatedUtc = DateTime.UtcNow,
            Members = []
        };
    }

    private static PickemGroupMatchup CreateMatchup(Guid groupId, Guid contestId, int weekNumber)
    {
        return new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            SeasonWeekId = Guid.NewGuid(),
            SeasonYear = 2025,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow.AddDays(1),
            CreatedBy = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow
        };
    }

    private static LeagueWeekMatchupsDto.MatchupForPickDto CreateCanonicalMatchup(Guid contestId)
    {
        return new LeagueWeekMatchupsDto.MatchupForPickDto
        {
            ContestId = contestId,
            StartDateUtc = DateTime.UtcNow.AddDays(1),
            Away = "Away Team",
            AwayShort = "AWAY",
            AwaySlug = "away-team",
            AwayFranchiseSeasonId = Guid.NewGuid(),
            AwayLogoUri = "https://example.com/away.png",
            AwayColor = "#FF0000",
            Home = "Home Team",
            HomeShort = "HOME",
            HomeSlug = "home-team",
            HomeFranchiseSeasonId = Guid.NewGuid(),
            HomeLogoUri = "https://example.com/home.png",
            HomeColor = "#0000FF",
            Venue = "Test Stadium",
            VenueCity = "Test City",
            VenueState = "TS"
        };
    }

    #endregion
}
