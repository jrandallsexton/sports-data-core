using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueById;

public interface IGetLeagueByIdQueryHandler
{
    Task<Result<LeagueDetailDto>> ExecuteAsync(GetLeagueByIdQuery query, CancellationToken cancellationToken = default);
}

public class GetLeagueByIdQueryHandler : IGetLeagueByIdQueryHandler
{
    private readonly AppDataContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetLeagueByIdQueryHandler(
        AppDataContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
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

        // Tiered response (docs/audit/league-authorization-idor.md). A league id
        // is an identifier, not a secret — it travels in invite links and share
        // sheets — so the ROSTER (real people's display names) is withheld from
        // non-members. Settings and size are not withheld: a public league
        // advertises them by design, and the invite-preview screen needs them
        // to show a prospective member what they'd be joining.
        var isMember = league.Members.Any(m => m.UserId == query.UserId);

        // Stored expiry (LeagueJoinExpiryCalculator) is the authority; the
        // derived first-game query only covers the uncomputed gap for
        // CloseAtFirstGame leagues (fresh slate, pre-backfill rows).
        var closesAtUtc = league.InvitationsExpireUtc;
        // Drop-week exclusion mirrors LeagueJoinExpiryCalculator: first-game
        // is the wrong close moment for FullSeason+drop-week leagues.
        if (closesAtUtc is null
            && league.JoinPolicy == JoinPolicy.CloseAtFirstGame
            && !(league.LeagueWindow == LeagueWindow.FullSeason && league.DropLowWeeksCount is > 0))
        {
            closesAtUtc = await _dbContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(m => m.GroupId == league.Id)
                .Select(m => (DateTime?)m.StartDateUtc)
                .MinAsync(cancellationToken);
        }

        var isJoinable = league.DeactivatedUtc is null
            && (closesAtUtc is null || closesAtUtc > _dateTimeProvider.UtcNow());

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
            StartsOn = league.StartsOn,
            EndsOn = league.EndsOn,
            DeactivatedUtc = league.DeactivatedUtc,
            // Always populated — clients render "N members" without needing the
            // roster itself.
            MemberCount = league.Members.Count,
            IsMember = isMember,
            JoinPolicy = league.JoinPolicy.ToString().ToLowerInvariant(),
            ClosesAtUtc = closesAtUtc,
            IsJoinable = isJoinable,
            Members = isMember
                ? league.Members.Select(m => new LeagueDetailDto.LeagueMemberDto
                {
                    UserId = m.UserId,
                    Username = m.User?.DisplayName ?? "UNKNOWN",
                    Role = m.Role.ToString().ToLowerInvariant()
                }).ToList()
                : []
        };

        return new Success<LeagueDetailDto>(dto);
    }
}
