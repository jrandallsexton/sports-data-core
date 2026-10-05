using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Diagnostics.Commands.GenerateUrlIdentity;
using SportsData.Api.Application.Diagnostics.Queries.GetAiResponse;
using SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutCompetitors;
using SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutDrives;
using SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutMetrics;
using SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutPlays;
using SportsData.Api.Infrastructure.Auth;
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Competition;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Diagnostics;

/// <summary>
/// Operator diagnostics: data-integrity queries (competitions missing a child
/// dataset), an AI connectivity check, and URL-identity generation. Every
/// action is admin-only: [AdminApiToken] is on the class.
/// </summary>
[ApiController]
[Route("api/diagnostics")]
[AdminApiToken]
public class DiagnosticsController : ApiControllerBase
{
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public DiagnosticsController(
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
    {
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
    }

    [HttpGet("competitions-without-competitors")]
    public async Task<ActionResult<List<CompetitionWithoutCompetitorsDto>>> GetCompetitionsWithoutCompetitors(
        [FromServices] IGetCompetitionsWithoutCompetitorsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetCompetitionsWithoutCompetitorsQuery();
        var result = await handler.ExecuteAsync(query, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("competitions-without-plays")]
    public async Task<ActionResult<List<CompetitionWithoutPlaysDto>>> GetCompetitionsWithoutPlays(
        [FromServices] IGetCompetitionsWithoutPlaysQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetCompetitionsWithoutPlaysQuery();
        var result = await handler.ExecuteAsync(query, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("competitions-without-drives")]
    public async Task<ActionResult<List<CompetitionWithoutDrivesDto>>> GetCompetitionsWithoutDrives(
        [FromServices] IGetCompetitionsWithoutDrivesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetCompetitionsWithoutDrivesQuery();
        var result = await handler.ExecuteAsync(query, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("competitions-without-metrics")]
    public async Task<ActionResult<List<CompetitionWithoutMetricsDto>>> GetCompetitionsWithoutMetrics(
        [FromServices] IGetCompetitionsWithoutMetricsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetCompetitionsWithoutMetricsQuery();
        var result = await handler.ExecuteAsync(query, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("ai-test")]
    public async Task<ActionResult<string>> TestAiCommunications(
        [FromBody] GetAiResponseQuery query,
        [FromServices] IGetAiResponseQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(query, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("generate-url-identity")]
    public Task<IActionResult> GenerateUrlIdentity([FromBody] GenerateUrlIdentityCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Url))
        {
            return Task.FromResult<IActionResult>(BadRequest("URL cannot be empty."));
        }

        var identity = _externalRefIdentityGenerator.Generate(command.Url);

        return Task.FromResult<IActionResult>(Ok(identity));
    }
}
