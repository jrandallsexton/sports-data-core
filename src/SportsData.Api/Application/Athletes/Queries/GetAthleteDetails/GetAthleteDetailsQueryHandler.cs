using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Athlete;

namespace SportsData.Api.Application.Athletes.Queries.GetAthleteDetails;

public interface IGetAthleteDetailsQueryHandler
{
    Task<Result<AthleteDetailDto>> ExecuteAsync(
        GetAthleteDetailsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Relay to Producer's athlete drill-down via the athlete client. This
/// originally rode the franchise client factory ("a dedicated athlete
/// client would be a factory chain with one method") — the pickem grid
/// feed made it two methods and athlete reads became their own aggregate
/// root. The client still resolves to the same Producer instance the
/// franchise reads target; the dormant Player service's client stays
/// untouched.
/// </summary>
public class GetAthleteDetailsQueryHandler : IGetAthleteDetailsQueryHandler
{
    private readonly ILogger<GetAthleteDetailsQueryHandler> _logger;
    private readonly IAthleteClientFactory _athleteClientFactory;

    public GetAthleteDetailsQueryHandler(
        ILogger<GetAthleteDetailsQueryHandler> logger,
        IAthleteClientFactory athleteClientFactory)
    {
        _logger = logger;
        _athleteClientFactory = athleteClientFactory;
    }

    public async Task<Result<AthleteDetailDto>> ExecuteAsync(
        GetAthleteDetailsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.AthleteId == Guid.Empty)
        {
            // The :guid route constraint admits Guid.Empty. It can never
            // identify a record, so it is a malformed request (400), not a
            // miss (404).
            return new Failure<AthleteDetailDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.AthleteId), "AthleteId is required")]);
        }

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
            var client = _athleteClientFactory.Resolve(mode);
            return await client.GetAthleteDetails(query.AthleteId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller went away — that is not a server error. Without this
            // the generic catch below would log it and return Error.
            throw;
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
