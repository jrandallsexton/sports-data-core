namespace SportsData.Api.Application.UI.Leagues.Queries.GetInviteableUsers;

/// <summary>
/// A registered user surfaced by the invite-by-username autocomplete. Carries
/// the stable handle and the display label only — never email (privacy).
/// </summary>
public class InviteableUserDto
{
    public required Guid UserId { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
}
