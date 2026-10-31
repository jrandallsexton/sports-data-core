using FluentAssertions;

using SportsData.Api.Application.MiniGames;
using SportsData.Api.Application.MiniGames.Queries.GetMiniGameStats;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.MiniGames.Queries.GetMiniGameStats;

public class GetMiniGameStatsQueryHandlerTests : ApiTestBase<GetMiniGameStatsQueryHandler>
{
    private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    private int _minute;

    /// <summary>Seeds results in play order, one minute apart.</summary>
    private async Task SeedAsync(Guid userId, string gameKey, params (int Score, int Opponent)[] games)
    {
        foreach (var (score, opponent) in games)
        {
            await DataContext.MiniGameResults.AddAsync(new MiniGameResult
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameKey = gameKey,
                Outcome = score > opponent ? MiniGameOutcome.Win
                    : score < opponent ? MiniGameOutcome.Loss
                    : MiniGameOutcome.Tie,
                Score = score,
                OpponentScore = opponent,
                GameVersion = "1",
                PlayedUtc = Start.AddMinutes(_minute++),
                CreatedUtc = Start,
                CreatedBy = userId
            });
        }

        await DataContext.SaveChangesAsync();
    }

    private Task<Result<MiniGameStatsDto>> GetStats(string gameKey = MiniGameKeys.FootballCards) =>
        Mocker.CreateInstance<GetMiniGameStatsQueryHandler>()
            .ExecuteAsync(new GetMiniGameStatsQuery { UserId = UserId, GameKey = gameKey });

    [Fact]
    public async Task Execute_NoGames_ReturnsZeros()
    {
        var result = await GetStats();

        result.IsSuccess.Should().BeTrue();
        result.Value.Wins.Should().Be(0);
        result.Value.CurrentStreak.Should().BeNull();
        result.Value.BestWinStreak.Should().Be(0);
        result.Value.Recent.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_TotalsAndMargins()
    {
        // W by 7, W by 3, L by 10, T.
        await SeedAsync(UserId, MiniGameKeys.FootballCards, (21, 14), (10, 7), (0, 10), (14, 14));

        var stats = (await GetStats()).Value;

        stats.Wins.Should().Be(2);
        stats.Losses.Should().Be(1);
        stats.Ties.Should().Be(1);
        stats.WinMarginTotal.Should().Be(10);
        stats.LossMarginTotal.Should().Be(10);
    }

    [Fact]
    public async Task Execute_Streaks_TieBreaksAWinRun()
    {
        // W W W T W W L L
        await SeedAsync(UserId, MiniGameKeys.FootballCards,
            (7, 0), (7, 0), (7, 0), (3, 3), (7, 0), (7, 0), (0, 7), (0, 7));

        var stats = (await GetStats()).Value;

        stats.BestWinStreak.Should().Be(3);
        stats.CurrentStreak.Should().BeEquivalentTo(
            new MiniGameStreakDto { Outcome = MiniGameOutcome.Loss, Length = 2 });
    }

    [Fact]
    public async Task Execute_Recent_IsNewestFirst_AndCapped()
    {
        var games = Enumerable.Range(1, 12).Select(i => (i, 0)).ToArray();
        await SeedAsync(UserId, MiniGameKeys.FootballCards, games);

        var stats = (await GetStats()).Value;

        stats.Recent.Should().HaveCount(GetMiniGameStatsQueryHandler.RecentCount);
        stats.Recent.Select(r => r.Score).Should().Equal(12, 11, 10, 9, 8, 7, 6, 5, 4, 3);
    }

    [Fact]
    public async Task Execute_IgnoresOtherUsersAndOtherGames()
    {
        await SeedAsync(UserId, MiniGameKeys.FootballCards, (7, 0));
        await SeedAsync(Guid.NewGuid(), MiniGameKeys.FootballCards, (7, 0), (7, 0));
        await SeedAsync(UserId, "some-other-game", (0, 7));

        var stats = (await GetStats()).Value;

        stats.Wins.Should().Be(1);
        stats.Losses.Should().Be(0);
        stats.Recent.Should().ContainSingle();
    }

    [Fact]
    public async Task Execute_UnknownGame_IsNotFound()
    {
        var result = await GetStats("tic-tac-toe");

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
