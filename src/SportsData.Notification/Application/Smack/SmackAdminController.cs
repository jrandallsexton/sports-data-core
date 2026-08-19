#nullable enable

using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Application.Smack.Commands.CreateSmackPhrase;
using SportsData.Notification.Application.Smack.Commands.RateSmackPreview;
using SportsData.Notification.Application.Smack.Commands.UpdateSmackPhrase;
using SportsData.Notification.Application.Smack.Queries.GetSmackPhrases;
using SportsData.Notification.Application.Smack.Queries.GetSmackRatings;
using SportsData.Notification.Application.Smack.Queries.PreviewSmack;
using SportsData.Notification.Infrastructure.Auth;

namespace SportsData.Notification.Application.Smack;

/// <summary>
/// SmackBot Lab's server side: preview what the voice would send for real
/// scored picks, manage the phrase catalog, and record 0–4 star ratings as
/// training data. API relays these server-to-server (the browser never
/// holds this service's key), same access model as
/// <see cref="Backfill.BackfillController"/>. See docs/features/smackbot-lab.md.
/// </summary>
[ApiController]
[Route("admin/smack")]
[ApiKeyAuth]
public class SmackAdminController : ControllerBase
{
    [HttpPost("preview")]
    public async Task<ActionResult<List<SmackPreviewResultDto>>> Preview(
        [FromBody] SmackPreviewRequestDto request,
        [FromServices] IPreviewSmackQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("phrases")]
    public async Task<ActionResult<List<SmackPhraseDto>>> GetPhrases(
        [FromServices] IGetSmackPhrasesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("phrases")]
    public async Task<ActionResult<SmackPhraseDto>> CreatePhrase(
        [FromBody] SmackPhraseUpsertDto request,
        [FromServices] ICreateSmackPhraseCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("phrases/{id:guid}")]
    public async Task<ActionResult<SmackPhraseDto>> UpdatePhrase(
        [FromRoute] Guid id,
        [FromBody] SmackPhraseUpsertDto request,
        [FromServices] IUpdateSmackPhraseCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("ratings")]
    public async Task<ActionResult<List<SmackRatingDto>>> GetRatings(
        [FromQuery] Guid leagueId,
        [FromServices] IGetSmackRatingsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(leagueId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("ratings")]
    public async Task<IActionResult> RatePreview(
        [FromBody] SmackRatingRequestDto request,
        [FromServices] IRateSmackPreviewCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(request, cancellationToken);
        // Bare 200 on success, matching the pre-VSA contract (no body).
        return result.ToActionResult(_ => new OkResult());
    }
}
