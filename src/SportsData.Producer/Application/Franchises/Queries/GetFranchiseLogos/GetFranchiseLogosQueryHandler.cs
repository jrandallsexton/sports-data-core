using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Franchises.Queries.GetFranchiseLogos;

public interface IGetFranchiseLogosQueryHandler
{
    Task<Result<FranchiseLogosDto>> ExecuteAsync(GetFranchiseLogosQuery query, CancellationToken cancellationToken = default);
}

public class GetFranchiseLogosQueryHandler : IGetFranchiseLogosQueryHandler
{
    private readonly TeamSportDataContext _dbContext;

    public GetFranchiseLogosQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<FranchiseLogosDto>> ExecuteAsync(GetFranchiseLogosQuery query, CancellationToken cancellationToken = default)
    {
        var franchise = await _dbContext.Franchises
            .AsNoTracking()
            .Include(f => f.Logos)
            .FirstOrDefaultAsync(f => f.Slug == query.Slug, cancellationToken);

        if (franchise is null)
        {
            return new Failure<FranchiseLogosDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure("Slug", $"Franchise '{query.Slug}' not found")]);
        }

        var seasonLogos = await _dbContext.FranchiseSeasons
            .AsNoTracking()
            .Where(fs => fs.FranchiseId == franchise.Id)
            .Include(fs => fs.Logos)
            .OrderByDescending(fs => fs.SeasonYear)
            .Select(fs => new SeasonLogosDto
            {
                FranchiseSeasonId = fs.Id,
                SeasonYear = fs.SeasonYear,
                Logos = fs.Logos.Select(l => new LogoItemDto
                {
                    Id = l.Id,
                    Url = l.Uri.OriginalString,
                    Width = l.Width,
                    Height = l.Height,
                    Rel = l.Rel,
                    IsForDarkBg = l.IsForDarkBg
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        var dto = new FranchiseLogosDto
        {
            FranchiseId = franchise.Id,
            FranchiseName = franchise.Name,
            FranchiseLogos = franchise.Logos.Select(l => new LogoItemDto
            {
                Id = l.Id,
                Url = l.Uri.OriginalString,
                Width = l.Width,
                Height = l.Height,
                Rel = l.Rel,
                IsForDarkBg = l.IsForDarkBg
            }).ToList(),
            SeasonLogos = seasonLogos
        };

        return new Success<FranchiseLogosDto>(dto);
    }
}
