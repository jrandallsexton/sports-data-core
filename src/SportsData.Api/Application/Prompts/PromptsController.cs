using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin;
using SportsData.Core.Extensions;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Api.Application.Prompts;

/// <summary>
/// LLM prompt management. Prompt text lives in the database, never in the
/// repository — this repo is public and the prompts are not.
/// </summary>
/// <remarks>
/// First slice extracted from <c>AdminController</c>
/// (docs/refactor/admin-controller-vsa-split.md). "Admin" is an authorization
/// concern, not a domain, so these endpoints live with the thing they manage
/// and carry the requirement as an attribute instead of a URL segment.
/// <para>
/// <c>[AdminApiToken]</c> is unchanged from the original and already accepts
/// either the shared <c>X-Admin-Token</c> header or a Firebase JWT carrying the
/// Admin role claim. Replacing it with a standard policy is a separate step
/// that applies to every slice at once.
/// </para>
/// </remarks>
[ApiController]
[Route("api/prompts")]
[AdminApiToken]
public class PromptsController : ControllerBase
{
    /// <summary>
    /// Create a prompt. IsDefault=true flips the (Sport, WithStats) slot.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> CreatePrompt(
        [FromBody] CreatePromptCommand command,
        [FromServices] ICreatePromptCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// One-time seeding: import a legacy prompt blob from the "prompts"
    /// container into the Prompt table.
    /// </summary>
    [HttpPost("import-blob")]
    public async Task<ActionResult<Guid>> ImportPromptFromBlob(
        [FromBody] ImportPromptFromBlobCommand command,
        [FromServices] IImportPromptFromBlobCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet]
    public async Task<ActionResult<List<PromptSummaryDto>>> GetPrompts(
        [FromServices] IGetPromptsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{promptId}")]
    public async Task<ActionResult<PromptDetailDto>> GetPromptById(
        [FromRoute] Guid promptId,
        [FromServices] IGetPromptByIdQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(promptId, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Edit a prompt's Text/Description. Name and slot (Sport, WithStats) are
    /// immutable — a different slot is a new version.
    /// </summary>
    [HttpPut("{promptId}")]
    public async Task<ActionResult<Guid>> UpdatePrompt(
        [FromRoute] Guid promptId,
        [FromBody] UpdatePromptCommand command,
        [FromServices] IUpdatePromptCommandHandler handler,
        CancellationToken cancellationToken)
    {
        command.PromptId = promptId;
        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Make a prompt the default for its own (Sport, WithStats) slot; clears
    /// the slot's previous default. Effective next run.
    /// </summary>
    [HttpPost("{promptId}/set-default")]
    public async Task<ActionResult<Guid>> SetDefaultPrompt(
        [FromRoute] Guid promptId,
        [FromServices] ISetDefaultPromptCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(promptId, cancellationToken);
        return result.ToActionResult();
    }
}
