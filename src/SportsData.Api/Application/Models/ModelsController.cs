using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Models.Commands.CreateModel;
using SportsData.Api.Application.Models.Commands.SetDefaultModel;
using SportsData.Api.Application.Models.Commands.UpdateModel;
using SportsData.Api.Application.Models.Queries.GetModelById;
using SportsData.Api.Application.Models.Queries.GetModels;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Models;

/// <summary>
/// Model management for the admin Model Manager page. Every action is
/// admin-only: [AdminApiToken] is on the class (accepts the shared header or
/// a Firebase JWT with the Admin role).
/// </summary>
[ApiController]
[Route("api/models")]
[AdminApiToken]
public class ModelsController : ApiControllerBase
{
    /// <summary>
    /// Create a model identity record (seed data:
    /// docs/metrics-modeling/llm-training-dates.md). IsDefault=true
    /// makes it THE production model.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateModel(
        [FromBody] CreateModelCommand command,
        [FromServices] ICreateModelCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet]
    public async Task<ActionResult<List<ModelDto>>> GetModels(
        [FromServices] IGetModelsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{modelId}")]
    public async Task<ActionResult<ModelDto>> GetModelById(
        [FromRoute] Guid modelId,
        [FromServices] IGetModelByIdQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(modelId, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Edit a model's metadata (cutoff verification, costs, IsActive).
    /// Identity fields (Name, ApiModelId, provider) are immutable — a
    /// different API identifier is a different model.
    /// </summary>
    [HttpPut("{modelId}")]
    public async Task<ActionResult<Guid>> UpdateModel(
        [FromRoute] Guid modelId,
        [FromBody] UpdateModelCommand command,
        [FromServices] IUpdateModelCommandHandler handler,
        CancellationToken cancellationToken)
    {
        command.ModelId = modelId;
        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Make a model THE production default (single global slot).
    /// Effective next generation run — the pre-season model selection,
    /// and any in-season swap, is this call.
    /// </summary>
    [HttpPost("{modelId}/set-default")]
    public async Task<ActionResult<Guid>> SetDefaultModel(
        [FromRoute] Guid modelId,
        [FromServices] ISetDefaultModelCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(modelId, cancellationToken);
        return result.ToActionResult();
    }
}
