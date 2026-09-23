using FluentValidation;
using FluentValidation.Results;

using SportsData.Api.Infrastructure.Refs;
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
/// directly. The Producer echoes back the correlation id the client stamped
/// on the request (X-Correlation-Id), so API and Producer logs share one
/// Seq handle; it is returned in the response.
/// </summary>
public class EnrichFranchiseSeasonCommandHandler : IEnrichFranchiseSeasonCommandHandler
{
    private readonly ILogger<EnrichFranchiseSeasonCommandHandler> _logger;
    private readonly IFranchiseClientFactory _franchiseClientFactory;
    private readonly IGenerateApiResourceRefs _refGenerator;
    private readonly IValidator<EnrichFranchiseSeasonCommand> _validator;

    public EnrichFranchiseSeasonCommandHandler(
        ILogger<EnrichFranchiseSeasonCommandHandler> logger,
        IFranchiseClientFactory franchiseClientFactory,
        IGenerateApiResourceRefs refGenerator,
        IValidator<EnrichFranchiseSeasonCommand> validator)
    {
        _logger = logger;
        _franchiseClientFactory = franchiseClientFactory;
        _refGenerator = refGenerator;
        _validator = validator;
    }

    public async Task<Result<EnrichFranchiseSeasonResponseDto>> ExecuteAsync(
        EnrichFranchiseSeasonCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<EnrichFranchiseSeasonResponseDto>(
                default!, ResultStatus.Validation, validation.Errors);
        }

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

        // Lookups: a Producer outage must not read as "not found". Only a
        // NotFound from the Producer, or a success with no payload, is a 404;
        // every other failure passes through with its own status.
        var franchiseResult = await client.GetFranchiseById(command.FranchiseSlugOrId, cancellationToken);
        if (franchiseResult is Failure<GetFranchiseByIdResponse> { Status: not ResultStatus.NotFound } franchiseFailure)
        {
            return new Failure<EnrichFranchiseSeasonResponseDto>(default!, franchiseFailure.Status, franchiseFailure.Errors);
        }
        if (franchiseResult is not Success<GetFranchiseByIdResponse> { Value.Franchise: { } franchise })
        {
            return new Failure<EnrichFranchiseSeasonResponseDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("FranchiseSlugOrId", $"Franchise '{command.FranchiseSlugOrId}' not found")]);
        }

        var seasonResult = await client.GetFranchiseSeasonById(franchise.Id, command.SeasonYear, cancellationToken);
        if (seasonResult is Failure<GetFranchiseSeasonByIdResponse> { Status: not ResultStatus.NotFound } seasonFailure)
        {
            return new Failure<EnrichFranchiseSeasonResponseDto>(default!, seasonFailure.Status, seasonFailure.Errors);
        }
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

        var selfRef = _refGenerator.ForFranchiseSeason(franchise.Id, command.SeasonYear, command.Sport, command.League);

        return new Success<EnrichFranchiseSeasonResponseDto>(
            new EnrichFranchiseSeasonResponseDto
            {
                Ref = selfRef,
                Links = new Dictionary<string, Uri>
                {
                    ["self"] = selfRef,
                    ["franchise"] = _refGenerator.ForFranchise(franchise.Id, command.Sport, command.League)
                },
                FranchiseId = franchise.Id,
                FranchiseSeasonId = season.Id,
                SeasonYear = command.SeasonYear,
                CorrelationId = correlationId
            },
            ResultStatus.Accepted);
    }
}
