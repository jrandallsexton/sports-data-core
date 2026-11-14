using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.FranchiseContestFacts.Commands.ProjectContestFacts;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.FranchiseContestFacts.Commands.RebuildSeasonFacts;

public interface IRebuildSeasonFactsCommandHandler
{
    /// <summary>Returns the number of contests enqueued for projection.</summary>
    Task<Result<int>> ExecuteAsync(RebuildSeasonFactsCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Enqueues one <see cref="ProjectContestFactsCommand"/> per contest of the
/// season: every finalized contest, plus every contest that already has fact
/// rows (so rows for a contest that is no longer projectable are removed).
/// The backfill for history, and the re-projection after a derivation change
/// (ProjectionVersion). Also the backstop for the one dependency the
/// per-game triggers do not follow: a correction to an earlier game shifts
/// the record-going-in of every later game of both teams.
/// </summary>
public class RebuildSeasonFactsCommandHandler : IRebuildSeasonFactsCommandHandler
{
    private const int EarliestSeason = 2000;

    private readonly ILogger<RebuildSeasonFactsCommandHandler> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RebuildSeasonFactsCommandHandler(
        ILogger<RebuildSeasonFactsCommandHandler> logger,
        FootballDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<int>> ExecuteAsync(
        RebuildSeasonFactsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.SeasonYear < EarliestSeason || command.SeasonYear > _dateTimeProvider.UtcNow().Year + 1)
        {
            return new Failure<int>(0, ResultStatus.Validation,
                [new ValidationFailure(nameof(command.SeasonYear),
                    $"Season year must be between {EarliestSeason} and next year")]);
        }

        var finalized = await _dataContext.Contests
            .AsNoTracking()
            .Where(c => c.SeasonYear == command.SeasonYear && c.FinalizedUtc != null)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var projected = await _dataContext.FranchiseContestFacts
            .AsNoTracking()
            .Where(f => f.SeasonYear == command.SeasonYear)
            .Select(f => f.ContestId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var contestIds = finalized.Union(projected).ToList();

        // Season-wide, so computed once here rather than in every job.
        var seasonUsesConferenceFlag = await SeasonConferenceFlag.SeasonUsesFlagAsync(
            _dataContext, command.SeasonYear, cancellationToken);

        foreach (var contestId in contestIds)
        {
            var cmd = new ProjectContestFactsCommand(contestId, seasonUsesConferenceFlag);
            _backgroundJobProvider.Enqueue<IProjectContestFactsCommandHandler>(
                h => h.ExecuteAsync(cmd, CancellationToken.None));
        }

        _logger.LogInformation(
            "Fact rebuild enqueued {Count} contests for {SeasonYear} ({Finalized} finalized, {Projected} already projected).",
            contestIds.Count, command.SeasonYear, finalized.Count, projected.Count);

        return new Success<int>(contestIds.Count);
    }
}
