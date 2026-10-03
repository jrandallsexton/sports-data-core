using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Prompts.Commands.CreatePrompt;
using SportsData.Api.Application.Prompts.Commands.ImportPromptFromBlob;
using SportsData.Api.Application.Prompts.Commands.SetDefaultPrompt;
using SportsData.Api.Application.Prompts.Commands.UpdatePrompt;
using SportsData.Api.Application.Prompts.Queries.GetPromptById;
using SportsData.Api.Application.Prompts.Queries.GetPrompts;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Prompts;

/// <summary>
/// Prompt management for the admin Prompt Manager page. Every action is
/// admin-only: [AdminApiToken] is on the class (accepts the shared header or
/// a Firebase JWT with the Admin role).
/// </summary>
[ApiController]
[Route("api/prompts")]
[AdminApiToken]
public class PromptsController : ApiControllerBase
{
    /// <summary>
    /// Create a prompt (text lives in the DB — the repo is public, the
    /// DB is not). IsDefault=true flips the (Sport, WithStats) slot.
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
    /// Edit a prompt's Text/Description. Name and slot (Sport,
    /// WithStats) are immutable — a different slot is a new version.
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
    /// Make a prompt the default for its own (Sport, WithStats) slot;
    /// clears the slot's previous default. Effective next run.
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
