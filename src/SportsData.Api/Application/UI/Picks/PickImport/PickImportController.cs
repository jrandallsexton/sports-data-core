using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Picks.PickImport.Commands.ImportPicks;
using SportsData.Api.Application.UI.Picks.PickImport.Dtos;
using SportsData.Api.Application.UI.Picks.PickImport.Queries.GetPickImportPreview;
using SportsData.Api.Application.UI.Picks.PickImport.Queries.GetPickImportSources;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Picks.PickImport;

/// <summary>
/// Cross-league pick import, scoped to the target league.
/// See docs/features/pick-import-across-leagues.md.
/// </summary>
[ApiController]
[Route("ui/leagues/{targetId:guid}/picks/import")]
public class PickImportController : ApiControllerBase
{
    /// <summary>
    /// Candidate source leagues for the picker: the user's other active same-type
    /// leagues that share at least one contest with the target.
    /// </summary>
    [HttpGet("sources")]
    [Authorize]
    public async Task<ActionResult<List<PickImportSourceDto>>> GetSources(
        [FromRoute] Guid targetId,
        [FromServices] IGetPickImportSourcesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetPickImportSourcesQuery
        {
            UserId = HttpContext.GetCurrentUserId(),
            TargetLeagueId = targetId
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Dry-run plan for importing the user's picks from the source league into this
    /// target league. No writes.
    /// </summary>
    [HttpPost("preview")]
    [Authorize]
    public async Task<ActionResult<PickImportPreviewDto>> Preview(
        [FromRoute] Guid targetId,
        [FromBody] PickImportPreviewRequest request,
        [FromServices] IGetPickImportPreviewQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetPickImportPreviewQuery
        {
            UserId = HttpContext.GetCurrentUserId(),
            SourceLeagueId = request.SourceLeagueId,
            TargetLeagueId = targetId
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Commits the import: creates the import set plus the collisions the user
    /// chose to replace. Returns a summary. Confidence-points targets are not yet
    /// supported and are rejected.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<PickImportResultDto>> Import(
        [FromRoute] Guid targetId,
        [FromBody] PickImportRequest request,
        [FromServices] IImportPicksCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new ImportPicksCommand
        {
            UserId = HttpContext.GetCurrentUserId(),
            SourceLeagueId = request.SourceLeagueId,
            TargetLeagueId = targetId,
            ReplaceContestIds = request.ReplaceContestIds
        };

        var result = await handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }
}
