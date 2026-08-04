using SportsData.Api.Application.UI.Leagues.Dtos;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetPendingInvitations;

/// <summary>
/// One row on the "Pending Invitations" home card (web + mobile). Embeds the
/// full <see cref="PublicLeagueDto"/> so clients can show the SAME
/// join-confirmation dialog (league parameters and all) used by public-league
/// discovery — an invitation grants visibility into the league's settings
/// even when the league itself is private.
/// </summary>
public class PendingInvitationDto
{
    public Guid InvitationId { get; set; }

    /// <summary>Display name of the inviting member.</summary>
    public string InvitedBy { get; set; } = string.Empty;

    public DateTime InvitedUtc { get; set; }

    public PublicLeagueDto League { get; set; } = default!;
}
