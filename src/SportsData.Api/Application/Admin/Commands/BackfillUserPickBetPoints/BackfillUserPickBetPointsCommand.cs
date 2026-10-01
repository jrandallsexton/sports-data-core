using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Commands.BackfillUserPickBetPoints;

/// <summary>
/// Compute the simulated $1 bet columns (PointsSU, PointsATS, PointsOU) on
/// every already-scored UserPick: one background job per distinct contest.
/// Leaves IsCorrect and PointsAwarded untouched.
/// </summary>
public record BackfillUserPickBetPointsCommand;

/// <param name="CorrelationId">Shared by every enqueued job; the Seq handle for the run.</param>
public record BackfillUserPickBetPointsResult(
    Guid CorrelationId,
    int ContestsEnqueued,
    List<BackfillUserPickBetPointsSportResult> Sports);

public record BackfillUserPickBetPointsSportResult(Sport Sport, int ContestsEnqueued);

/// <summary>One contest's job: fetch its result, price every scored pick on it from its league's matchup.</summary>
public record ApplyUserPickBetPointsCommand(Sport Sport, Guid ContestId, Guid CorrelationId);
