using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests.Commands
{
    public interface IReenrichContestHandler
    {
        Task<Result<Guid>> ExecuteAsync(
            ReenrichContestCommand command,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Admin "re-run enrichment" path. Clears the derived/enriched fields
    /// on the Contest row (FinalizedUtc, WinnerFranchiseSeasonId,
    /// SpreadWinnerFranchiseSeasonId, OverUnder, AwayScore, HomeScore,
    /// AuditedUtc) and re-invokes the sport-specific enrichment processor
    /// SYNCHRONOUSLY in this request. Caller (the API admin endpoint) does
    /// not need to poll Hangfire or follow a separate cron — once this
    /// returns, enrichment has rerun, ContestFinalized has fired, and the
    /// API's ContestFinalizedHandler has enqueued ScorePicksCommand.
    ///
    /// This is NOT a re-source. CompetitionCompetitorScores are not
    /// touched. Enrichment reads MAX(CCS) and derives the result fields;
    /// the bug class this handler is for is "derived fields are wrong
    /// but the canonical scores are correct" — primarily a manual
    /// recovery path for stuck WinnerFranchiseSeasonId /
    /// SpreadWinnerFranchiseSeasonId values that the audit job hasn't
    /// caught yet.
    ///
    /// Returns the CorrelationId on success so the caller can log /
    /// surface the same id Producer ran under.
    /// </summary>
    public class ReenrichContestHandler : IReenrichContestHandler
    {
        private readonly ILogger<ReenrichContestHandler> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IEnrichContests _enrichContests;
        private readonly IValidator<ReenrichContestCommand> _validator;

        public ReenrichContestHandler(
            ILogger<ReenrichContestHandler> logger,
            TeamSportDataContext dataContext,
            IEnrichContests enrichContests,
            IValidator<ReenrichContestCommand> validator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _enrichContests = enrichContests;
            _validator = validator;
        }

        public async Task<Result<Guid>> ExecuteAsync(
            ReenrichContestCommand command,
            CancellationToken cancellationToken = default)
        {
            var validationResult = await _validator.ValidateAsync(command, cancellationToken);
            if (!validationResult.IsValid)
            {
                return new Failure<Guid>(
                    default!,
                    ResultStatus.Validation,
                    validationResult.Errors);
            }

            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["ContestId"] = command.ContestId,
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                return await ExecuteInternalAsync(command, cancellationToken);
            }
        }

        private async Task<Result<Guid>> ExecuteInternalAsync(
            ReenrichContestCommand command,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("ReenrichContest requested.");

                var contest = await _dataContext.Contests
                    .FirstOrDefaultAsync(c => c.Id == command.ContestId, cancellationToken);

                if (contest is null)
                {
                    _logger.LogWarning("ReenrichContest: contest not found.");
                    return new Failure<Guid>(
                        default!,
                        ResultStatus.NotFound,
                        []);
                }

                // Null the derived fields so the enrichment processor's
                // FinalizedUtc != null short-circuit doesn't no-op. AwayScore
                // / HomeScore get nulled too so they get re-written from
                // MAX(CCS). AuditedUtc gets cleared so the next audit sweep
                // verifies the newly-derived values.
                contest.FinalizedUtc = null;
                contest.WinnerFranchiseSeasonId = null;
                contest.SpreadWinnerFranchiseSeasonId = null;
                contest.OverUnder = OverUnderResult.None;
                contest.AwayScore = null;
                contest.HomeScore = null;
                contest.AuditedUtc = null;

                await _dataContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("ReenrichContest: cleared derived fields.");

                await _enrichContests.Process(
                    new EnrichContestCommand(command.ContestId, command.CorrelationId));

                _logger.LogInformation("ReenrichContest completed.");

                return new Success<Guid>(command.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReenrichContest failed.");
                return new Failure<Guid>(
                    default!,
                    ResultStatus.Error,
                    []);
            }
        }
    }
}
