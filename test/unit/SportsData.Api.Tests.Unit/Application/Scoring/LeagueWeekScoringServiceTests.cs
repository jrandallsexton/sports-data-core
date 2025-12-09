using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

using SportsData.Api.Application;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

/// <summary>
/// Tests for LeagueWeekScoringService.
/// Validates league scoring, winner determination, drop week calculation, and rankings.
/// </summary>
public class LeagueWeekScoringServiceTests : ApiTestBase<LeagueWeekScoringService>
{
    private readonly Mock<ILogger<LeagueWeekScoringService>> _loggerMock;

    public LeagueWeekScoringServiceTests()
    {
        _loggerMock = new Mock<ILogger<LeagueWeekScoringService>>();
        Mocker.Use(_loggerMock.Object);
    }

    [Fact]
    public async Task ScoreLeagueWeekAsync_CreatesWeekResults_WhenPicksExist()
    {
        // Arrange
        var leagueId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var seasonYear = 2024;
        var weekNumber = 1;

        var league = new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = userId1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty,
            Members = new List<PickemGroupMember>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId1,
                    Role = LeagueRole.Commissioner,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId2,
                    Role = LeagueRole.Member,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                }
            }
        };

        var contestId1 = Guid.NewGuid();
        var contestId2 = Guid.NewGuid();

        var matchup1 = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId1,
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        var matchup2 = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId2,
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        // User1: 2 picks, both correct = 2 points
        var pick1 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId1,
            ContestId = contestId1,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 1,
            IsCorrect = true,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = userId1
        };

        var pick2 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId1,
            ContestId = contestId2,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 1,
            IsCorrect = true,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = userId1
        };

        // User2: 2 picks, 1 correct = 1 point
        var pick3 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId2,
            ContestId = contestId1,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 1,
            IsCorrect = true,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = userId2
        };

        var pick4 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId2,
            ContestId = contestId2,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 0,
            IsCorrect = false,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = userId2
        };

        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.PickemGroupMatchups.AddRangeAsync(matchup1, matchup2);
        await DataContext.UserPicks.AddRangeAsync(pick1, pick2, pick3, pick4);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<LeagueWeekScoringService>();

        // Act
        await sut.ScoreLeagueWeekAsync(leagueId, seasonYear, weekNumber);

        // Assert
        var results = DataContext.PickemGroupWeekResults
            .Where(r => r.PickemGroupId == leagueId && r.SeasonWeek == weekNumber)
            .OrderByDescending(r => r.TotalPoints)
            .ToList();

        results.Should().HaveCount(2);

        // User1 should be the winner with 2 points
        var user1Result = results.First(r => r.UserId == userId1);
        user1Result.TotalPoints.Should().Be(2);
        user1Result.CorrectPicks.Should().Be(2);
        user1Result.TotalPicks.Should().Be(2);
        user1Result.IsWeeklyWinner.Should().BeTrue();
        user1Result.Rank.Should().Be(1);

        // User2 should be second with 1 point
        var user2Result = results.First(r => r.UserId == userId2);
        user2Result.TotalPoints.Should().Be(1);
        user2Result.CorrectPicks.Should().Be(1);
        user2Result.TotalPicks.Should().Be(2);
        user2Result.IsWeeklyWinner.Should().BeFalse();
        user2Result.Rank.Should().Be(2);
    }

    [Fact]
    public async Task ScoreLeagueWeekAsync_HandlesMultipleWinners_WhenTied()
    {
        // Arrange
        var leagueId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var userId3 = Guid.NewGuid();
        var seasonYear = 2024;
        var weekNumber = 1;

        var league = new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None, // No tiebreaker - allow multiple winners
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = userId1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty,
            Members = new List<PickemGroupMember>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId1,
                    Role = LeagueRole.Commissioner,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId2,
                    Role = LeagueRole.Member,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId3,
                    Role = LeagueRole.Member,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                }
            }
        };

        var contestId = Guid.NewGuid();

        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId,
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        // All three users get 1 point - three-way tie
        var pick1 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId1,
            ContestId = contestId,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 1,
            IsCorrect = true,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = userId1
        };

        var pick2 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId2,
            ContestId = contestId,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 1,
            IsCorrect = true,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = userId2
        };

        var pick3 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId3,
            ContestId = contestId,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 1,
            IsCorrect = true,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = userId3
        };

        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.UserPicks.AddRangeAsync(pick1, pick2, pick3);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<LeagueWeekScoringService>();

        // Act
        await sut.ScoreLeagueWeekAsync(leagueId, seasonYear, weekNumber);

        // Assert
        var results = DataContext.PickemGroupWeekResults
            .Where(r => r.PickemGroupId == leagueId && r.SeasonWeek == weekNumber)
            .ToList();

        results.Should().HaveCount(3);

        // All three should be marked as winners when TiebreakerType is None
        results.Should().OnlyContain(r => r.IsWeeklyWinner);
        results.Should().OnlyContain(r => r.Rank == 1);
        results.Should().OnlyContain(r => r.TotalPoints == 1);
    }

    [Fact]
    public async Task ScoreLeagueWeekAsync_UsesEarliestSubmission_WhenTiedWithEarliestSubmissionTiebreaker()
    {
        // Arrange
        var leagueId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var userId3 = Guid.NewGuid();
        var seasonYear = 2024;
        var weekNumber = 1;

        var league = new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.EarliestSubmission, // Use earliest submission to break ties
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = userId1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty,
            Members = new List<PickemGroupMember>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId1,
                    Role = LeagueRole.Commissioner,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId2,
                    Role = LeagueRole.Member,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId3,
                    Role = LeagueRole.Member,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                }
            }
        };

        var contestId = Guid.NewGuid();

        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId,
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        var now = DateTime.UtcNow;

        // All three users get 1 point, but user2 submitted first
        var pick1 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId1,
            ContestId = contestId,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 1,
            IsCorrect = true,
            CreatedUtc = now.AddMinutes(-5), // Third to submit
            CreatedBy = userId1
        };

        var pick2 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId2,
            ContestId = contestId,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 1,
            IsCorrect = true,
            CreatedUtc = now.AddMinutes(-10), // First to submit - should win
            CreatedBy = userId2
        };

        var pick3 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId3,
            ContestId = contestId,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 1,
            IsCorrect = true,
            CreatedUtc = now.AddMinutes(-7), // Second to submit
            CreatedBy = userId3
        };

        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.UserPicks.AddRangeAsync(pick1, pick2, pick3);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<LeagueWeekScoringService>();

        // Act
        await sut.ScoreLeagueWeekAsync(leagueId, seasonYear, weekNumber);

        // Assert
        var results = DataContext.PickemGroupWeekResults
            .Where(r => r.PickemGroupId == leagueId && r.SeasonWeek == weekNumber)
            .ToList();

        results.Should().HaveCount(3);

        // Only user2 should be marked as winner (earliest submission)
        var user1Result = results.First(r => r.UserId == userId1);
        user1Result.IsWeeklyWinner.Should().BeFalse();
        user1Result.Rank.Should().Be(1); // Still rank 1 because tied on points
        user1Result.TotalPoints.Should().Be(1);

        var user2Result = results.First(r => r.UserId == userId2);
        user2Result.IsWeeklyWinner.Should().BeTrue(); // Winner by earliest submission
        user2Result.Rank.Should().Be(1);
        user2Result.TotalPoints.Should().Be(1);

        var user3Result = results.First(r => r.UserId == userId3);
        user3Result.IsWeeklyWinner.Should().BeFalse();
        user3Result.Rank.Should().Be(1); // Still rank 1 because tied on points
        user3Result.TotalPoints.Should().Be(1);
    }

    [Fact]
    public async Task ScoreLeagueWeekAsync_IncludesUsersWithNoPicks_WithZeroScore()
    {
        // Arrange
        var leagueId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var seasonYear = 2024;
        var weekNumber = 1;

        var league = new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = userId1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty,
            Members = new List<PickemGroupMember>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId1,
                    Role = LeagueRole.Commissioner,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId2,
                    Role = LeagueRole.Member,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                }
            }
        };

        var contestId = Guid.NewGuid();

        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId,
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        // Only user1 makes a pick, user2 makes no picks
        var pick1 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId1,
            ContestId = contestId,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 1,
            IsCorrect = true,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = userId1
        };

        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.UserPicks.AddAsync(pick1);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<LeagueWeekScoringService>();

        // Act
        await sut.ScoreLeagueWeekAsync(leagueId, seasonYear, weekNumber);

        // Assert
        var results = DataContext.PickemGroupWeekResults
            .Where(r => r.PickemGroupId == leagueId && r.SeasonWeek == weekNumber)
            .OrderByDescending(r => r.TotalPoints)
            .ToList();

        results.Should().HaveCount(2);

        // User1 should be the winner with 1 point
        var user1Result = results.First(r => r.UserId == userId1);
        user1Result.TotalPoints.Should().Be(1);
        user1Result.TotalPicks.Should().Be(1);
        user1Result.IsWeeklyWinner.Should().BeTrue();

        // User2 should have 0 points and 0 picks
        var user2Result = results.First(r => r.UserId == userId2);
        user2Result.TotalPoints.Should().Be(0);
        user2Result.TotalPicks.Should().Be(0);
        user2Result.CorrectPicks.Should().Be(0);
        user2Result.IsWeeklyWinner.Should().BeFalse();
    }

    [Fact]
    public async Task ScoreLeagueWeekAsync_CalculatesDropWeeks_WhenConfigured()
    {
        // Arrange
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seasonYear = 2024;

        var league = new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = userId,
            DropLowWeeksCount = 1, // Drop 1 lowest week
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty,
            Members = new List<PickemGroupMember>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId,
                    Role = LeagueRole.Commissioner,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                }
            }
        };

        // Create 3 weeks of matchups and picks
        for (int week = 1; week <= 3; week++)
        {
            var contestId = Guid.NewGuid();
            var matchup = new PickemGroupMatchup
            {
                Id = Guid.NewGuid(),
                GroupId = leagueId,
                SeasonWeekId = Guid.NewGuid(),
                ContestId = contestId,
                SeasonYear = seasonYear,
                SeasonWeek = week,
                StartDateUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.Empty
            };

            await DataContext.PickemGroupMatchups.AddAsync(matchup);

            // Week 1: 3 points, Week 2: 1 point, Week 3: 2 points
            // Week 2 should be the drop week
            var points = week switch
            {
                1 => 3,
                2 => 1,
                3 => 2,
                _ => 0
            };

            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                PickemGroupId = leagueId,
                UserId = userId,
                ContestId = contestId,
                PickType = PickType.StraightUp,
                TiebreakerType = TiebreakerType.None,
                Week = week,
                PointsAwarded = points,
                IsCorrect = true,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = userId
            };

            await DataContext.UserPicks.AddAsync(pick);
        }

        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<LeagueWeekScoringService>();

        // Act - Score all weeks
        for (int week = 1; week <= 3; week++)
        {
            await sut.ScoreLeagueWeekAsync(leagueId, seasonYear, week);
        }

        // Assert
        var results = DataContext.PickemGroupWeekResults
            .Where(r => r.PickemGroupId == leagueId)
            .OrderBy(r => r.SeasonWeek)
            .ToList();

        results.Should().HaveCount(3);

        // Week 1: 3 points - NOT a drop week
        results.First(r => r.SeasonWeek == 1).IsDropWeek.Should().BeFalse();
        results.First(r => r.SeasonWeek == 1).TotalPoints.Should().Be(3);

        // Week 2: 1 point - SHOULD be the drop week
        results.First(r => r.SeasonWeek == 2).IsDropWeek.Should().BeTrue();
        results.First(r => r.SeasonWeek == 2).TotalPoints.Should().Be(1);

        // Week 3: 2 points - NOT a drop week
        results.First(r => r.SeasonWeek == 3).IsDropWeek.Should().BeFalse();
        results.First(r => r.SeasonWeek == 3).TotalPoints.Should().Be(2);
    }

    [Fact]
    public async Task ScoreLeagueWeekAsync_MarksLowestWeekAsDropWeek_WhenUserMissesEntireWeek()
    {
        // Arrange
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seasonYear = 2024;

        var league = new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = userId,
            DropLowWeeksCount = 1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty,
            Members = new List<PickemGroupMember>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId,
                    Role = LeagueRole.Commissioner,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                }
            }
        };

        // Create 3 weeks of matchups
        for (int week = 1; week <= 3; week++)
        {
            var contestId = Guid.NewGuid();
            var matchup = new PickemGroupMatchup
            {
                Id = Guid.NewGuid(),
                GroupId = leagueId,
                SeasonWeekId = Guid.NewGuid(),
                ContestId = contestId,
                SeasonYear = seasonYear,
                SeasonWeek = week,
                StartDateUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.Empty
            };

            await DataContext.PickemGroupMatchups.AddAsync(matchup);

            // Only make picks for weeks 1 and 3, skip week 2
            if (week != 2)
            {
                var pick = new PickemGroupUserPick
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId,
                    ContestId = contestId,
                    PickType = PickType.StraightUp,
                    TiebreakerType = TiebreakerType.None,
                    Week = week,
                    PointsAwarded = 1,
                    IsCorrect = true,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = userId
                };

                await DataContext.UserPicks.AddAsync(pick);
            }
        }

        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<LeagueWeekScoringService>();

        // Act
        for (int week = 1; week <= 3; week++)
        {
            await sut.ScoreLeagueWeekAsync(leagueId, seasonYear, week);
        }

        // Assert
        var results = DataContext.PickemGroupWeekResults
            .Where(r => r.PickemGroupId == leagueId)
            .OrderBy(r => r.SeasonWeek)
            .ToList();

        // Week 2 has a result record with 0 picks/points, weeks 1 and 3 have 1 pick/point each
        results.Should().HaveCount(3);

        // Week 1: 1 point - NOT a drop week
        results.First(r => r.SeasonWeek == 1).IsDropWeek.Should().BeFalse();
        results.First(r => r.SeasonWeek == 1).TotalPoints.Should().Be(1);
        results.First(r => r.SeasonWeek == 1).TotalPicks.Should().Be(1);

        // Week 2: 0 points, 0 picks - SHOULD be the drop week
        results.First(r => r.SeasonWeek == 2).IsDropWeek.Should().BeTrue();
        results.First(r => r.SeasonWeek == 2).TotalPoints.Should().Be(0);
        results.First(r => r.SeasonWeek == 2).TotalPicks.Should().Be(0);

        // Week 3: 1 point - NOT a drop week
        results.First(r => r.SeasonWeek == 3).IsDropWeek.Should().BeFalse();
        results.First(r => r.SeasonWeek == 3).TotalPoints.Should().Be(1);
        results.First(r => r.SeasonWeek == 3).TotalPicks.Should().Be(1);
    }

    [Fact]
    public async Task ScoreLeagueWeekAsync_UpdatesExistingResults_WhenCalledMultipleTimes()
    {
        // Arrange
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seasonYear = 2024;
        var weekNumber = 1;

        var league = new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = userId,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty,
            Members = new List<PickemGroupMember>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userId,
                    Role = LeagueRole.Commissioner,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                }
            }
        };

        var contestId = Guid.NewGuid();

        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId,
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        var pick = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId,
            ContestId = contestId,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 1,
            IsCorrect = true,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = userId
        };

        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.UserPicks.AddAsync(pick);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<LeagueWeekScoringService>();

        // Act - Score twice
        await sut.ScoreLeagueWeekAsync(leagueId, seasonYear, weekNumber);
        
        // Modify the pick before scoring again
        pick.PointsAwarded = 2;
        await DataContext.SaveChangesAsync();
        
        await sut.ScoreLeagueWeekAsync(leagueId, seasonYear, weekNumber);

        // Assert
        var results = DataContext.PickemGroupWeekResults
            .Where(r => r.PickemGroupId == leagueId && r.SeasonWeek == weekNumber)
            .ToList();

        results.Should().HaveCount(1, "should update existing record, not create duplicate");
        results.First().TotalPoints.Should().Be(2, "should have updated score");
    }

    [Fact]
    public async Task ScoreAllLeaguesForWeekAsync_ScoresAllLeagues_WithMatchupsForWeek()
    {
        // Arrange
        var league1Id = Guid.NewGuid();
        var league2Id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seasonYear = 2024;
        var weekNumber = 1;

        // League 1
        var league1 = new PickemGroup
        {
            Id = league1Id,
            Name = "League 1",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = userId,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty,
            Members = new List<PickemGroupMember>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = league1Id,
                    UserId = userId,
                    Role = LeagueRole.Commissioner,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                }
            }
        };

        // League 2
        var league2 = new PickemGroup
        {
            Id = league2Id,
            Name = "League 2",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = userId,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty,
            Members = new List<PickemGroupMember>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = league2Id,
                    UserId = userId,
                    Role = LeagueRole.Commissioner,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                }
            }
        };

        var contestId1 = Guid.NewGuid();
        var contestId2 = Guid.NewGuid();

        var matchup1 = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = league1Id,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId1,
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        var matchup2 = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = league2Id,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId2,
            SeasonYear = seasonYear,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        var pick1 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = league1Id,
            UserId = userId,
            ContestId = contestId1,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 1,
            IsCorrect = true,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = userId
        };

        var pick2 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = league2Id,
            UserId = userId,
            ContestId = contestId2,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            Week = weekNumber,
            PointsAwarded = 2,
            IsCorrect = true,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = userId
        };

        await DataContext.PickemGroups.AddRangeAsync(league1, league2);
        await DataContext.PickemGroupMatchups.AddRangeAsync(matchup1, matchup2);
        await DataContext.UserPicks.AddRangeAsync(pick1, pick2);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<LeagueWeekScoringService>();

        // Act
        await sut.ScoreAllLeaguesForWeekAsync(seasonYear, weekNumber);

        // Assert
        var results = DataContext.PickemGroupWeekResults
            .Where(r => r.SeasonWeek == weekNumber)
            .ToList();

        results.Should().HaveCount(2, "both leagues should have results");
        
        var league1Result = results.First(r => r.PickemGroupId == league1Id);
        league1Result.TotalPoints.Should().Be(1);

        var league2Result = results.First(r => r.PickemGroupId == league2Id);
        league2Result.TotalPoints.Should().Be(2);
    }

    [Fact]
    public async Task ScoreLeagueWeekAsync_DoesNothing_WhenLeagueNotFound()
    {
        // Arrange
        var nonExistentLeagueId = Guid.NewGuid();
        var sut = Mocker.CreateInstance<LeagueWeekScoringService>();

        // Act
        await sut.ScoreLeagueWeekAsync(nonExistentLeagueId, 2024, 1);

        // Assert
        var results = DataContext.PickemGroupWeekResults.ToList();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ScoreLeagueWeekAsync_DoesNothing_WhenNoMatchupsForWeek()
    {
        // Arrange
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var league = new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = userId,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.Empty,
            Members = new List<PickemGroupMember>()
        };

        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<LeagueWeekScoringService>();

        // Act
        await sut.ScoreLeagueWeekAsync(leagueId, 2024, 1);

        // Assert
        var results = DataContext.PickemGroupWeekResults.ToList();
        results.Should().BeEmpty();
    }
}
