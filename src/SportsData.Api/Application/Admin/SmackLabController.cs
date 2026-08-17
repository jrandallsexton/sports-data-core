using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin.SmackLab;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Notification;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;

namespace SportsData.Api.Application.Admin;

/// <summary>
/// SmackBot Lab's API surface (web admin → here → Notification). Two
/// composition reads own the canonical-data half (leagues with scored picks,
/// per-league preview facts); the rest are thin relays through
/// <see cref="IProvideNotifications"/> — the typed client stamps Notification's
/// X-Api-Key server-to-server, so the browser only ever holds the admin token
/// this controller already requires. See docs/features/smackbot-lab.md.
/// </summary>
[ApiController]
[Route("admin/smack-lab")]
[AdminApiToken]
public class SmackLabController : ControllerBase
{
    private readonly ILogger<SmackLabController> _logger;

    public SmackLabController(ILogger<SmackLabController> logger)
    {
        _logger = logger;
    }

    /// <summary>Leagues with at least one scored pick to preview.</summary>
    [HttpGet("leagues")]
    public async Task<ActionResult<List<SmackLabLeagueDto>>> GetLeagues(
        [FromServices] IGetSmackLabLeaguesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Every scored pick in a league, composed into the preview fact payload
    /// plus operator display context.
    /// </summary>
    [HttpGet("leagues/{leagueId:guid}/picks")]
    public async Task<ActionResult<List<SmackLabPickDto>>> GetPicks(
        [FromServices] IGetSmackLabPicksQueryHandler handler,
        [FromRoute] Guid leagueId,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(leagueId, cancellationToken);
        return result.ToActionResult();
    }

    // ─── Relays to Notification (typed client only — no ad-hoc HTTP) ─────

    [HttpPost("preview")]
    public async Task<ActionResult<List<SmackPreviewResultDto>>> Preview(
        [FromServices] IProvideNotifications notifications,
        [FromBody] SmackPreviewRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await notifications.PreviewSmack(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("phrases")]
    public async Task<ActionResult<List<SmackPhraseDto>>> GetPhrases(
        [FromServices] IProvideNotifications notifications,
        CancellationToken cancellationToken)
    {
        var result = await notifications.GetSmackPhrases(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("phrases")]
    public async Task<ActionResult<SmackPhraseDto>> CreatePhrase(
        [FromServices] IProvideNotifications notifications,
        [FromBody] SmackPhraseUpsertDto request,
        CancellationToken cancellationToken)
    {
        var result = await notifications.CreateSmackPhrase(request, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>Relays the xmin echo; a stale RowVersion returns 409.</summary>
    [HttpPut("phrases/{phraseId:guid}")]
    public async Task<ActionResult<SmackPhraseDto>> UpdatePhrase(
        [FromServices] IProvideNotifications notifications,
        [FromRoute] Guid phraseId,
        [FromBody] SmackPhraseUpsertDto request,
        CancellationToken cancellationToken)
    {
        var result = await notifications.UpdateSmackPhrase(phraseId, request, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>Stored ratings for a league — the Lab re-hydrates stars from these.</summary>
    [HttpGet("leagues/{leagueId:guid}/ratings")]
    public async Task<ActionResult<List<SmackRatingDto>>> GetRatings(
        [FromServices] IProvideNotifications notifications,
        [FromRoute] Guid leagueId,
        CancellationToken cancellationToken)
    {
        var result = await notifications.GetSmackRatings(leagueId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("ratings")]
    public async Task<IActionResult> RatePreview(
        [FromServices] IProvideNotifications notifications,
        [FromBody] SmackRatingRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await notifications.RateSmackPreview(request, cancellationToken);
        return result.ToActionResult().Result ?? Ok();
    }
}
