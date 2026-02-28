using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Franchises.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonEnrichment;

public interface IEnqueueFranchiseSeasonEnrichmentCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        EnqueueFranchiseSeasonEnrichmentCommand command,
        CancellationToken cancellationToken = default);
}

public class EnqueueFranchiseSeasonEnrichmentCommandHandler : IEnqueueFranchiseSeasonEnrichmentCommandHandler
{
    private readonly ILogger<EnqueueFranchiseSeasonEnrichmentCommandHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly IValidator<EnqueueFranchiseSeasonEnrichmentCommand> _validator;

    public EnqueueFranchiseSeasonEnrichmentCommandHandler(
        ILogger<EnqueueFranchiseSeasonEnrichmentCommandHandler> logger,
        TeamSportDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider,
        IValidator<EnqueueFranchiseSeasonEnrichmentCommand> validator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
        _validator = validator;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        EnqueueFranchiseSeasonEnrichmentCommand command,
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
            ["SeasonYear"] = command.SeasonYear
        }))
        {
            return await ExecuteInternalAsync(command, cancellationToken);
        }
    }

    private async Task<Result<Guid>> ExecuteInternalAsync(
        EnqueueFranchiseSeasonEnrichmentCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Enqueueing franchise season enrichment jobs");

            var correlationId = Guid.NewGuid();

            // Get all franchise season IDs for the specified sport/year
            var franchiseSeasonIds = await _dataContext.FranchiseSeasons
                .AsNoTracking()
                .Where(fs =>
                    fs.SeasonYear == command.SeasonYear &&
                    fs.Franchise.Sport == command.Sport)
                .Select(fs => fs.Id)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Found {Count} franchise seasons to enrich",
                franchiseSeasonIds.Count);

            // Enqueue enrichment job for each franchise season
            foreach (var franchiseSeasonId in franchiseSeasonIds)
            {
                var enrichCommand = new EnrichFranchiseSeasonCommand(
                    franchiseSeasonId,
                    command.SeasonYear,
                    correlationId);

                _backgroundJobProvider.Enqueue<IEnrichFranchiseSeasons>(
                    h => h.Process(enrichCommand));
            }

            _logger.LogInformation("Franchise season enrichment queueing complete");

            return new Success<Guid>(correlationId, ResultStatus.Accepted);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to enqueue franchise season enrichment jobs");

            return new Failure<Guid>(
                default!,
                ResultStatus.Error,
                []);
        }
    }
}
