using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.User.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.User.Queries.GetNotificationPreferences;

public interface IGetNotificationPreferencesQueryHandler
{
    Task<Result<NotificationPreferencesDto>> ExecuteAsync(
        GetNotificationPreferencesQuery query,
        CancellationToken cancellationToken = default);
}

public class GetNotificationPreferencesQueryHandler : IGetNotificationPreferencesQueryHandler
{
    private readonly AppDataContext _db;
    private readonly ILogger<GetNotificationPreferencesQueryHandler> _logger;

    public GetNotificationPreferencesQueryHandler(
        AppDataContext db,
        ILogger<GetNotificationPreferencesQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<NotificationPreferencesDto>> ExecuteAsync(
        GetNotificationPreferencesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting notification preferences for UserId={UserId}", query.UserId);

        // No row = user has never changed a setting = all categories enabled.
        // Project defaults rather than 404 so the client always gets a full set.
        var dto = await _db.UserNotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == query.UserId)
            .Select(p => new NotificationPreferencesDto
            {
                PickResultEnabled = p.PickResultEnabled,
                PickDeadlineReminderEnabled = p.PickDeadlineReminderEnabled,
                ContestStartReminderEnabled = p.ContestStartReminderEnabled,
                LeagueInviteEnabled = p.LeagueInviteEnabled,
                MembershipEnabled = p.MembershipEnabled,
                MatchupPreviewEnabled = p.MatchupPreviewEnabled,
                ScheduleChangeEnabled = p.ScheduleChangeEnabled,
                OddsChangedEnabled = p.OddsChangedEnabled
            })
            .FirstOrDefaultAsync(cancellationToken);

        // All-defaults (everything on) when absent.
        return new Success<NotificationPreferencesDto>(dto ?? new NotificationPreferencesDto());
    }
}
