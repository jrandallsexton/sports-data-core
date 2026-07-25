using System.Globalization;

using Microsoft.Extensions.Options;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Config;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues;

/// <summary>
/// Answers "is league creation open for this sport?" from the API-owned
/// <see cref="ApiConfig.LeagueCreationOpensUtc"/> gate config. League-creation
/// availability is a Pick'em rule and lives entirely in the API — it is not
/// canonical season data. One source consumed by both the create-time guard
/// (<see cref="Commands.CreateLeagueCommandHandlerBase{TRequest}"/>) and the
/// FE-facing endpoint. See docs/features/league-creation-availability-gate.md.
/// </summary>
public interface ILeagueCreationAvailability
{
    /// <summary>
    /// The future UTC instant at which the sport's league creation opens, or
    /// <c>null</c> if it is open now (no gate, or the gate has already elapsed).
    /// </summary>
    DateTime? GetOpensUtc(Sport sport);

    /// <summary>
    /// Every sport currently gated (open instant still in the future), earliest
    /// first. Sports not returned are open.
    /// </summary>
    IReadOnlyList<LeagueCreationGateDto> GetActiveGates();
}

public class LeagueCreationAvailability : ILeagueCreationAvailability
{
    private readonly IReadOnlyDictionary<Sport, DateTime> _opensUtc;
    private readonly IDateTimeProvider _dateTimeProvider;

    public LeagueCreationAvailability(
        IOptions<ApiConfig> config,
        IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
        _opensUtc = ParseGates(config.Value.LeagueCreationOpensUtc);
    }

    // Config is a string→string map keyed by Sport name (the binder populates
    // string-keyed dictionaries reliably; enum-keyed ones bind empty). Parse once:
    // "FootballNcaa" → Sport, the value → a UTC instant. Silently drop unknown
    // sport names / unparseable dates so a stray config value can't break creation.
    private static IReadOnlyDictionary<Sport, DateTime> ParseGates(
        IReadOnlyDictionary<string, string>? configured)
    {
        var parsed = new Dictionary<Sport, DateTime>();
        if (configured is null)
            return parsed;

        foreach (var (name, value) in configured)
        {
            if (!Enum.TryParse<Sport>(name, ignoreCase: true, out var sport))
                continue;

            // AssumeUniversal: a bare "2026-08-17T00:00:00" is the intended UTC
            // instant. AdjustToUniversal normalizes a "…Z"/offset value to UTC.
            // Either way the result is Kind=Utc, so comparisons against UtcNow()
            // are by instant and never shifted by the host timezone.
            if (DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var opens))
            {
                parsed[sport] = opens;
            }
        }

        return parsed;
    }

    public DateTime? GetOpensUtc(Sport sport)
    {
        if (!_opensUtc.TryGetValue(sport, out var opensUtc))
            return null;

        return opensUtc > _dateTimeProvider.UtcNow() ? opensUtc : null;
    }

    public IReadOnlyList<LeagueCreationGateDto> GetActiveGates()
    {
        var now = _dateTimeProvider.UtcNow();

        return _opensUtc
            .Where(kvp => kvp.Value > now)
            .OrderBy(kvp => kvp.Value)
            .Select(kvp => new LeagueCreationGateDto(kvp.Key.ToString(), kvp.Value))
            .ToList();
    }
}
