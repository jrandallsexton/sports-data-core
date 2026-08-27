using FluentValidation;
using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Athlete;

namespace SportsData.Api.Application.Athletes.Queries.GetPickemAthletes;

public interface IGetPickemAthletesQueryHandler
{
    Task<Result<AthleteMatchupSummariesDto>> ExecuteAsync(
        GetPickemAthletesQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Relay to Producer's roster-builder grid feed via the athlete client —
/// athlete reads are their own aggregate root even though they resolve to
/// the same Producer instance as franchise reads. Position validation
/// lives in the Producer handler — this relay only validates the route
/// segments it owns.
/// </summary>
public class GetPickemAthletesQueryHandler : IGetPickemAthletesQueryHandler
{
    private readonly ILogger<GetPickemAthletesQueryHandler> _logger;
    private readonly IAthleteClientFactory _athleteClientFactory;
    private readonly IValidator<GetPickemAthletesQuery> _validator;

    public GetPickemAthletesQueryHandler(
        ILogger<GetPickemAthletesQueryHandler> logger,
        IAthleteClientFactory athleteClientFactory,
        IValidator<GetPickemAthletesQuery> validator)
    {
        _logger = logger;
        _athleteClientFactory = athleteClientFactory;
        _validator = validator;
    }

    public async Task<Result<AthleteMatchupSummariesDto>> ExecuteAsync(
        GetPickemAthletesQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<AthleteMatchupSummariesDto>(
                default!,
                ResultStatus.Validation,
                validation.Errors);
        }

        Sport mode;
        try
        {
            mode = ModeMapper.ResolveMode(query.Sport, query.League);
        }
        catch (NotSupportedException)
        {
            return new Failure<AthleteMatchupSummariesDto>(
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
            var phaseTypeCode = query.Phase switch
            {
                "preseason" => 1,
                "postseason" => 3,
                _ => 2,
            };
            return await client.GetAthleteMatchupSummaries(
                query.Position, query.SeasonYear, query.Week, phaseTypeCode, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // CWE-117: route/query values are user-controlled — strip CR/LF
            // before plain-text log sinks.
            _logger.LogError(
                ex,
                "Error retrieving pickem athletes for sport={Sport}, league={League}, position={Position}, seasonYear={SeasonYear}, week={Week}",
                SanitizeForLog(query.Sport), SanitizeForLog(query.League), SanitizeForLog(query.Position),
                query.SeasonYear, query.Week);

            return new Failure<AthleteMatchupSummariesDto>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("PickemAthletes", "Error retrieving athletes. Please try again later.")]);
        }
    }

    private static string SanitizeForLog(string? value) =>
        value is null ? string.Empty : value.Replace("\r", string.Empty).Replace("\n", string.Empty);
}
