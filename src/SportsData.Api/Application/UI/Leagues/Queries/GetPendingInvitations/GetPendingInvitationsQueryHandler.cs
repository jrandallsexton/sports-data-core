using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetPendingInvitations;

public interface IGetPendingInvitationsQueryHandler
{
    Task<Result<List<PendingInvitationDto>>> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Pending league invitations for the current user — the "Pending
/// Invitations" card on the web + mobile home pages. A row qualifies when it
/// has no accept/decline stamp, isn't revoked, its league is still active and
/// still joinable, and the user hasn't already become a member through some
/// other path (public join, invite link).
///
/// Each row embeds a full PublicLeagueDto (the invited league's parameters)
/// so clients reuse the join-confirmation dialog from public discovery. The
/// closesAtUtc / isJoinable derivation mirrors GetPublicLeaguesQueryHandler:
/// stored expiry is the authority; derived first-game covers the uncomputed
/// gap for CloseAtFirstGame leagues (and NOT FullSeason+drop-week leagues,
/// where the calculator's week-(N+1) override applies).
/// </summary>
public class GetPendingInvitationsQueryHandler : IGetPendingInvitationsQueryHandler
{
    private readonly AppDataContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetPendingInvitationsQueryHandler(
        AppDataContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<List<PendingInvitationDto>>> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.PickemGroupInvitations
            .AsNoTracking()
            .Where(i =>
                i.InviteeUserId == userId &&
                i.AcceptedUtc == null &&
                i.DeclinedUtc == null &&
                !i.IsRevoked &&
                i.Group.DeactivatedUtc == null &&
                !i.Group.Members.Any(m => m.UserId == userId))
            .OrderByDescending(i => i.CreatedUtc)
            .Select(i => new
            {
                InvitationId = i.Id,
                InvitedBy = i.InvitedByUser.DisplayName,
                InvitedUtc = i.CreatedUtc,
                i.Group.Id,
                i.Group.Name,
                i.Group.Description,
                Commissioner = i.Group.CommissionerUser != null
                    ? i.Group.CommissionerUser.DisplayName
                    : null,
                i.Group.RankingFilter,
                i.Group.PickType,
                i.Group.UseConfidencePoints,
                i.Group.DropLowWeeksCount,
                i.Group.Sport,
                i.Group.League,
                i.Group.SeasonYear,
                i.Group.StartsOn,
                i.Group.EndsOn,
                i.Group.TiebreakerType,
                i.Group.TiebreakerTiePolicy,
                i.Group.LeagueWindow,
                i.Group.JoinPolicy,
                i.Group.InvitationsExpireUtc,
                MemberCount = i.Group.Members.Count,
                // Fallback for rows the expiry sweep hasn't reached yet.
                FirstGameUtc = _dbContext.PickemGroupMatchups
                    .Where(m => m.GroupId == i.Group.Id)
                    .Select(m => (DateTime?)m.StartDateUtc)
                    .Min()
            })
            .ToListAsync(cancellationToken);

        var now = _dateTimeProvider.UtcNow();
        var invitations = rows.Select(x =>
        {
            var dropWeekOverride = x.LeagueWindow == LeagueWindow.FullSeason
                && x.DropLowWeeksCount is > 0;
            var closesAtUtc = x.InvitationsExpireUtc
                ?? (x.JoinPolicy == JoinPolicy.CloseAtFirstGame && !dropWeekOverride
                    ? x.FirstGameUtc : null);
            return new PendingInvitationDto
            {
                InvitationId = x.InvitationId,
                InvitedBy = x.InvitedBy,
                InvitedUtc = x.InvitedUtc,
                League = new PublicLeagueDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description ?? string.Empty,
                    Commissioner = x.Commissioner ?? "Unknown",
                    RankingFilter = (int?)x.RankingFilter ?? 0,
                    PickType = (int)x.PickType,
                    UseConfidencePoints = x.UseConfidencePoints,
                    DropLowWeeksCount = x.DropLowWeeksCount ?? 0,
                    TiebreakerType = x.TiebreakerType.ToString(),
                    TiebreakerTiePolicy = x.TiebreakerTiePolicy.ToString(),
                    Sport = x.Sport,
                    League = x.League,
                    SeasonYear = x.SeasonYear,
                    MemberCount = x.MemberCount,
                    StartsOn = x.StartsOn,
                    EndsOn = x.EndsOn,
                    JoinPolicy = x.JoinPolicy,
                    ClosesAtUtc = closesAtUtc,
                    IsJoinable = closesAtUtc is null || closesAtUtc > now
                }
            };
        })
        // An invite to a closed league isn't actionable — accept would be
        // rejected by the join gates. Drop it rather than badging.
        .Where(x => x.League.IsJoinable)
        .ToList();

        return new Success<List<PendingInvitationDto>>(invitations);
    }
}
