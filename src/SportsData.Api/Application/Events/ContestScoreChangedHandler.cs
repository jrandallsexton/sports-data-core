using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Handles ContestScoreChanged events: broadcasts them to connected
    /// SignalR clients, and clears the audit watermark on the contest's picks
    /// so the nightly audit re-verifies scoring against the corrected scores.
    /// </summary>
    public class ContestScoreChangedHandler : IConsumer<ContestScoreChanged>
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IInvalidatePickAudits _auditInvalidator;

        public ContestScoreChangedHandler(
            IHubContext<NotificationHub> hubContext,
            IInvalidatePickAudits auditInvalidator)
        {
            _hubContext = hubContext;
            _auditInvalidator = auditInvalidator;
        }

        public async Task Consume(ConsumeContext<ContestScoreChanged> context)
        {
            var msg = context.Message;

            await _hubContext.Clients
                .All // ? simple, global broadcast for now
                .SendAsync("ContestScoreChanged", msg, context.CancellationToken);

            // A score correction changes exactly the inputs the audit checks,
            // so any prior audit of these picks is stale.
            await _auditInvalidator.InvalidateForContestAsync(
                msg.ContestId,
                nameof(ContestScoreChanged),
                context.CancellationToken);
        }
    }
}
