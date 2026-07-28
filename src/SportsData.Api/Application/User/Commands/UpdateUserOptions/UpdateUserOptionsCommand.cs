namespace SportsData.Api.Application.User.Commands.UpdateUserOptions;

/// <summary>
/// Full replacement of the KNOWN user options (see <c>UserOptionKeys</c>) —
/// mirrors notification-preferences' full-set PATCH so a stale client can't
/// partially clobber newer options it doesn't know about (unknown rows are
/// never touched).
/// </summary>
public record UpdateUserOptionsCommand
{
    public bool ShowGamblingContent { get; init; }
}
