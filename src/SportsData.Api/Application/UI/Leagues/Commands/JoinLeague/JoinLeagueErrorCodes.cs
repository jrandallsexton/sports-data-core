namespace SportsData.Api.Application.UI.Leagues.Commands.JoinLeague;

/// <summary>
/// Stable machine-readable error codes carried on
/// <c>ValidationFailure.ErrorCode</c> by <see cref="JoinLeagueCommandHandler"/>.
/// Consumers (e.g. the invitation accept flow's self-heal branch) match on
/// these instead of human-readable message text.
/// </summary>
public static class JoinLeagueErrorCodes
{
    public const string AlreadyMember = "join_league.already_member";
}
