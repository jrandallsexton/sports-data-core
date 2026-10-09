using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueScoresByWeek;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Queries.GetLeagueScoresByWeek;

public class GetLeagueScoresByWeekQueryHandlerTests : ApiTestBase<GetLeagueScoresByWeekQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_WhenLeagueNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetLeagueScoresByWeekQueryHandler>();
        var query = new GetLeagueScoresByWeekQuery { LeagueId = Guid.NewGuid() ,
            UserId = Guid.NewGuid()};

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        var failure = result as Failure<Api.Application.UI.Leagues.Dtos.LeagueScoresByWeekDto>;
        failure!.Errors.Should().ContainSingle(e => e.PropertyName == nameof(query.LeagueId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoWeekResults_ReturnsEmptyWeeksList()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetLeagueScoresByWeekQueryHandler>();
        var query = new GetLeagueScoresByWeekQuery { LeagueId = league.Id ,
            UserId = Guid.NewGuid()};

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LeagueId.Should().Be(league.Id);
        result.Value.LeagueName.Should().Be(league.Name);
        result.Value.Weeks.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenWeekResultsExist_ReturnsScoresGroupedByWeek()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);

        var user1 = CreateUser("User One");
        var user2 = CreateUser("User Two");
        DataContext.Users.AddRange(user1, user2);

        // Week 1 results
        var week1Result1 = CreateWeekResult(league.Id, user1.Id, 1, 10, 8, 12, rank: 1, isWeeklyWinner: true);
        var week1Result2 = CreateWeekResult(league.Id, user2.Id, 1, 8, 6, 12, rank: 2, isWeeklyWinner: false);

        // Week 2 results
        var week2Result1 = CreateWeekResult(league.Id, user1.Id, 2, 9, 7, 12, rank: 2, isWeeklyWinner: false);
        var week2Result2 = CreateWeekResult(league.Id, user2.Id, 2, 11, 9, 12, rank: 1, isWeeklyWinner: true);

        DataContext.PickemGroupWeekResults.AddRange(week1Result1, week1Result2, week2Result1, week2Result2);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetLeagueScoresByWeekQueryHandler>();
        var query = new GetLeagueScoresByWeekQuery { LeagueId = league.Id ,
            UserId = Guid.NewGuid()};

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LeagueId.Should().Be(league.Id);
        result.Value.LeagueName.Should().Be(league.Name);
        result.Value.Weeks.Should().HaveCount(2);

        var week1 = result.Value.Weeks.First(w => w.WeekNumber == 1);
        week1.UserScores.Should().HaveCount(2);
        week1.PickCount.Should().Be(12);

        var week2 = result.Value.Weeks.First(w => w.WeekNumber == 2);
        week2.UserScores.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCorrectUserScoreDetails()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);

        var user = CreateUser("Test User", isSynthetic: true);
        DataContext.Users.Add(user);

        var weekResult = CreateWeekResult(
            league.Id,
            user.Id,
            weekNumber: 5,
            totalPoints: 15,
            correctPicks: 12,
            totalPicks: 14,
            rank: 1,
            isWeeklyWinner: true,
            isDropWeek: true);

        DataContext.PickemGroupWeekResults.Add(weekResult);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetLeagueScoresByWeekQueryHandler>();
        var query = new GetLeagueScoresByWeekQuery { LeagueId = league.Id ,
            UserId = Guid.NewGuid()};

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Weeks.Should().HaveCount(1);

        var userScore = result.Value.Weeks[0].UserScores[0];
        userScore.UserId.Should().Be(user.Id);
        userScore.UserName.Should().Be("Test User");
        userScore.IsSynthetic.Should().BeTrue();
        userScore.WeekNumber.Should().Be(5);
        userScore.PickCount.Should().Be(14);
        userScore.Score.Should().Be(15);
        userScore.IsDropWeek.Should().BeTrue();
        userScore.IsWeeklyWinner.Should().BeTrue();
        userScore.Rank.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WeeksAreOrderedByWeekNumber()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);

        var user = CreateUser("Test User");
        DataContext.Users.Add(user);

        // Add results out of order
        var week3 = CreateWeekResult(league.Id, user.Id, 3, 10, 8, 12);
        var week1 = CreateWeekResult(league.Id, user.Id, 1, 8, 6, 12);
        var week2 = CreateWeekResult(league.Id, user.Id, 2, 9, 7, 12);

        DataContext.PickemGroupWeekResults.AddRange(week3, week1, week2);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetLeagueScoresByWeekQueryHandler>();
        var query = new GetLeagueScoresByWeekQuery { LeagueId = league.Id ,
            UserId = Guid.NewGuid()};

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Weeks.Should().HaveCount(3);
        result.Value.Weeks[0].WeekNumber.Should().Be(1);
        result.Value.Weeks[1].WeekNumber.Should().Be(2);
        result.Value.Weeks[2].WeekNumber.Should().Be(3);
    }

    #region Helper Methods

    private static PickemGroup CreateLeague(string name = "Test League")
    {
        return new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = name,
            CommissionerUserId = Guid.NewGuid(),
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            IsPublic = false,
            UseConfidencePoints = false,
            CreatedBy = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow
        };
    }

    private static UserEntity CreateUser(string displayName, bool isSynthetic = false)
    {
        return new UserEntity
        {
            Username = displayName.Replace(" ", "").ToLowerInvariant(),
            Id = Guid.NewGuid(),
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = $"{displayName.Replace(" ", "").ToLowerInvariant()}@test.com",
            DisplayName = displayName,
            SignInProvider = "test",
            IsSynthetic = isSynthetic,
            LastLoginUtc = DateTime.UtcNow
        };
    }

    private static PickemGroupWeekResult CreateWeekResult(
        Guid leagueId,
        Guid userId,
        int weekNumber,
        int totalPoints,
        int correctPicks,
        int totalPicks,
        int? rank = null,
        bool isWeeklyWinner = false,
        bool isDropWeek = false)
    {
        return new PickemGroupWeekResult
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId,
            SeasonYear = 2025,
            SeasonWeek = weekNumber,
            TotalPoints = totalPoints,
            CorrectPicks = correctPicks,
            TotalPicks = totalPicks,
            Rank = rank,
            IsWeeklyWinner = isWeeklyWinner,
            IsDropWeek = isDropWeek,
            CalculatedUtc = DateTime.UtcNow
        };
    }

    #endregion

    [Fact]
    public async Task ExecuteAsync_PlayerPickemLeague_BuildsWeeksFromLineupPoints()
    {
        // Week 5: Ann 31.5 (3 players) and Ben 31.5 (2) tie for the win and
        // share rank 1; Cal 0 (no slots) is never a weekly winner.
        var leagueId = Guid.NewGuid();
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = leagueId,
            Name = "PP",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            CommissionerUserId = Guid.NewGuid(),
            SeasonYear = 2026,
            GroupType = GroupType.PlayerPickem,
        });
        var ann = AddUser("Ann");
        var ben = AddUser("Ben");
        var cal = AddUser("Cal");
        AddLineup(leagueId, ann, 31.5m, players: 3);
        AddLineup(leagueId, ben, 31.5m, players: 2);
        AddLineup(leagueId, cal, 0m, players: 0);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetLeagueScoresByWeekQueryHandler>();
        var result = await handler.ExecuteAsync(new GetLeagueScoresByWeekQuery { LeagueId = leagueId, UserId = ann });

        result.IsSuccess.Should().BeTrue();
        result.Value.GroupType.Should().Be("PlayerPickem");
        var week = result.Value.Weeks.Should().ContainSingle().Subject;
        week.WeekNumber.Should().Be(5);

        var annScore = week.UserScores.Single(s => s.UserId == ann);
        annScore.Points.Should().Be(31.5m);
        annScore.PlayerCount.Should().Be(3);
        annScore.Rank.Should().Be(1);
        annScore.IsWeeklyWinner.Should().BeTrue();
        annScore.Score.Should().Be(32); // rounded, for clients not branching on GroupType

        var benScore = week.UserScores.Single(s => s.UserId == ben);
        benScore.Rank.Should().Be(1);
        benScore.IsWeeklyWinner.Should().BeTrue();

        var calScore = week.UserScores.Single(s => s.UserId == cal);
        calScore.Rank.Should().Be(3);
        calScore.IsWeeklyWinner.Should().BeFalse();
    }

    private Guid AddUser(string name)
    {
        var id = Guid.NewGuid();
        DataContext.Users.Add(new UserEntity
        {
            Id = id,
            FirebaseUid = $"fb-{id:N}",
            Email = $"{name}@x.com",
            SignInProvider = "password",
            DisplayName = name,
            Username = name.ToLowerInvariant(),
        });
        return id;
    }

    private void AddLineup(Guid leagueId, Guid userId, decimal total, int players)
    {
        var lineup = new PlayerLineup
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId,
            SeasonYear = 2026,
            SeasonWeek = 5,
            TotalPoints = total,
            CreatedBy = userId,
        };
        for (var i = 0; i < players; i++)
        {
            lineup.Slots.Add(new PlayerLineupSlot
            {
                Id = Guid.NewGuid(),
                PlayerLineupId = lineup.Id,
                SlotId = $"S{i}",
                AthleteId = Guid.NewGuid(),
                AthleteSeasonId = Guid.NewGuid(),
                Position = "QB",
                FirstName = "F",
                LastName = "L",
                TeamName = "T",
                TeamSlug = "t",
                CreatedBy = userId,
            });
        }
        DataContext.PlayerLineups.Add(lineup);
    }
}
