using FluentAssertions;
using SportsData.Api.Application.Common.Enums;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;

public class GetUserPicksByGroupAndWeekQueryHandlerTests : ApiTestBase<GetUserPicksByGroupAndWeekQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenNoPicksExist()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetUserPicksByGroupAndWeekQueryHandler>();
        var query = new GetUserPicksByGroupAndWeekQuery
        {
            UserId = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            WeekNumber = 1
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Picks.Should().BeEmpty();
        result.Value.TotalMatchups.Should().Be(0);
        result.Value.CorrectCount.Should().Be(0);
        result.Value.IncorrectCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUserPicks_WhenPicksExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        var user = new UserEntity
        {
            Username = "test_user_15",
            Id = userId,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "test@test.com",
            DisplayName = "Test User",
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow
        };
        await DataContext.Users.AddAsync(user);

        var franchiseSeasonId = Guid.NewGuid();
        var pick = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = groupId,
            ContestId = contestId,
            Week = 5,
            PickType = PickType.StraightUp,
            FranchiseSeasonId = franchiseSeasonId,
            ConfidencePoints = 7,
            IsCorrect = true,
            PointsAwarded = 7,
            TiebreakerType = TiebreakerType.TotalPoints
        };
        await DataContext.UserPicks.AddAsync(pick);

        // The matchup the pick belongs to — a pick without its matchup is an
        // impossible state, and TotalMatchups should reflect it. Fixed instant:
        // the handler never consumes time, so seed timestamps are inert.
        await DataContext.PickemGroupMatchups.AddAsync(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            SeasonYear = 2025,
            SeasonWeek = 5,
            SeasonWeekId = Guid.NewGuid(),
            StartDateUtc = new DateTime(2025, 10, 4, 12, 0, 0, DateTimeKind.Utc)
        });
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserPicksByGroupAndWeekQueryHandler>();
        var query = new GetUserPicksByGroupAndWeekQuery
        {
            UserId = userId,
            GroupId = groupId,
            WeekNumber = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Picks.Should().HaveCount(1);
        result.Value.Picks[0].UserId.Should().Be(userId);
        result.Value.Picks[0].ContestId.Should().Be(contestId);
        result.Value.Picks[0].FranchiseSeasonId.Should().Be(franchiseSeasonId);
        result.Value.Picks[0].PickType.Should().Be(PickType.StraightUp);
        result.Value.Picks[0].ConfidencePoints.Should().Be(7);
        result.Value.Picks[0].IsCorrect.Should().BeTrue();
        result.Value.Picks[0].PointsAwarded.Should().Be(7);
        result.Value.CorrectCount.Should().Be(1);
        result.Value.IncorrectCount.Should().Be(0);
        result.Value.TotalMatchups.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFilterByWeek_WhenMultipleWeeksExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var user = new UserEntity
        {
            Username = "test_user_16",
            Id = userId,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "test@test.com",
            DisplayName = "Test User",
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow
        };
        await DataContext.Users.AddAsync(user);

        var pick1 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = groupId,
            ContestId = Guid.NewGuid(),
            Week = 5,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints
        };
        var pick2 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = groupId,
            ContestId = Guid.NewGuid(),
            Week = 6,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints
        };
        await DataContext.UserPicks.AddRangeAsync(pick1, pick2);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserPicksByGroupAndWeekQueryHandler>();
        var query = new GetUserPicksByGroupAndWeekQuery
        {
            UserId = userId,
            GroupId = groupId,
            WeekNumber = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Picks.Should().HaveCount(1);
        result.Value.Picks[0].ContestId.Should().Be(pick1.ContestId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFilterByUser_WhenMultipleUsersExist()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var user1 = new UserEntity
        {
            Username = "test_user_17",
            Id = userId1,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "test1@test.com",
            DisplayName = "Test User 1",
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow
        };
        var user2 = new UserEntity
        {
            Username = "test_user_18",
            Id = userId2,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "test2@test.com",
            DisplayName = "Test User 2",
            SignInProvider = "test",
            LastLoginUtc = DateTime.UtcNow
        };
        await DataContext.Users.AddRangeAsync(user1, user2);

        var pick1 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId1,
            PickemGroupId = groupId,
            ContestId = Guid.NewGuid(),
            Week = 5,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints
        };
        var pick2 = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId2,
            PickemGroupId = groupId,
            ContestId = Guid.NewGuid(),
            Week = 5,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints
        };
        await DataContext.UserPicks.AddRangeAsync(pick1, pick2);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserPicksByGroupAndWeekQueryHandler>();
        var query = new GetUserPicksByGroupAndWeekQuery
        {
            UserId = userId1,
            GroupId = groupId,
            WeekNumber = 5
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Picks.Should().HaveCount(1);
        result.Value.Picks[0].UserId.Should().Be(userId1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldComputeResultCounts_AcrossPickOutcomes()
    {
        // Arrange — 5 matchups in the group-week: 2 correct picks, 1 incorrect,
        // 1 picked-but-unscored (IsCorrect null), 1 unpicked. The client
        // derives X (no scored pick) = TotalMatchups - Correct - Incorrect = 2,
        // covering both the unpicked and the unscored game.
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        const int week = 5;
        // Fixed instants: the clock is pinned AFTER kickoff, so the unpicked
        // matchup is locked (not pending) and only the unscored pick pends.
        var seededUtc = new DateTime(2025, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        Mocker.GetMock<SportsData.Core.Common.IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(seededUtc.AddHours(2));

        var user = new UserEntity
        {
            Username = "test_user_19",
            Id = userId,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "test@test.com",
            DisplayName = "Test User",
            SignInProvider = "test",
            LastLoginUtc = seededUtc
        };
        await DataContext.Users.AddAsync(user);

        var contestIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var contestId in contestIds)
        {
            await DataContext.PickemGroupMatchups.AddAsync(new PickemGroupMatchup
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                ContestId = contestId,
                SeasonYear = 2025,
                SeasonWeek = week,
                SeasonWeekId = Guid.NewGuid(),
                StartDateUtc = seededUtc
            });
        }

        // A matchup for another week must not inflate TotalMatchups.
        await DataContext.PickemGroupMatchups.AddAsync(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = Guid.NewGuid(),
            SeasonYear = 2025,
            SeasonWeek = week + 1,
            SeasonWeekId = Guid.NewGuid(),
            StartDateUtc = seededUtc
        });

        bool?[] outcomes = [true, true, false, null]; // contestIds[4] stays unpicked
        for (var i = 0; i < outcomes.Length; i++)
        {
            await DataContext.UserPicks.AddAsync(new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PickemGroupId = groupId,
                ContestId = contestIds[i],
                Week = week,
                PickType = PickType.StraightUp,
                IsCorrect = outcomes[i],
                TiebreakerType = TiebreakerType.TotalPoints
            });
        }
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserPicksByGroupAndWeekQueryHandler>();
        var query = new GetUserPicksByGroupAndWeekQuery
        {
            UserId = userId,
            GroupId = groupId,
            WeekNumber = week
        };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Picks.Should().HaveCount(4);
        result.Value.TotalMatchups.Should().Be(5);
        result.Value.CorrectCount.Should().Be(2);
        result.Value.IncorrectCount.Should().Be(1);
        // Pending = the unscored pick only. The unpicked matchup's game already
        // started (now = kickoff + 2h), so it's a decided no-result, not pending.
        result.Value.PendingCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_PendingCounts_UnpickedFutureGame_AndZeroWhenResolved()
    {
        // Arrange — 2 matchups: one picked-and-scored, one unpicked. With the
        // clock BEFORE the unpicked game's kickoff it's still actionable
        // (pending = 1); with the clock AFTER kickoff the user's week is fully
        // resolved (pending = 0) even though that game may still be running —
        // the user's results can no longer change.
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        const int week = 3;
        var kickoffUtc = new DateTime(2025, 11, 1, 16, 0, 0, DateTimeKind.Utc);

        var user = new UserEntity
        {
            Username = "test_user_20",
            Id = userId,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "test@test.com",
            DisplayName = "Test User",
            SignInProvider = "test",
            LastLoginUtc = kickoffUtc
        };
        await DataContext.Users.AddAsync(user);

        var pickedContestId = Guid.NewGuid();
        var unpickedContestId = Guid.NewGuid();
        foreach (var contestId in new[] { pickedContestId, unpickedContestId })
        {
            await DataContext.PickemGroupMatchups.AddAsync(new PickemGroupMatchup
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                ContestId = contestId,
                SeasonYear = 2025,
                SeasonWeek = week,
                SeasonWeekId = Guid.NewGuid(),
                StartDateUtc = kickoffUtc
            });
        }

        await DataContext.UserPicks.AddAsync(new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = groupId,
            ContestId = pickedContestId,
            Week = week,
            PickType = PickType.StraightUp,
            IsCorrect = true,
            TiebreakerType = TiebreakerType.TotalPoints
        });
        await DataContext.SaveChangesAsync();

        var clock = Mocker.GetMock<SportsData.Core.Common.IDateTimeProvider>();
        var handler = Mocker.CreateInstance<GetUserPicksByGroupAndWeekQueryHandler>();
        var query = new GetUserPicksByGroupAndWeekQuery
        {
            UserId = userId,
            GroupId = groupId,
            WeekNumber = week
        };

        // Act + Assert — before kickoff: the unpicked game is still actionable.
        clock.Setup(x => x.UtcNow()).Returns(kickoffUtc.AddHours(-1));
        var before = await handler.ExecuteAsync(query);
        before.IsSuccess.Should().BeTrue();
        before.Value.PendingCount.Should().Be(1);

        // After kickoff: unpicked game is locked -> a decided no-result;
        // the only pick is scored -> the week is resolved for this user.
        clock.Setup(x => x.UtcNow()).Returns(kickoffUtc.AddHours(1));
        var after = await handler.ExecuteAsync(query);
        after.IsSuccess.Should().BeTrue();
        after.Value.PendingCount.Should().Be(0);
        after.Value.TotalMatchups.Should().Be(2);
        after.Value.CorrectCount.Should().Be(1);
    }
}
