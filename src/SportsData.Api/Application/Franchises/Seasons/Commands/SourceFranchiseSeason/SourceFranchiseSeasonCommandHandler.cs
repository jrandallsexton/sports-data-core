using FluentValidation;
using FluentValidation.Results;

using SportsData.Api.Infrastructure.Refs;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.Franchise.Queries;

namespace SportsData.Api.Application.Franchises.Seasons.Commands.SourceFranchiseSeason;

public interface ISourceFranchiseSeasonCommandHandler
{
    Task<Result<SourceFranchiseSeasonResponseDto>> ExecuteAsync(
        SourceFranchiseSeasonCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves sport/league + slug + season to the Producer's FranchiseSeason
/// (the same two-step lookup as EnrichFranchiseSeasonCommandHandler), then
/// asks that sport's Producer to re-source it from ESPN. The API never
/// touches Producer data directly. The Producer echoes back the correlation
/// id the client stamped on the request (X-Correlation-Id); it is returned
/// in the response as the Seq handle.
/// </summary>
public class SourceFranchiseSeasonCommandHandler : ISourceFranchiseSeasonCommandHandler
{
    private readonly ILogger<SourceFranchiseSeasonCommandHandler> _logger;
    private readonly IFranchiseClientFactory _franchiseClientFactory;
    private readonly IGenerateApiResourceRefs _refGenerator;
    private readonly IValidator<SourceFranchiseSeasonCommand> _validator;

    public SourceFranchiseSeasonCommandHandler(
        ILogger<SourceFranchiseSeasonCommandHandler> logger,
        IFranchiseClientFactory franchiseClientFactory,
        IGenerateApiResourceRefs refGenerator,
        IValidator<SourceFranchiseSeasonCommand> validator)
    {
        _logger = logger;
        _franchiseClientFactory = franchiseClientFactory;
        _refGenerator = refGenerator;
        _validator = validator;
    }

    public async Task<Result<SourceFranchiseSeasonResponseDto>> ExecuteAsync(
        SourceFranchiseSeasonCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<SourceFranchiseSeasonResponseDto>(
                default!, ResultStatus.Validation, validation.Errors);
        }

        Sport mode;
        try
        {
            mode = ModeMapper.ResolveMode(command.Sport, command.League);
        }
        catch (NotSupportedException ex)
        {
            return new Failure<SourceFranchiseSeasonResponseDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("Sport/League", ex.Message)]);
        }

        var client = _franchiseClientFactory.Resolve(mode);

        // Lookups mirror EnrichFranchiseSeasonCommandHandler: a Producer
        // outage must not read as "not found". Only a NotFound from the
        // Producer, or a success with no payload, is a 404; every other
        // failure passes through with its own status.
        var franchiseResult = await client.GetFranchiseById(command.FranchiseSlugOrId, cancellationToken);
        if (franchiseResult is Failure<GetFranchiseByIdResponse> { Status: not ResultStatus.NotFound } franchiseFailure)
        {
            return new Failure<SourceFranchiseSeasonResponseDto>(default!, franchiseFailure.Status, franchiseFailure.Errors);
        }
        if (franchiseResult is not Success<GetFranchiseByIdResponse> { Value.Franchise: { } franchise })
        {
            return new Failure<SourceFranchiseSeasonResponseDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("FranchiseSlugOrId", $"Franchise '{command.FranchiseSlugOrId}' not found")]);
        }

        var seasonResult = await client.GetFranchiseSeasonById(franchise.Id, command.SeasonYear, cancellationToken);
        if (seasonResult is Failure<GetFranchiseSeasonByIdResponse> { Status: not ResultStatus.NotFound } seasonFailure)
        {
            return new Failure<SourceFranchiseSeasonResponseDto>(default!, seasonFailure.Status, seasonFailure.Errors);
        }
        if (seasonResult is not Success<GetFranchiseSeasonByIdResponse> { Value.Season: { } season })
        {
            return new Failure<SourceFranchiseSeasonResponseDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("SeasonYear", $"Season {command.SeasonYear} not found for franchise '{franchise.Slug}'")]);
        }

        var sourceResult = await client.RequestSingleFranchiseSeasonSourcing(season.Id, cancellationToken);
        if (sourceResult is Failure<Guid> failure)
        {
            _logger.LogError(
                "Producer refused sourcing for FranchiseSeason {FranchiseSeasonId} ({Slug} {SeasonYear}): {Errors}",
                season.Id, franchise.Slug, command.SeasonYear,
                string.Join("; ", failure.Errors.Select(e => e.ErrorMessage)));

            return new Failure<SourceFranchiseSeasonResponseDto>(
                default!,
                failure.Status,
                failure.Errors);
        }

        var correlationId = sourceResult.Value;

        _logger.LogInformation(
            "Sourcing requested for {Slug} {SeasonYear} (FranchiseSeason {FranchiseSeasonId}). CorrelationId={CorrelationId}",
            franchise.Slug, command.SeasonYear, season.Id, correlationId);

        var selfRef = _refGenerator.ForFranchiseSeason(franchise.Id, command.SeasonYear, command.Sport, command.League);

        return new Success<SourceFranchiseSeasonResponseDto>(
            new SourceFranchiseSeasonResponseDto
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
