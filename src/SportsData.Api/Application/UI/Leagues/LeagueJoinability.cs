using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues;

/// <summary>
/// The single derivation of "when does joining close?" shared by every
/// read surface that renders a join affordance (public discovery, pending
/// invitations). The stored expiry (LeagueJoinExpiryCalculator's output) is
/// the authority; the derived first-game start only covers the uncomputed
/// gap for CloseAtFirstGame leagues — and NOT FullSeason+drop-week leagues,
/// where the calculator's week-(N+1) override applies and first-game would
/// be wrong (uncomputed there means "open"; the creation trigger fills it
/// within seconds). The join WRITE gate (JoinLeagueCommandHandler) applies
/// this same policy.
/// </summary>
public static class LeagueJoinability
{
    public static (DateTime? ClosesAtUtc, bool IsJoinable) Compute(
        LeagueWindow leagueWindow,
        int? dropLowWeeksCount,
        JoinPolicy joinPolicy,
        DateTime? invitationsExpireUtc,
        DateTime? firstGameUtc,
        DateTime nowUtc)
    {
        var dropWeekOverride = leagueWindow == LeagueWindow.FullSeason
            && dropLowWeeksCount is > 0;
        var closesAtUtc = invitationsExpireUtc
            ?? (joinPolicy == JoinPolicy.CloseAtFirstGame && !dropWeekOverride
                ? firstGameUtc : null);
        return (closesAtUtc, closesAtUtc is null || closesAtUtc > nowUtc);
    }
}
