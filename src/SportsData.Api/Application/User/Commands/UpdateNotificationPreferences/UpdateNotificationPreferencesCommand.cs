namespace SportsData.Api.Application.User.Commands.UpdateNotificationPreferences;

/// <summary>
/// Full replacement of a user's per-category notification opt-in flags. The
/// client always sends the complete set (the settings screen holds every toggle
/// in state), so this is a PUT-style overwrite rather than a partial patch.
/// </summary>
public class UpdateNotificationPreferencesCommand
{
    public bool PickResultEnabled { get; init; } = true;
    public bool PickDeadlineReminderEnabled { get; init; } = true;
    public bool ContestStartReminderEnabled { get; init; } = true;
    public bool LeagueInviteEnabled { get; init; } = true;
    public bool MembershipEnabled { get; init; } = true;
    public bool MatchupPreviewEnabled { get; init; } = true;
    public bool ScheduleChangeEnabled { get; init; } = true;
    public bool OddsChangedEnabled { get; init; } = true;
}
