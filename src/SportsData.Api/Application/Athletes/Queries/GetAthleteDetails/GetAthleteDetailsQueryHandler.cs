using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.Athletes.Queries.GetAthleteDetails;

public interface IGetAthleteDetailsQueryHandler
{
    Task<Result<AthleteDetailDto>> ExecuteAsync(
        GetAthleteDetailsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Relay to Producer's athlete drill-down. Resolved through the franchise
/// client factory — the athlete read lives on the same Producer instance
/// the franchise reads target, and the dormant Player service's client is
/// dead code, so a dedicated athlete client would be a factory chain with
/// one method.
/// </summary>
public class GetAthleteDetailsQueryHandler : IGetAthleteDetailsQueryHandler
{
    private readonly ILogger<GetAthleteDetailsQueryHandler> _logger;
    private readonly IFranchiseClientFactory _franchiseClientFactory;

    public GetAthleteDetailsQueryHandler(
        ILogger<GetAthleteDetailsQueryHandler> logger,
        IFranchiseClientFactory franchiseClientFactory)
    {
        _logger = logger;
        _franchiseClientFactory = franchiseClientFactory;
    }

    public async Task<Result<AthleteDetailDto>> ExecuteAsync(
        GetAthleteDetailsQuery query,
        CancellationToken cancellationToken = default)
    {
        Sport mode;
        try
        {
            mode = ModeMapper.ResolveMode(query.Sport, query.League);
        }
        catch (NotSupportedException)
        {
            // Bad route segments — surface as 400 (Validation), not 500.
            return new Failure<AthleteDetailDto>(
                default!,
                ResultStatus.Validation,
                [
                    new ValidationFailure(nameof(query.Sport), $"Unsupported sport: '{query.Sport}'"),
                    new ValidationFailure(nameof(query.League), $"Unsupported league: '{query.League}'")
                ]);
        }

        try
        {
            var client = _franchiseClientFactory.Resolve(mode);
            return await client.GetAthleteDetails(query.AthleteId, cancellationToken);
        }
        catch (Exception ex)
        {
            // CWE-117: route segments are user-controlled — strip CR/LF before
            // plain-text log sinks (Seq is already safe via JSON escaping).
            _logger.LogError(
                ex,
                "Error retrieving athlete details for sport={Sport}, league={League}, athleteId={AthleteId}",
                SanitizeForLog(query.Sport), SanitizeForLog(query.League), query.AthleteId);

            return new Failure<AthleteDetailDto>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("AthleteDetails", "Error retrieving athlete details. Please try again later.")]);
        }
    }

    private static string SanitizeForLog(string? value) =>
        value is null ? string.Empty : value.Replace("\r", string.Empty).Replace("\n", string.Empty);
}
