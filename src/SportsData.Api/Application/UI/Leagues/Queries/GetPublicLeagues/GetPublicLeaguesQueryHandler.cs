using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetPublicLeagues;

public interface IGetPublicLeaguesQueryHandler
{
    Task<Result<List<PublicLeagueDto>>> ExecuteAsync(GetPublicLeaguesQuery query, CancellationToken cancellationToken = default);
}

public class GetPublicLeaguesQueryHandler : IGetPublicLeaguesQueryHandler
{
    private readonly ILogger<GetPublicLeaguesQueryHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetPublicLeaguesQueryHandler(
        ILogger<GetPublicLeaguesQueryHandler> logger,
        AppDataContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<List<PublicLeagueDto>>> ExecuteAsync(
        GetPublicLeaguesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting public leagues for user {UserId}", query.UserId);

        // Deactivated leagues are finished seasons — never browsable.
        var leagues = await _dbContext.PickemGroups
            .AsNoTracking()
            .Where(g => g.IsPublic
                && g.DeactivatedUtc == null
                && !g.Members.Any(x => x.UserId == query.UserId))
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                Commissioner = x.CommissionerUser != null ? x.CommissionerUser.DisplayName : null,
                x.RankingFilter,
                x.PickType,
                x.UseConfidencePoints,
                x.DropLowWeeksCount,
                x.Sport,
                x.League,
                x.SeasonYear,
                x.StartsOn,
                x.EndsOn,
                x.TiebreakerType,
                x.TiebreakerTiePolicy,
                x.JoinPolicy,
                x.InvitationsExpireUtc,
                MemberCount = x.Members.Count,
                // Fallback for rows the expiry sweep hasn't reached yet.
                FirstGameUtc = _dbContext.PickemGroupMatchups
                    .Where(m => m.GroupId == x.Id)
                    .Select(m => (DateTime?)m.StartDateUtc)
                    .Min()
            })
            .ToListAsync(cancellationToken);

        var now = _dateTimeProvider.UtcNow();
        var result = leagues.Select(x =>
        {
            // Stored expiry is the authority; derived first-game only covers
            // the uncomputed gap for CloseAtFirstGame leagues.
            var closesAtUtc = x.InvitationsExpireUtc
                ?? (x.JoinPolicy == JoinPolicy.CloseAtFirstGame ? x.FirstGameUtc : null);
            return new PublicLeagueDto
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
            };
        })
        // Browse answers "what can I join?" — expired leagues are noise, not
        // advertising. (v1 badged them instead; superseded once stored
        // expiries made ended-but-not-yet-deactivated leagues evaluate as
        // closed, which filled the page with unjoinable rows. Urgency for
        // soon-closing leagues is the countdown's job.) IsJoinable stays on
        // the DTO as a belt-and-braces signal for clients.
        .Where(x => x.IsJoinable)
        .ToList();

        _logger.LogInformation(
            "Found {Count} public leagues for user {UserId}",
            result.Count,
            query.UserId);

        return new Success<List<PublicLeagueDto>>(result);
    }
}
