using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueById;

public interface IGetLeagueByIdQueryHandler
{
    Task<Result<LeagueDetailDto>> ExecuteAsync(GetLeagueByIdQuery query, CancellationToken cancellationToken = default);
}

public class GetLeagueByIdQueryHandler : IGetLeagueByIdQueryHandler
{
    private readonly AppDataContext _dbContext;

    public GetLeagueByIdQueryHandler(AppDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<LeagueDetailDto>> ExecuteAsync(
        GetLeagueByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var league = await _dbContext.PickemGroups
            .Include(x => x.Conferences)
            .Include(x => x.Members)
            .ThenInclude(m => m.User)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == query.LeagueId, cancellationToken);

        if (league is null)
            return new Failure<LeagueDetailDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.LeagueId), $"League with ID {query.LeagueId} not found.")]);

        var dto = new LeagueDetailDto
        {
            Id = league.Id,
            Name = league.Name,
            Description = league.Description,
            PickType = league.PickType.ToString().ToLowerInvariant(),
            UseConfidencePoints = league.UseConfidencePoints,
            TiebreakerType = league.TiebreakerType.ToString().ToLowerInvariant(),
            TiebreakerTiePolicy = league.TiebreakerTiePolicy.ToString().ToLowerInvariant(),
            RankingFilter = league.RankingFilter.ToString(),
            ConferenceSlugs = league.Conferences?.Select(c => c.ConferenceSlug).ToList() ?? new(),
            IsPublic = league.IsPublic,
            Members = league.Members.Select(m => new LeagueDetailDto.LeagueMemberDto
            {
                UserId = m.UserId,
                Username = m.User?.DisplayName ?? "UNKNOWN",
                Role = m.Role.ToString().ToLowerInvariant()
            }).ToList()
        };

        return new Success<LeagueDetailDto>(dto);
    }
}
