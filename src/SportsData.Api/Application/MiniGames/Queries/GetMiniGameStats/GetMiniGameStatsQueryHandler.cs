using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.MiniGames.Queries.GetMiniGameStats;

public interface IGetMiniGameStatsQueryHandler
{
    Task<Result<MiniGameStatsDto>> ExecuteAsync(
        GetMiniGameStatsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The signed-in user's record for one mini-game. Streaks need the results in
/// play order, so the handler reads the user's rows for that game (four small
/// columns each) and folds them in memory instead of aggregating in SQL.
/// Reading the full history is deliberate (reviewed on #851): the best streak
/// needs all of it, the (UserId, GameKey, PlayedUtc) index serves the read, and
/// the submit handler's daily cap bounds growth. A heavy player is a few
/// thousand rows. Revisit with a stored running aggregate if a profile shows
/// otherwise. See docs/features/mini-game-results.md.
/// </summary>
public class GetMiniGameStatsQueryHandler : IGetMiniGameStatsQueryHandler
{
    public const int RecentCount = 10;

    private readonly AppDataContext _db;

    public GetMiniGameStatsQueryHandler(AppDataContext db)
    {
        _db = db;
    }

    public async Task<Result<MiniGameStatsDto>> ExecuteAsync(
        GetMiniGameStatsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!MiniGameKeys.Known.Contains(query.GameKey))
        {
            return new Failure<MiniGameStatsDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.GameKey), $"Unknown game '{query.GameKey}'.")]);
        }

        var results = await _db.MiniGameResults
            .AsNoTracking()
            .Where(r => r.UserId == query.UserId && r.GameKey == query.GameKey)
            .OrderBy(r => r.PlayedUtc)
            .ThenBy(r => r.CreatedUtc)
            .ThenBy(r => r.Id)
            .Select(r => new MiniGameRecentResultDto
            {
                Id = r.Id,
                Outcome = r.Outcome,
                Score = r.Score,
                OpponentScore = r.OpponentScore,
                PlayedUtc = r.PlayedUtc
            })
            .ToListAsync(cancellationToken);

        int wins = 0, losses = 0, ties = 0, winMargin = 0, lossMargin = 0, best = 0;
        MiniGameStreakDto? streak = null;

        foreach (var r in results)
        {
            switch (r.Outcome)
            {
                case MiniGameOutcome.Win:
                    wins++;
                    winMargin += r.Score - r.OpponentScore;
                    break;
                case MiniGameOutcome.Loss:
                    losses++;
                    lossMargin += r.OpponentScore - r.Score;
                    break;
                default:
                    ties++;
                    break;
            }

            // A tie is its own streak, so it breaks a run of wins or losses.
            streak = streak is not null && streak.Outcome == r.Outcome
                ? streak with { Length = streak.Length + 1 }
                : new MiniGameStreakDto { Outcome = r.Outcome, Length = 1 };

            if (streak.Outcome == MiniGameOutcome.Win && streak.Length > best)
                best = streak.Length;
        }

        return new Success<MiniGameStatsDto>(new MiniGameStatsDto
        {
            Wins = wins,
            Losses = losses,
            Ties = ties,
            WinMarginTotal = winMargin,
            LossMarginTotal = lossMargin,
            CurrentStreak = streak,
            BestWinStreak = best,
            Recent = results.AsEnumerable().Reverse().Take(RecentCount).ToList()
        });
    }
}
