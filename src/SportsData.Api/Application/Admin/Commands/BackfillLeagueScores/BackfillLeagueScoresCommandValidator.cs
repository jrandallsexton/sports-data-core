using FluentValidation;

namespace SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;

public class BackfillLeagueScoresCommandValidator : AbstractValidator<BackfillLeagueScoresCommand>
{
    public BackfillLeagueScoresCommandValidator()
    {
        RuleFor(x => x.SeasonYear)
            .GreaterThan(2000)
            .WithMessage("Season year must be greater than 2000")
            .LessThanOrEqualTo(DateTime.UtcNow.Year + 1)
            .WithMessage("Season year cannot be more than one year in the future");
    }
}
