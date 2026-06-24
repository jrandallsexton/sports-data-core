using MassTransit;

using SportsData.Core.Eventing.Events.PickemGroups;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// PickemGroupCreated is a precondition / awareness event for Notification.
    /// No user-facing notification fires on league creation today — the welcome
    /// flow is driven by <see cref="PickemGroupMemberAddedConsumer"/> (which
    /// fires for the commissioner-as-first-member too, via
    /// <c>CreateLeagueCommandHandlerBase</c>'s synthetic membership).
    ///
    /// <para>
    /// Kept as a no-op handler because (a) we may layer in commissioner-side
    /// "your league is live" notifications later and (b) projecting a minimal
    /// league record locally for fast lookup during fan-out is on the table
    /// (see design doc §3 — currently we lean on fat events instead).
    /// </para>
    /// </summary>
    public class PickemGroupCreatedConsumer : IConsumer<PickemGroupCreated>
    {
        private readonly ILogger<PickemGroupCreatedConsumer> _logger;

        public PickemGroupCreatedConsumer(ILogger<PickemGroupCreatedConsumer> logger)
        {
            _logger = logger;
        }

        public Task Consume(ConsumeContext<PickemGroupCreated> context)
        {
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = context.Message.CorrelationId,
                ["GroupId"] = context.Message.GroupId,
                ["Sport"] = context.Message.Sport
            });

            _logger.LogInformation(
                "PickemGroupCreated received; no notification fan-out today.");

            // TODO (future): If we add commissioner-side "your league is live"
            // notifications, look up the synthetic commissioner membership
            // via a follow-up PickemGroupMemberAdded event (it carries the
            // commissioner's UserId) — do NOT try to derive it here, since
            // PickemGroupCreated does not carry the commissioner identity.

            return Task.CompletedTask;
        }
    }
}
