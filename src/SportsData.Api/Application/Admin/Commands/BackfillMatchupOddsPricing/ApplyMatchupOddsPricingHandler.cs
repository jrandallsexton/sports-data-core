using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Admin.Commands.BackfillMatchupOddsPricing;

public interface IApplyMatchupOddsPricing
{
    Task Process(ApplyMatchupOddsPricingCommand command);
}

/// <summary>
/// Hangfire job: prices ONE contest from its sport's Producer
/// (GET contests/{id}/odds-pricing) and writes the values onto every
/// PickemGroupMatchup carrying that contest in a league of that sport.
///
/// A value is written only when the Producer supplies one (never erases), so a
/// contest whose odds are gone upstream keeps what it had. NotFound (the
/// contest is not in that Producer) is logged and skipped: retrying cannot
/// help. Any other failure THROWS so Hangfire retries this one contest.
/// </summary>
public class ApplyMatchupOddsPricingHandler : IApplyMatchupOddsPricing
{
    private readonly AppDataContext _dataContext;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ApplyMatchupOddsPricingHandler> _logger;

    public ApplyMatchupOddsPricingHandler(
        AppDataContext dataContext,
        IContestClientFactory contestClientFactory,
        IDateTimeProvider dateTimeProvider,
        ILogger<ApplyMatchupOddsPricingHandler> logger)
    {
        _dataContext = dataContext;
        _contestClientFactory = contestClientFactory;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task Process(ApplyMatchupOddsPricingCommand command)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId,
            ["ContestId"] = command.ContestId,
            ["Sport"] = command.Sport
        });

        var result = await _contestClientFactory
            .Resolve(command.Sport)
            .GetOddsPricingByContestId(command.ContestId);

        if (result is Failure<OddsPricingDto> { Status: ResultStatus.NotFound })
        {
            _logger.LogWarning("Odds pricing: contest not found in the {Sport} Producer; matchups left unchanged.", command.Sport);
            return;
        }

        if (result is not Success<OddsPricingDto> { Value: { } pricing })
        {
            var errors = result is Failure<OddsPricingDto> failure
                ? string.Join("; ", failure.Errors.Select(e => e.ErrorMessage))
                : "no pricing returned";
            throw new InvalidOperationException(
                $"Odds pricing fetch failed for contest {command.ContestId} ({command.Sport}): {errors}");
        }

        // Keyed by contest alone, no season predicate, BY DESIGN (Vortex, #802,
        // raised twice; settled here). A ContestId is the hash of the ESPN event
        // URL, whose normalized PATH is .../leagues/{league}/events/{espnEventId}:
        // the path carries ESPN's event id, so one ContestId is one game. A
        // rematch, in any season, is a new ESPN event with a new id, hence a new
        // path and a new ContestId; nothing season-shaped is dropped by the
        // normalization (it strips only the query string). Verified on prod
        // copies 2026-09-30: contests = distinct URL hashes = distinct event ids
        // (NCAA 80,840 / NFL 9,202 / MLB 30,532), 0 event ids spanning more than
        // one season, and max seasons per ContestId across PickemGroupMatchup = 1.
        var matchups = await _dataContext.PickemGroupMatchups
            .Where(m => m.ContestId == command.ContestId
                     && _dataContext.PickemGroups.Any(g => g.Id == m.GroupId && g.Sport == command.Sport))
            .ToListAsync();

        foreach (var m in matchups)
        {
            m.AwayMoneyLine = pricing.AwayMoneyLine ?? m.AwayMoneyLine;
            m.HomeMoneyLine = pricing.HomeMoneyLine ?? m.HomeMoneyLine;
            m.AwaySpreadPrice = (double?)pricing.AwaySpreadPrice ?? m.AwaySpreadPrice;
            m.HomeSpreadPrice = (double?)pricing.HomeSpreadPrice ?? m.HomeSpreadPrice;
            m.OverOdds = (double?)pricing.OverOdds ?? m.OverOdds;
            m.UnderOdds = (double?)pricing.UnderOdds ?? m.UnderOdds;

            // Stamp only when EF detected a change, as MatchupOddsProcessor and
            // MatchupScheduleProcessor do: a rewrite must leave a trace, a
            // no-op (re-run) must not.
            if (_dataContext.Entry(m).State == EntityState.Modified)
            {
                m.ModifiedUtc = _dateTimeProvider.UtcNow();
                m.ModifiedBy = Guid.Empty;
            }
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Odds pricing applied to {Count} matchups.", matchups.Count);
    }
}
