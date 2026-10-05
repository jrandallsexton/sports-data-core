using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Synthetics.Commands.RefreshAiExistence;
using SportsData.Api.Infrastructure.Auth;
using SportsData.Core.Common;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Synthetics;

/// <summary>
/// Synthetic users (StatBot, MetricBot, the pick-style bots): operator
/// tools for their league membership and picks. Every action is
/// admin-only: [AdminApiToken] is on the class.
/// </summary>
[ApiController]
[Route("api/synthetics")]
[AdminApiToken]
public class SyntheticsController : ApiControllerBase
{
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public SyntheticsController(IProvideBackgroundJobs backgroundJobProvider)
    {
        _backgroundJobProvider = backgroundJobProvider;
    }

    /// <summary>
    /// Ensure every synthetic is in every league and has picks for a week.
    /// StatBot's picks are written as previews land (event handlers); this
    /// is the catch-all sweep. <paramref name="week"/> defaults to the
    /// current week; name a past week to backfill it.
    /// </summary>
    [HttpPost("refresh")]
    public IActionResult RefreshAiExistence([FromQuery] int? week = null)
    {
        var correlationId = Guid.NewGuid();
        var command = new RefreshAiExistenceCommand { CorrelationId = correlationId, Week = week };
        _backgroundJobProvider.Enqueue<IRefreshAiExistenceCommandHandler>(p => p.ExecuteAsync(command, CancellationToken.None));
        return Accepted(correlationId);
    }
}
