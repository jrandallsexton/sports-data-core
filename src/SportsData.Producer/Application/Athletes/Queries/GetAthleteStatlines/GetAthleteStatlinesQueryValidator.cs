using FluentValidation;

namespace SportsData.Producer.Application.Athletes.Queries.GetAthleteStatlines;

/// <summary>
/// Bounded batch: a lineup has single-digit slots; 100 is generous
/// headroom for future league-wide scoring sweeps while keeping the
/// IN-clauses sane.
/// </summary>
public class GetAthleteStatlinesQueryValidator : AbstractValidator<GetAthleteStatlinesQuery>
{
    public GetAthleteStatlinesQueryValidator()
    {
        // Null-guarded predicates: FluentValidation runs the whole chain
        // even after NotEmpty fails, so a null body list must not NRE
        // inside Must.
        RuleFor(x => x.ContestIds)
            .NotEmpty()
            .Must(x => x is null || x.Count <= 100)
            .WithMessage("ContestIds must contain between 1 and 100 entries");

        RuleFor(x => x.AthleteSeasonIds)
            .NotEmpty()
            .Must(x => x is null || x.Count <= 100)
            .WithMessage("AthleteSeasonIds must contain between 1 and 100 entries");
    }
}
