namespace SportsData.Api.Application.User.Dtos;

/// <summary>
/// Per-category notification opt-in flags. Returned by GET and accepted (full
/// set) by the PATCH on /user/me/notification-preferences.
/// </summary>
public class NotificationPreferencesDto
{
    public bool PickResultEnabled { get; set; } = true;
    public bool PickDeadlineReminderEnabled { get; set; } = true;
    public bool ContestStartReminderEnabled { get; set; } = true;
    public bool LeagueInviteEnabled { get; set; } = true;
    public bool MembershipEnabled { get; set; } = true;
    public bool MatchupPreviewEnabled { get; set; } = true;
    public bool ScheduleChangeEnabled { get; set; } = true;
    public bool OddsChangedEnabled { get; set; } = true;
}
