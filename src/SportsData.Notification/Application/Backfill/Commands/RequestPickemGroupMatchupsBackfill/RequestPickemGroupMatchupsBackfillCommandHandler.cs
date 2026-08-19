#nullable enable

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

namespace SportsData.Notification.Application.Backfill.Commands.RequestPickemGroupMatchupsBackfill;

public interface IRequestPickemGroupMatchupsBackfillCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Triggers a full backfill of the local <c>PickemGroupMatchup</c>
/// projection by publishing <see cref="PickemGroupMatchupsRequested"/>.
/// The API consumer responds with one <c>PickemGroupMatchupDataPublished</c>
/// per future matchup (StartDateUtc &gt; UtcNow filter — past games
/// excluded), and Notification's own consumer upserts each row.
/// </summary>
public class RequestPickemGroupMatchupsBackfillCommandHandler : IRequestPickemGroupMatchupsBackfillCommandHandler
{
    private readonly ILogger<RequestPickemGroupMatchupsBackfillCommandHandler> _logger;
    private readonly IEventBus _eventBus;
    private readonly IMessageDeliveryScope _deliveryScope;

    public RequestPickemGroupMatchupsBackfillCommandHandler(
        ILogger<RequestPickemGroupMatchupsBackfillCommandHandler> logger,
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope)
    {
        _logger = logger;
        _eventBus = eventBus;
        _deliveryScope = deliveryScope;
    }

    public async Task<Result<Guid>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid();

        _logger.LogInformation(
            "Publishing PickemGroupMatchupsRequested. CorrelationId={CorrelationId}",
            correlationId);

        // Direct publish — Notification has no DbContext write to bundle
        // this with, and the bus-outbox isn't registered for this service.
        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            await _eventBus.Publish(new PickemGroupMatchupsRequested(
                    Sport.All,
                    null,
                    correlationId,
                    Guid.NewGuid()),
                cancellationToken);
        }

        return new Success<Guid>(correlationId);
    }
}
