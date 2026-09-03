using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Previews;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Admin.Queries.GetModelLabMatrix;

public interface IGetModelLabMatrixQueryHandler
{
    Task<Result<ModelLabMatrixDto>> ExecuteAsync(
        GetModelLabMatrixQuery query,
        CancellationToken cancellationToken);
}

public class GetModelLabMatrixQueryHandler : IGetModelLabMatrixQueryHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IAiModelClientResolver _modelClientResolver;
    private readonly ILogger<GetModelLabMatrixQueryHandler> _logger;

    public GetModelLabMatrixQueryHandler(
        AppDataContext dataContext,
        IContestClientFactory contestClientFactory,
        IAiModelClientResolver modelClientResolver,
        ILogger<GetModelLabMatrixQueryHandler> logger)
    {
        _dataContext = dataContext;
        _contestClientFactory = contestClientFactory;
        _modelClientResolver = modelClientResolver;
        _logger = logger;
    }

    public async Task<Result<ModelLabMatrixDto>> ExecuteAsync(
        GetModelLabMatrixQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await new GetModelLabMatrixQueryValidator()
            .ValidateAsync(query, cancellationToken);

        if (!validation.IsValid)
        {
            return new Failure<ModelLabMatrixDto>(default!, ResultStatus.Validation, validation.Errors);
        }

        // The contest universe is "every game any pick'em league carries"
        // for the week — PickemGroupMatchup is the synced, API-local record
        // of exactly that, distinct across leagues.
        var contestIds = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.SeasonYear == query.SeasonYear
                     && m.SeasonWeek == query.Week
                     && _dataContext.PickemGroups
                         .Any(g => g.Id == m.GroupId && g.Sport == query.Sport))
            .Select(m => m.ContestId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var columns = await GetModelColumnsAsync(cancellationToken);

        if (contestIds.Count == 0)
        {
            return new Success<ModelLabMatrixDto>(new ModelLabMatrixDto { Models = columns });
        }

        // Display identity (names + FranchiseSeasonIds for pick mapping)
        // comes from Producer's existing batch matchup endpoint — the same
        // source the picks page renders from.
        var matchupsResult = await _contestClientFactory
            .Resolve(query.Sport)
            .GetMatchupsByContestIds(contestIds, MarkDirection.Roundel, cancellationToken);

        if (!matchupsResult.IsSuccess || matchupsResult.Value is null)
        {
            _logger.LogError(
                "Producer GetMatchupsByContestIds failed for model-lab matrix. Sport={Sport}, Year={Year}, Week={Week}, Contests={Count}",
                query.Sport, query.SeasonYear, query.Week, contestIds.Count);
            return new Failure<ModelLabMatrixDto>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("matchups", "Failed to retrieve matchup data from Producer")]);
        }

        var modelIds = columns.Select(c => c.Id).ToHashSet();

        var captures = await _dataContext.MatchupPreviewPrompts
            .AsNoTracking()
            .Where(x => x.Mode == PreviewGenerationMode.Experiment
                     && x.ModelId != null
                     && contestIds.Contains(x.ContestId))
            .Select(x => new
            {
                x.ContestId,
                x.ModelId,
                x.PredictedStraightUpWinnerId,
                x.PredictedSpreadWinnerId,
                x.ResponseValidationErrors,
                x.CreatedUtc
            })
            .ToListAsync(cancellationToken);

        // Latest run wins per (contest, model) — reruns supersede, history
        // stays on the capture rows for the Preview Lab drill-down.
        var latestCells = captures
            .Where(c => modelIds.Contains(c.ModelId!.Value))
            .GroupBy(c => (c.ContestId, c.ModelId))
            .Select(g => g.OrderByDescending(c => c.CreatedUtc).First())
            .ToLookup(c => c.ContestId);

        var contests = matchupsResult.Value
            .OrderBy(m => m.StartDateUtc).ThenBy(m => m.Away)
            .Select(m => new ModelLabMatrixDto.MatrixContestDto
            {
                ContestId = m.ContestId,
                StartDateUtc = m.StartDateUtc,
                Away = m.Away,
                AwayShort = m.AwayShort,
                AwayFranchiseSeasonId = m.AwayFranchiseSeasonId,
                Home = m.Home,
                HomeShort = m.HomeShort,
                HomeFranchiseSeasonId = m.HomeFranchiseSeasonId,
                Spread = m.SpreadCurrent,
                Cells = latestCells[m.ContestId]
                    .Select(c => new ModelLabMatrixDto.MatrixCellDto
                    {
                        ModelId = c.ModelId!.Value,
                        PredictedStraightUpWinnerId = c.PredictedStraightUpWinnerId,
                        PredictedSpreadWinnerId = c.PredictedSpreadWinnerId,
                        Problems = c.ResponseValidationErrors,
                        CreatedUtc = c.CreatedUtc
                    })
                    .ToList()
            })
            .ToList();

        return new Success<ModelLabMatrixDto>(new ModelLabMatrixDto
        {
            Models = columns,
            Contests = contests
        });
    }

    private async Task<List<ModelLabMatrixDto.MatrixModelDto>> GetModelColumnsAsync(
        CancellationToken cancellationToken)
    {
        var candidates = await _dataContext.Models
            .AsNoTracking()
            .Where(m => m.IsActive && m.ModelProvider!.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new { m.Id, m.Name, m.Gateway, m.ModelProvider!.Kind })
            .ToListAsync(cancellationToken);

        // The resolver is the single source of truth for lab reachability —
        // same filter the panel fan-out applies.
        return candidates
            .Where(m => _modelClientResolver.CanResolve(m.Gateway, m.Kind))
            .Select(m => new ModelLabMatrixDto.MatrixModelDto { Id = m.Id, Name = m.Name })
            .ToList();
    }
}
