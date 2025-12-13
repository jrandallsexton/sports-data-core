using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Provider.Application.Sourcing.Historical;

namespace SportsData.Provider.Application.Sourcing;

[Route("api/sourcing/historical")]
public class HistoricalSourcingController : ApiControllerBase
{
    private readonly ILogger<HistoricalSourcingController> _logger;
    private readonly IHistoricalSeasonSourcingService _sourcingService;

    public HistoricalSourcingController(
        ILogger<HistoricalSourcingController> logger,
        IHistoricalSeasonSourcingService sourcingService)
    {
        _logger = logger;
        _sourcingService = sourcingService;
    }

    /// <summary>
    /// Initiates historical season sourcing for a completed season.
    /// </summary>
    /// <param name="request">Sourcing parameters including sport, provider, year, and optional tier delays</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Correlation ID for tracking the sourcing job</returns>
    /// <response code="202">Sourcing job initiated successfully</response>
    /// <response code="400">Invalid request parameters</response>
    [HttpPost("seasons")]
    [ProducesResponseType(typeof(HistoricalSeasonSourcingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SourceSeason(
        [FromBody] HistoricalSeasonSourcingRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received historical season sourcing request. Sport={Sport}, Provider={Provider}, Year={Year}",
            request.Sport, request.SourceDataProvider, request.SeasonYear);

        try
        {
            var response = await _sourcingService.SourceSeasonAsync(request, cancellationToken);

            return Accepted(response);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Historical sourcing not supported for this sport/provider combination");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating historical season sourcing");
            throw;
        }
    }
}
