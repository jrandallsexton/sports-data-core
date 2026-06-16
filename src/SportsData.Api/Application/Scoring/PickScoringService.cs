using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.Scoring;

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

    private void ScoreStraightUp(
        PickemGroupUserPick pick,
        MatchupResult result,
        DateTime now)
    {
        if (!pick.FranchiseId.HasValue)
        {
            SetIncorrect(pick, now);
            return;
        }

        pick.IsCorrect = pick.FranchiseId == result.WinnerFranchiseSeasonId;
        pick.ScoredAt = now;
    }

    private void ScoreAgainstSpread(
        PickemGroupUserPick pick,
        double? spread,
        MatchupResult result,
        DateTime now)
    {
        if (!pick.FranchiseId.HasValue)
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

        var homeScore = result.HomeScore;
        var awayScore = result.AwayScore;

        Guid? spreadWinnerId = null;

        if (spread.Value < 0)
        {
            // Home team was favored: adjust home score
            var adjustedHomeScore = homeScore + spread.Value;

            if (adjustedHomeScore > awayScore)
                spreadWinnerId = result.HomeFranchiseSeasonId;
            else if (adjustedHomeScore < awayScore)
                spreadWinnerId = result.AwayFranchiseSeasonId;
            // else: it's a push → leave spreadWinnerId as null
        }
        else
        {
            // Away team was favored: adjust away score
            var adjustedAwayScore = awayScore + (-spread.Value);

            if (adjustedAwayScore > homeScore)
                spreadWinnerId = result.AwayFranchiseSeasonId;
            else if (adjustedAwayScore < homeScore)
                spreadWinnerId = result.HomeFranchiseSeasonId;
            // else: it's a push → leave spreadWinnerId as null
        }

        pick.IsCorrect = spreadWinnerId.HasValue && pick.FranchiseId == spreadWinnerId.Value;
        pick.ScoredAt = now;
    }

    private void SetIncorrect(PickemGroupUserPick pick, DateTime now)
    {
        pick.IsCorrect = false;
        pick.ScoredAt = now;
        pick.PointsAwarded = 0;
    }
}