using FluentValidation;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsBySeasonWeekId;

/// <summary>
/// An empty GUID matches no SeasonWeek and would silently return an
/// empty slate; reject it up front (same rationale as the number-based
/// query's validator).
/// </summary>
public class GetMatchupsBySeasonWeekIdQueryValidator
    : AbstractValidator<GetMatchupsBySeasonWeekIdQuery>
{
    public GetMatchupsBySeasonWeekIdQueryValidator()
    {
        RuleFor(x => x.SeasonWeekId)
            .NotEmpty()
            .WithMessage("SeasonWeekId is required");
    }
}
