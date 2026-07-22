using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests.Commands
{
    public interface IRefreshContestsBySeasonYearHandler
    {
        Task<Result<Guid>> ExecuteAsync(
            RefreshContestsBySeasonYearCommand command,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Re-sources every contest for a (sport, season) by enqueuing the narrowed
    /// <see cref="UpdateContestCommand"/> per contest — the same path the admin
    /// "refresh contest" button fires, at season scale.
    ///
    /// The distinct ContestId set comes from the FranchiseSeason traversal
    /// (FranchiseSeason → CompetitionCompetitor → Competition.ContestId,
    /// deduped) rather than Contest.SeasonYear, to avoid leaning on that
    /// denormalized column. Each contest has two CompetitionCompetitors
    /// (home + away), so the Distinct() is load-bearing. No throttling — the
    /// Redis token bucket meters ESPN load. See
    /// docs/features/season-contest-resource-driver.md.
    /// </summary>
    public class RefreshContestsBySeasonYearHandler : IRefreshContestsBySeasonYearHandler
    {
        private readonly ILogger<RefreshContestsBySeasonYearHandler> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IValidator<RefreshContestsBySeasonYearCommand> _validator;

        public RefreshContestsBySeasonYearHandler(
            ILogger<RefreshContestsBySeasonYearHandler> logger,
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider,
            IValidator<RefreshContestsBySeasonYearCommand> validator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
            _validator = validator;
        }

        public async Task<Result<Guid>> ExecuteAsync(
            RefreshContestsBySeasonYearCommand command,
            CancellationToken cancellationToken = default)
        {
            var validationResult = await _validator.ValidateAsync(command, cancellationToken);
            if (!validationResult.IsValid)
            {
                return new Failure<Guid>(default!, ResultStatus.Validation, validationResult.Errors);
            }

            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["Sport"] = command.Sport,
                       ["SeasonYear"] = command.SeasonYear,
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                return await ExecuteInternalAsync(command, cancellationToken);
            }
        }

        private async Task<Result<Guid>> ExecuteInternalAsync(
            RefreshContestsBySeasonYearCommand command,
            CancellationToken cancellationToken)
        {
            try
            {
                // Distinct ContestIds via the FranchiseSeason traversal (avoids the
                // Contest.SeasonYear denorm). Each contest has two competitors, so
                // Distinct() collapses the home/away duplicates.
                var franchiseSeasonIds = _dataContext.FranchiseSeasons
                    .AsNoTracking()
                    .Where(fs => fs.SeasonYear == command.SeasonYear)
                    .Select(fs => fs.Id);

                var contestIds = await _dataContext.CompetitionCompetitors
                    .AsNoTracking()
                    .Where(cc => franchiseSeasonIds.Contains(cc.FranchiseSeasonId))
                    .Select(cc => cc.Competition.ContestId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                _logger.LogInformation(
                    "Refreshing {ContestCount} distinct contests for the season.",
                    contestIds.Count);

                foreach (var contestId in contestIds)
                {
                    var cmd = new UpdateContestCommand(
                        contestId,
                        SourceDataProvider.Espn,
                        command.Sport,
                        command.CorrelationId);

                    _backgroundJobProvider.Enqueue<IUpdateContests>(p => p.Process(cmd));
                }

                _logger.LogInformation("Contest refresh queueing complete.");

                return new Success<Guid>(command.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh contests by season year");
                return new Failure<Guid>(default!, ResultStatus.Error, []);
            }
        }
    }
}
