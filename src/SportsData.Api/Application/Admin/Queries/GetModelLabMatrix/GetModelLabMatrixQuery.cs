using FluentValidation;

using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetModelLabMatrix;

/// <summary>
/// One week of the Model Consensus Lab matrix: every contest any pick'em
/// league carries for (sport, season, week) x every active lab-reachable
/// model. See docs/features/model-consensus-lab.md.
/// </summary>
public class GetModelLabMatrixQuery
{
    public Sport Sport { get; set; }

    public int SeasonYear { get; set; }

    public int Week { get; set; }
}

public class GetModelLabMatrixQueryValidator : AbstractValidator<GetModelLabMatrixQuery>
{
    public GetModelLabMatrixQueryValidator()
    {
        RuleFor(x => x.SeasonYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Week).InclusiveBetween(1, 30);
    }
}
