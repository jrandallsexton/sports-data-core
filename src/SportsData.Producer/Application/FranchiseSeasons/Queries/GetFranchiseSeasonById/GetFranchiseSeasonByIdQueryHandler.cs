using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonById;

public interface IGetFranchiseSeasonByIdQueryHandler
{
    Task<Result<FranchiseSeasonDto>> ExecuteAsync(GetFranchiseSeasonByIdQuery query, CancellationToken cancellationToken = default);
}

public class GetFranchiseSeasonByIdQueryHandler : IGetFranchiseSeasonByIdQueryHandler
{
    private readonly FootballDataContext _context;

    public GetFranchiseSeasonByIdQueryHandler(FootballDataContext context)
    {
        _context = context;
    }

    public async Task<Result<FranchiseSeasonDto>> ExecuteAsync(
        GetFranchiseSeasonByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var franchiseSeason = await _context.FranchiseSeasons
            .AsNoTracking()
            .Where(fs => fs.FranchiseId == query.FranchiseId && fs.SeasonYear == query.SeasonYear)
            .Select(fs => new FranchiseSeasonDto
            {
                Id = fs.Id,
                FranchiseId = fs.FranchiseId,
                SeasonYear = fs.SeasonYear,
                Slug = fs.Slug,
                Location = fs.Location,
                Name = fs.Name,
                Abbreviation = fs.Abbreviation,
                DisplayName = fs.DisplayName,
                DisplayNameShort = fs.DisplayNameShort,
                ColorCodeHex = fs.ColorCodeHex,
                ColorCodeAltHex = fs.ColorCodeAltHex,
                IsActive = fs.IsActive,
                Wins = fs.Wins,
                Losses = fs.Losses,
                Ties = fs.Ties,
                ConferenceWins = fs.ConferenceWins,
                ConferenceLosses = fs.ConferenceLosses,
                ConferenceTies = fs.ConferenceTies
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (franchiseSeason == null)
        {
            return new Failure<FranchiseSeasonDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure("SeasonYear", $"Season {query.SeasonYear} not found for franchise {query.FranchiseId}")]);
        }

        return new Success<FranchiseSeasonDto>(franchiseSeason);
    }
}
