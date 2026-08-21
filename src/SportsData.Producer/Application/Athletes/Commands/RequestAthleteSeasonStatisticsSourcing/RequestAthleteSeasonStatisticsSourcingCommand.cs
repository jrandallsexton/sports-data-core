using SportsData.Core.Common;

namespace SportsData.Producer.Application.Athletes.Commands.RequestAthleteSeasonStatisticsSourcing;

/// <param name="SeasonType">ESPN season type scope for the statistics URL:
/// 2 = regular season, 3 = through postseason (default — full-season
/// totals, what the lineup picker wants for "last season").</param>
public record RequestAthleteSeasonStatisticsSourcingCommand(
    int SeasonYear,
    Sport Sport,
    int SeasonType = 3,
    Guid? CorrelationId = null);
