using Microsoft.EntityFrameworkCore;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Franchises.Queries.GetFranchiseSeasons;

public interface IGetFranchiseSeasonsQueryHandler
{
    Task<Result<List<FranchiseSeasonDto>>> ExecuteAsync(GetFranchiseSeasonsQuery query, CancellationToken cancellationToken = default);
}

public class GetFranchiseSeasonsQueryHandler : IGetFranchiseSeasonsQueryHandler
{
    private readonly TeamSportDataContext _dataContext;
    private readonly ILogger<GetFranchiseSeasonsQueryHandler> _logger;

    public GetFranchiseSeasonsQueryHandler(
        TeamSportDataContext dataContext,
        ILogger<GetFranchiseSeasonsQueryHandler> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task<Result<List<FranchiseSeasonDto>>> ExecuteAsync(
        GetFranchiseSeasonsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetFranchiseSeasons query: franchiseId={FranchiseId}", query.FranchiseId);

        var seasons = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .Where(fs => fs.FranchiseId == query.FranchiseId)
            .OrderByDescending(fs => fs.SeasonYear)
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
                ConferenceTies = fs.ConferenceTies,
                CreatedUtc = fs.CreatedUtc
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} seasons for franchise {FranchiseId}", seasons.Count, query.FranchiseId);

        return new Success<List<FranchiseSeasonDto>>(seasons);
    }
}
