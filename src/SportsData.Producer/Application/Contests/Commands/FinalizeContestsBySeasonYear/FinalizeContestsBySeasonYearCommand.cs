using SportsData.Core.Common;

namespace SportsData.Producer.Application.Contests.Commands.FinalizeContestsBySeasonYear;

public record FinalizeContestsBySeasonYearCommand
{
    public Sport Sport { get; init; }

    public int SeasonYear { get; init; }

    public bool ReprocessEnriched { get; init; }

    public Guid CorrelationId { get; init; } = Guid.Empty;
}