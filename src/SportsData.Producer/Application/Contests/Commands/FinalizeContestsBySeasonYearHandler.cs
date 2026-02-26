using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests.Commands
{
    public interface IFinalizeContestsBySeasonYearHandler
    {
        Task<Result<Guid>> ExecuteAsync(
            FinalizeContestsBySeasonYearCommand command,
            CancellationToken cancellationToken = default);
    }

    public class FinalizeContestsBySeasonYearHandler : IFinalizeContestsBySeasonYearHandler
    {
        private readonly ILogger<FinalizeContestsBySeasonYearHandler> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IValidator<FinalizeContestsBySeasonYearCommand> _validator;

        public FinalizeContestsBySeasonYearHandler(
            ILogger<FinalizeContestsBySeasonYearHandler> logger,
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider,
            IValidator<FinalizeContestsBySeasonYearCommand> validator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
            _validator = validator;
        }

        public async Task<Result<Guid>> ExecuteAsync(
            FinalizeContestsBySeasonYearCommand command,
            CancellationToken cancellationToken = default)
        {
            // Validate command
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
                       ["Sport"] = command.Sport,
                       ["SeasonYear"] = command.SeasonYear,
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                return await ExecuteInternalAsync(command, cancellationToken);
            }
        }

        private async Task<Result<Guid>> ExecuteInternalAsync(
            FinalizeContestsBySeasonYearCommand command,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Finalizing contests");

                var contestIds = await _dataContext.Contests
                    .AsNoTracking()
                    .Where(c => c.Sport == command.Sport &&
                                c.SeasonYear == command.SeasonYear &&
                                c.FinalizedUtc == null)
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);

                _logger.LogInformation(
                    "Found {ContestCount} unfinalized contests to process",
                    contestIds.Count);

                foreach (var contestId in contestIds)
                {
                    var cmd = new EnrichContestCommand(contestId, command.CorrelationId);
                    _backgroundJobProvider.Enqueue<IEnrichContests>(p => p.Process(cmd));
                }

                await _dataContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Finalization queueing complete");

                return new Success<Guid>(command.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to finalize contests");

                return new Failure<Guid>(
                    default!,
                    ResultStatus.Error,
                    [new FluentValidation.Results.ValidationFailure(
                        string.Empty,
                        $"An error occurred while finalizing contests: {ex.Message}")]);
            }
        }
    }
}
