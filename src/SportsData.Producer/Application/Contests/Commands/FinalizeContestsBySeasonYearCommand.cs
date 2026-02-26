using SportsData.Core.Common;

namespace SportsData.Producer.Application.Contests.Commands;

public class FinalizeContestsBySeasonYearCommand
{
    public Sport Sport { get; init; }

    public int SeasonYear { get; init; }

    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}