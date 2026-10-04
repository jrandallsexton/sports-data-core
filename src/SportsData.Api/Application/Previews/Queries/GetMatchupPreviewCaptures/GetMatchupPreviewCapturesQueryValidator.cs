using FluentValidation;

namespace SportsData.Api.Application.Previews.Queries.GetMatchupPreviewCaptures;

public class GetMatchupPreviewCapturesQueryValidator : AbstractValidator<GetMatchupPreviewCapturesQuery>
{
    public GetMatchupPreviewCapturesQueryValidator()
    {
        RuleFor(x => x.ContestId)
            .NotEmpty()
            .WithMessage("Contest ID cannot be empty");
    }
}
