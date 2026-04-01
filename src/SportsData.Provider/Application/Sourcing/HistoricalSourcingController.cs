using Microsoft.AspNetCore.Mvc;

using MassTransit;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Provider.Application.Sourcing.Historical;
using SportsData.Provider.Application.Sourcing.Historical.Saga;

namespace SportsData.Provider.Application.Sourcing;

[Route("api/sourcing/historical")]
public class HistoricalSourcingController : ApiControllerBase
{
    private readonly ILogger<HistoricalSourcingController> _logger;
    private readonly IHistoricalSeasonSourcingService _sourcingService;
    private readonly IBus _bus;
    private readonly IAppMode _appMode;

    public HistoricalSourcingController(
        ILogger<HistoricalSourcingController> logger,
        IHistoricalSeasonSourcingService sourcingService,
        IBus bus,
        IAppMode appMode)
    {
        _logger = logger;
        _sourcingService = sourcingService;
        _bus = bus;
        _appMode = appMode;
    }

    /// <summary>
    /// Initiates historical season sourcing for a completed season.
    /// Sport is derived from the pod's -mode flag.
    /// </summary>
    [HttpPost("seasons")]
    [ProducesResponseType(typeof(HistoricalSeasonSourcingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SourceSeason(
        [FromBody] HistoricalSeasonSourcingRequest request,
        CancellationToken cancellationToken)
    {
        var sport = _appMode.CurrentSport;

        _logger.LogInformation(
            "Received historical season sourcing request. Sport={Sport}, Provider={Provider}, Year={Year}",
            sport, request.SourceDataProvider, request.SeasonYear);

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
    /// Sport is derived from the pod's -mode flag.
    /// </summary>
    [HttpPost("seasons/saga")]
    [ProducesResponseType(typeof(SeasonSourcingSagaResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartSagaSourcing(
        [FromBody] HistoricalSeasonSourcingRequest request,
        CancellationToken cancellationToken)
    {
        var sport = _appMode.CurrentSport;

        _logger.LogInformation(
            "SAGA_START_REQUESTED: Sport={Sport}, Provider={Provider}, Year={Year}",
            sport, request.SourceDataProvider, request.SeasonYear);

        try
        {
            var correlationId = await _sourcingService.CreateSagaResourceIndexesAsync(request, cancellationToken);

            var sagaEvent = new SeasonSourcingStarted(
                correlationId,
                sport,
                request.SeasonYear,
                request.SourceDataProvider);

            await _bus.Publish(sagaEvent, cancellationToken);

            _logger.LogInformation(
                "SAGA_STARTED: SeasonSourcingStarted event published. CorrelationId={CorrelationId}",
                correlationId);

            return Accepted(new SeasonSourcingSagaResponse(
                correlationId,
                sport,
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
