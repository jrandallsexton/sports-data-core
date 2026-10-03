using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Application.Scoring.Jobs.PickScoring;

public class PickScoringService : IPickScoringService
{
    private readonly ILogger<PickScoringService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PickScoringService(
        ILogger<PickScoringService> logger,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    public void ScorePick(
        PickemGroup group,
        double? spread,
        PickemGroupUserPick pick,
        MatchupResult result)
    {
        // PickScoringProcessor already guards on this; the early return here
        // is observable defense-in-depth for any future caller that bypasses
        // the processor. Log + return instead of throw — exceptions get
        // swallowed by the processor's broad catch and surface as the
        // misleading "Error scoring pick" message.
        if (result.FinalizedUtc is null)
        {
            _logger.LogWarning(
                "ScorePick called on unfinalized contest — skipping. ContestId={ContestId}, PickId={PickId}",
                result.ContestId, pick.Id);
            return;
        }

        var now = _dateTimeProvider.UtcNow();

        switch (group.PickType)
        {
            case PickType.None:
            case PickType.StraightUp:
                ScoreStraightUp(pick, result, now);
                pick.WasAgainstSpread = false;
                break;

            case PickType.AgainstTheSpread:
                ScoreAgainstSpread(pick, spread, result, now);
                pick.WasAgainstSpread = true;
                break;

            case PickType.OverUnder:
                // TODO: Implement when OverUnder scoring logic is ready
                break;

            default:
                throw new InvalidOperationException("Unsupported PickType: " + group.PickType);
        }

        // Centralized confidence points logic - applies to all pick types
        if (group.UseConfidencePoints)
        {
            pick.PointsAwarded = pick.IsCorrect == true ? (pick.ConfidencePoints ?? 0) : 0;
        }
        else
        {
            pick.PointsAwarded = pick.IsCorrect == true ? 1 : 0;
        }
    }

    /// <summary>
    /// Simulated $1 bet on the picked team at the matchup's closing price:
    /// a win is the net profit (-110 wins 0.9091, +240 wins 2.40), a loss
    /// is -1, a push (ATS) or tie (SU) is 0. Only the column matching the
    /// league's pick type is populated; the others are cleared. Null when
    /// there is nothing to bet: no team picked, no price, or an ATS league
    /// with no or zero spread (scoring falls back to straight-up there, but
    /// the spread bet itself never existed).
    /// </summary>
    public void ScoreSimulatedBets(
        PickemGroup group,
        double? spread,
        PickemGroupUserPick pick,
        MatchupResult result,
        MatchupPricing? pricing)
    {
        if (result.FinalizedUtc is null)
        {
            return;
        }

        pick.PointsSU = null;
        pick.PointsATS = null;
        // Over/under leagues are not scored yet; PointsOU stays null until they are.
        pick.PointsOU = null;

        bool? pickedIsHome = pick.FranchiseSeasonId == result.HomeFranchiseSeasonId ? true
            : pick.FranchiseSeasonId == result.AwayFranchiseSeasonId ? false
            : null;

        if (!pick.FranchiseSeasonId.HasValue || pickedIsHome is null || pricing is null)
        {
            return;
        }

        switch (group.PickType)
        {
            case PickType.None:
            case PickType.StraightUp:
            {
                var moneyLine = pickedIsHome.Value ? pricing.HomeMoneyLine : pricing.AwayMoneyLine;
                pick.PointsSU = SettleBet(moneyLine, result.WinnerFranchiseSeasonId, pick.FranchiseSeasonId.Value);
                break;
            }

            case PickType.AgainstTheSpread:
            {
                if (!spread.HasValue || spread.Value == 0)
                {
                    break;
                }

                var spreadPrice = pickedIsHome.Value ? pricing.HomeSpreadPrice : pricing.AwaySpreadPrice;
                pick.PointsATS = SettleBet(
                    ToPrice(spreadPrice),
                    ResolveSpreadWinner(spread.Value, result),
                    pick.FranchiseSeasonId.Value);
                break;
            }
        }
    }

    /// <summary>
    /// The stored spread price is a double. NaN, infinity or a magnitude past
    /// decimal's range would throw on the cast, and the processors' catch
    /// would then skip the pick; treat any of them as "no price" (CodeRabbit,
    /// #805). Real prices arrive from ESPN JSON via a decimal, so this is
    /// defense only.
    /// </summary>
    private static decimal? ToPrice(double? price) =>
        price is { } value && double.IsFinite(value) && Math.Abs(value) < 1_000_000d
            ? (decimal)value
            : null;

    /// <param name="winnerId">Null means a tie (SU) or push (ATS): the stake comes back.</param>
    private static decimal? SettleBet(decimal? americanPrice, Guid? winnerId, Guid pickedId)
    {
        var profit = NetProfitPerDollar(americanPrice);

        if (profit is null)
        {
            return null;
        }

        if (!winnerId.HasValue)
        {
            return 0m;
        }

        return winnerId.Value == pickedId ? profit : -1m;
    }

    /// <summary>
    /// Net profit of a winning $1 bet at an American price. Rounded to the
    /// column's 4 places HERE so a re-score compares equal to what Postgres
    /// stored; the nightly audit would otherwise "correct" every pick. Null
    /// for a value that is not an American price (|price| under 100), which
    /// would otherwise pay absurdly (a stray -5 is a 20x payout).
    /// </summary>
    private static decimal? NetProfitPerDollar(decimal? americanPrice)
    {
        if (americanPrice is null || Math.Abs(americanPrice.Value) < 100m)
        {
            return null;
        }

        var profit = americanPrice.Value > 0
            ? americanPrice.Value / 100m
            : 100m / -americanPrice.Value;

        return Math.Round(profit, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The team that covered the home-relative spread, or null on a push.
    /// </summary>
    private static Guid? ResolveSpreadWinner(double spread, MatchupResult result)
    {
        var homeScore = result.HomeScore;
        var awayScore = result.AwayScore;

        if (spread < 0)
        {
            // Home team was favored: adjust home score
            var adjustedHomeScore = homeScore + spread;

            if (adjustedHomeScore > awayScore)
                return result.HomeFranchiseSeasonId;
            if (adjustedHomeScore < awayScore)
                return result.AwayFranchiseSeasonId;
            return null;
        }

        // Away team was favored: adjust away score
        var adjustedAwayScore = awayScore + (-spread);

        if (adjustedAwayScore > homeScore)
            return result.AwayFranchiseSeasonId;
        if (adjustedAwayScore < homeScore)
            return result.HomeFranchiseSeasonId;
        return null;
    }

    private void ScoreStraightUp(
        PickemGroupUserPick pick,
        MatchupResult result,
        DateTime now)
    {
        if (!pick.FranchiseSeasonId.HasValue)
        {
            SetIncorrect(pick, now);
            return;
        }

        pick.IsCorrect = pick.FranchiseSeasonId == result.WinnerFranchiseSeasonId;
        pick.ScoredAt = now;
        pick.AuditedUtc = null;
    }

    private void ScoreAgainstSpread(
        PickemGroupUserPick pick,
        double? spread,
        MatchupResult result,
        DateTime now)
    {
        if (!pick.FranchiseSeasonId.HasValue)
        {
            SetIncorrect(pick, now);
            return;
        }

        // If no spread was provided or is zero, fall back to straight up scoring
        if (!spread.HasValue || spread.Value == 0)
        {
            ScoreStraightUp(pick, result, now);
            return;
        }

        var spreadWinnerId = ResolveSpreadWinner(spread.Value, result);

        // PUSH: the game landed exactly on the line — the bet never happened.
        // Nobody is graded: IsCorrect stays null (with ScoredAt set, so the
        // processor's ScoredAt == null selection never re-scores it) and the
        // centralized points block awards 0. Distinct from a loss: pushes are
        // excluded from accuracy (leaderboard counts decided picks only) and
        // render as "Push", not ✗. Was graded as a loss until 2026-09-08
        // (SMU@FSU: SMU -3, 27-24 — every ATS pick marked incorrect).
        if (!spreadWinnerId.HasValue)
        {
            pick.IsCorrect = null;
            pick.ScoredAt = now;
            pick.AuditedUtc = null;
            return;
        }

        pick.IsCorrect = pick.FranchiseSeasonId == spreadWinnerId.Value;
        pick.ScoredAt = now;
        pick.AuditedUtc = null;
    }

    // NOTE: every write of ScoredAt above also clears AuditedUtc. Scoring a
    // pick IS a change to the values the nightly audit verifies, so any prior
    // audit is stale by definition. Keeping the two together here means a
    // future caller of ScorePick cannot forget it. (The audit itself scores a
    // CLONE, so it never trips this on the real pick.)
    private void SetIncorrect(PickemGroupUserPick pick, DateTime now)
    {
        pick.IsCorrect = false;
        pick.ScoredAt = now;
        pick.AuditedUtc = null;
        pick.PointsAwarded = 0;
    }
}