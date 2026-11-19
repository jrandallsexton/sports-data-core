using FluentValidation;

using SportsData.Core.Common;

namespace SportsData.Producer.Application.FranchiseContestFacts.Queries.GetFranchiseSplits;

public class GetFranchiseSplitsQueryValidator : AbstractValidator<GetFranchiseSplitsQuery>
{
    public GetFranchiseSplitsQueryValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Slug is required (at most 100 characters)");

        RuleFor(x => x.SeasonFrom)
            .GreaterThanOrEqualTo(2000)
            .WithMessage("SeasonFrom must be 2000 or later");

        RuleFor(x => x.SeasonTo)
            .GreaterThanOrEqualTo(x => x.SeasonFrom)
            .WithMessage("SeasonTo must not be before SeasonFrom")
            .Must(year => year <= dateTimeProvider.UtcNow().Year + 1)
            .WithMessage("SeasonTo cannot be more than one year in the future");

        RuleFor(x => x.Site).IsInEnum().When(x => x.Site.HasValue);
        RuleFor(x => x.OpponentRank).IsInEnum().When(x => x.OpponentRank.HasValue);
        RuleFor(x => x.Line).IsInEnum().When(x => x.Line.HasValue);
        RuleFor(x => x.KickoffWindow).IsInEnum().When(x => x.KickoffWindow.HasValue);
        RuleFor(x => x.SeasonType).IsInEnum().When(x => x.SeasonType.HasValue);
        RuleFor(x => x.SpreadSize).IsInEnum().When(x => x.SpreadSize.HasValue);

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .When(x => x.Month.HasValue)
            .WithMessage("Month must be 1-12");

        RuleFor(x => x.Opponent)
            .MaximumLength(100)
            .WithMessage("Opponent must be at most 100 characters");

        RuleFor(x => x.OpponentConference)
            .MaximumLength(100)
            .WithMessage("OpponentConference must be at most 100 characters");
    }
}
