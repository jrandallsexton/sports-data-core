#nullable enable

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

namespace SportsData.Notification.Application.Backfill.Commands.RequestPickemGroupsBackfill;

public interface IRequestPickemGroupsBackfillCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Triggers a full backfill of the local <c>PickemGroup</c> +
/// <c>PickemGroupMember</c> projections by publishing
/// <see cref="PickemGroupsRequested"/>. The API consumer responds with
/// one bundled <c>PickemGroupDataPublished</c> per league (members
/// embedded in the payload), and Notification's own consumer
/// upserts each group + replaces its member roster.
/// </summary>
public class RequestPickemGroupsBackfillCommandHandler : IRequestPickemGroupsBackfillCommandHandler
{
    private readonly ILogger<RequestPickemGroupsBackfillCommandHandler> _logger;
    private readonly IEventBus _eventBus;
    private readonly IMessageDeliveryScope _deliveryScope;

    public RequestPickemGroupsBackfillCommandHandler(
        ILogger<RequestPickemGroupsBackfillCommandHandler> logger,
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
            "Publishing PickemGroupsRequested. CorrelationId={CorrelationId}",
            correlationId);

        // Direct publish — Notification has no DbContext write to bundle
        // this with, and the bus-outbox isn't registered for this service.
        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            await _eventBus.Publish(new PickemGroupsRequested(
                    Sport.All,
                    null,
                    correlationId,
                    Guid.NewGuid()),
                cancellationToken);
        }

        return new Success<Guid>(correlationId);
    }
}
