using Microsoft.AspNetCore.Mvc;

using MassTransit;

using SportsData.Core.Common;
using SportsData.Provider.Application.Sourcing.Historical;
using SportsData.Provider.Application.Sourcing.Historical.Saga;

namespace SportsData.Provider.Application.Sourcing;

[Route("api/sourcing/historical")]
public class HistoricalSourcingController : ApiControllerBase
{
    private readonly ILogger<HistoricalSourcingController> _logger;
    private readonly IHistoricalSeasonSourcingService _sourcingService;
    private readonly IBus _bus;

    public HistoricalSourcingController(
        ILogger<HistoricalSourcingController> logger,
        IHistoricalSeasonSourcingService sourcingService,
        IBus bus)
    {
        _logger = logger;
        _sourcingService = sourcingService;
        _bus = bus;
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

    /// <summary>
    /// Starts the saga-orchestrated historical season sourcing workflow.
    /// Creates ResourceIndex jobs for all tiers but only triggers Tier 1 (Season).
    /// The saga will automatically trigger subsequent tiers based on completion events.
    /// </summary>
    /// <param name="request">Sourcing parameters including sport, provider, and year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Correlation ID for tracking the saga</returns>
    /// <response code="202">Saga initiated successfully</response>
    /// <response code="400">Invalid request parameters</response>
    [HttpPost("seasons/saga")]
    [ProducesResponseType(typeof(SeasonSourcingSagaResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartSagaSourcing(
        [FromBody] HistoricalSeasonSourcingRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "🚀 SAGA_START_REQUESTED: Sport={Sport}, Provider={Provider}, Year={Year}",
            request.Sport, request.SourceDataProvider, request.SeasonYear);

        try
        {
            // Create ResourceIndex entities for all 4 tiers (NO Hangfire scheduling)
            var correlationId = await _sourcingService.CreateSagaResourceIndexesAsync(request, cancellationToken);

            // Publish SeasonSourcingStarted event to kick off the saga
            var sagaEvent = new SeasonSourcingStarted(
                correlationId,
                request.Sport,
                request.SeasonYear,
                request.SourceDataProvider);

            await _bus.Publish(sagaEvent, cancellationToken);

            _logger.LogInformation(
                "✅ SAGA_STARTED: SeasonSourcingStarted event published. CorrelationId={CorrelationId}",
                correlationId);

            return Accepted(new SeasonSourcingSagaResponse(
                correlationId,
                request.Sport,
                request.SeasonYear,
                request.SourceDataProvider,
                $"Saga initiated. Track progress via CorrelationId: {correlationId}"));
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Historical sourcing not supported for this sport/provider combination");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating saga-based historical season sourcing");
            throw;
        }
    }
}

public record SeasonSourcingSagaResponse(
    Guid CorrelationId,
    Sport Sport,
    int SeasonYear,
    SourceDataProvider Provider,
    string Message);
