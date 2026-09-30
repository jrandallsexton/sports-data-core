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
    private readonly ILogger<ApplyMatchupOddsPricingHandler> _logger;

    public ApplyMatchupOddsPricingHandler(
        AppDataContext dataContext,
        IContestClientFactory contestClientFactory,
        ILogger<ApplyMatchupOddsPricingHandler> logger)
    {
        _dataContext = dataContext;
        _contestClientFactory = contestClientFactory;
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
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Odds pricing applied to {Count} matchups.", matchups.Count);
    }
}
