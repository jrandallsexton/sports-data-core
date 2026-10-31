using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.MiniGames.Queries.GetMiniGameStats;

public record GetMiniGameStatsQuery
{
    public Guid UserId { get; init; }

    public string GameKey { get; init; } = null!;
}

/// <summary>
/// Raw totals rather than averages, so the client can fold in results still
/// queued on the device exactly (average = total / count). Margins count wins
/// and losses only; ties have none.
/// </summary>
public record MiniGameStatsDto
{
    public int Wins { get; init; }

    public int Losses { get; init; }

    public int Ties { get; init; }

    public int WinMarginTotal { get; init; }

    public int LossMarginTotal { get; init; }

    /// <summary>Null until the first game.</summary>
    public MiniGameStreakDto? CurrentStreak { get; init; }

    public int BestWinStreak { get; init; }

    /// <summary>The most recent results, newest first.</summary>
    public List<MiniGameRecentResultDto> Recent { get; init; } = [];
}

public record MiniGameStreakDto
{
    public MiniGameOutcome Outcome { get; init; }

    public int Length { get; init; }
}

public record MiniGameRecentResultDto
{
    public Guid Id { get; init; }

    public MiniGameOutcome Outcome { get; init; }

    public int Score { get; init; }

    public int OpponentScore { get; init; }

    public DateTime PlayedUtc { get; init; }
}
