#nullable enable

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Users;

namespace SportsData.Notification.Application.Backfill.Commands.RequestUsersBackfill;

public interface IRequestUsersBackfillCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Triggers a full backfill of the local <c>User</c> projection by
/// publishing <see cref="UsersRequested"/>. The API consumer responds
/// with one <c>UserDataPublished</c> per user, and Notification's
/// own <c>UserDataPublishedConsumer</c> upserts them locally.
/// </summary>
public class RequestUsersBackfillCommandHandler : IRequestUsersBackfillCommandHandler
{
    private readonly ILogger<RequestUsersBackfillCommandHandler> _logger;
    private readonly IEventBus _eventBus;
    private readonly IMessageDeliveryScope _deliveryScope;

    public RequestUsersBackfillCommandHandler(
        ILogger<RequestUsersBackfillCommandHandler> logger,
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
            "Publishing UsersRequested. CorrelationId={CorrelationId}",
            correlationId);

        // Direct publish — Notification has no DbContext write to bundle
        // this with, and the bus-outbox isn't registered for this service.
        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            await _eventBus.Publish(new UsersRequested(
                    Sport.All,
                    null,
                    correlationId,
                    Guid.NewGuid()),
                cancellationToken);
        }

        return new Success<Guid>(correlationId);
    }
}
