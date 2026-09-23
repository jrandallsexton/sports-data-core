using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.Franchise.Queries;

namespace SportsData.Api.Application.Franchises.Seasons.Commands.EnrichFranchiseSeason;

public interface IEnrichFranchiseSeasonCommandHandler
{
    Task<Result<EnrichFranchiseSeasonResponseDto>> ExecuteAsync(
        EnrichFranchiseSeasonCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves sport/league + slug + season to the Producer's FranchiseSeason
/// (same two-step lookup as GetFranchiseSeasonByIdQueryHandler), then asks
/// that sport's Producer to enrich it. The API never touches Producer data
/// directly; the correlation id in the response is the Producer's Seq
/// handle for the three enrichment legs.
/// </summary>
public class EnrichFranchiseSeasonCommandHandler : IEnrichFranchiseSeasonCommandHandler
{
    private readonly ILogger<EnrichFranchiseSeasonCommandHandler> _logger;
    private readonly IFranchiseClientFactory _franchiseClientFactory;

    public EnrichFranchiseSeasonCommandHandler(
        ILogger<EnrichFranchiseSeasonCommandHandler> logger,
        IFranchiseClientFactory franchiseClientFactory)
    {
        _logger = logger;
        _franchiseClientFactory = franchiseClientFactory;
    }

    public async Task<Result<EnrichFranchiseSeasonResponseDto>> ExecuteAsync(
        EnrichFranchiseSeasonCommand command,
        CancellationToken cancellationToken = default)
    {
        Sport mode;
        try
        {
            mode = ModeMapper.ResolveMode(command.Sport, command.League);
        }
        catch (NotSupportedException ex)
        {
            return new Failure<EnrichFranchiseSeasonResponseDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("Sport/League", ex.Message)]);
        }

        var client = _franchiseClientFactory.Resolve(mode);

        var franchiseResult = await client.GetFranchiseById(command.FranchiseSlugOrId, cancellationToken);
        if (franchiseResult is not Success<GetFranchiseByIdResponse> { Value.Franchise: { } franchise })
        {
            return new Failure<EnrichFranchiseSeasonResponseDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("FranchiseSlugOrId", $"Franchise '{command.FranchiseSlugOrId}' not found")]);
        }

        var seasonResult = await client.GetFranchiseSeasonById(franchise.Id, command.SeasonYear, cancellationToken);
        if (seasonResult is not Success<GetFranchiseSeasonByIdResponse> { Value.Season: { } season })
        {
            return new Failure<EnrichFranchiseSeasonResponseDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("SeasonYear", $"Season {command.SeasonYear} not found for franchise '{franchise.Slug}'")]);
        }

        var enrichResult = await client.EnrichFranchiseSeason(season.Id, cancellationToken);
        if (enrichResult is Failure<Guid> failure)
        {
            _logger.LogError(
                "Producer refused enrichment for FranchiseSeason {FranchiseSeasonId} ({Slug} {SeasonYear}): {Errors}",
                season.Id, franchise.Slug, command.SeasonYear,
                string.Join("; ", failure.Errors.Select(e => e.ErrorMessage)));

            return new Failure<EnrichFranchiseSeasonResponseDto>(
                default!,
                failure.Status,
                failure.Errors);
        }

        var correlationId = enrichResult.Value;

        _logger.LogInformation(
            "Enrichment requested for {Slug} {SeasonYear} (FranchiseSeason {FranchiseSeasonId}). CorrelationId={CorrelationId}",
            franchise.Slug, command.SeasonYear, season.Id, correlationId);

        return new Success<EnrichFranchiseSeasonResponseDto>(
            new EnrichFranchiseSeasonResponseDto
            {
                FranchiseId = franchise.Id,
                FranchiseSeasonId = season.Id,
                SeasonYear = command.SeasonYear,
                CorrelationId = correlationId
            },
            ResultStatus.Accepted);
    }
}
