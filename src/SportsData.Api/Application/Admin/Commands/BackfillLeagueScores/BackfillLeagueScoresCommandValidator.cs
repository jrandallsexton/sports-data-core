using FluentValidation;

namespace SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;

public class BackfillLeagueScoresCommandValidator : AbstractValidator<BackfillLeagueScoresCommand>
{
    /// <summary>
    /// Initializes a new instance of <see cref="BackfillLeagueScoresCommandValidator"/> and configures validation rules.
    /// </summary>
    /// <remarks>
    /// Ensures <see cref="BackfillLeagueScoresCommand.SeasonYear"/> is greater than 2000 and less than or equal to the current UTC year plus one, with specific error messages for each constraint.
    /// </remarks>
    public BackfillLeagueScoresCommandValidator()
    {
        RuleFor(x => x.SeasonYear)
            .GreaterThan(2000)
            .WithMessage("Season year must be greater than 2000")
            .LessThanOrEqualTo(DateTime.UtcNow.Year + 1)
            .WithMessage("Season year cannot be more than one year in the future");
    }
}