using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetPublicLeagues;

public interface IGetPublicLeaguesQueryHandler
{
    Task<Result<List<PublicLeagueDto>>> ExecuteAsync(GetPublicLeaguesQuery query, CancellationToken cancellationToken = default);
}

public class GetPublicLeaguesQueryHandler : IGetPublicLeaguesQueryHandler
{
    private readonly ILogger<GetPublicLeaguesQueryHandler> _logger;
    private readonly AppDataContext _dbContext;

    public GetPublicLeaguesQueryHandler(
        ILogger<GetPublicLeaguesQueryHandler> logger,
        AppDataContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<Result<List<PublicLeagueDto>>> ExecuteAsync(
        GetPublicLeaguesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting public leagues for user {UserId}", query.UserId);

        var leagues = await _dbContext.PickemGroups
            .Include(g => g.CommissionerUser)
            .Include(g => g.Members)
            .Where(g => g.IsPublic && !g.Members.Any(x => x.UserId == query.UserId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var result = leagues.Select(x => new PublicLeagueDto
        {
            Id = x.Id,
            Name = x.Name,
            Description = x.Description ?? string.Empty,
            Commissioner = x.CommissionerUser.DisplayName,
            RankingFilter = (int?)x.RankingFilter ?? 0,
            PickType = (int)x.PickType,
            UseConfidencePoints = x.UseConfidencePoints,
            DropLowWeeksCount = x.DropLowWeeksCount ?? 0
        }).ToList();

        _logger.LogInformation(
            "Found {Count} public leagues for user {UserId}",
            result.Count,
            query.UserId);

        return new Success<List<PublicLeagueDto>>(result);
    }
}
