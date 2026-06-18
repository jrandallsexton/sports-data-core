using MassTransit;

using SportsData.Api.Application.UI.Contest.Commands.RefreshContest;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Events
{
    public class ContestRefreshRequestedHandler : IConsumer<ContestRefreshRequested>
    {
        private readonly ILogger<ContestRefreshRequestedHandler> _logger;
        private readonly IRefreshContestCommandHandler _refreshHandler;

        public ContestRefreshRequestedHandler(
            ILogger<ContestRefreshRequestedHandler> logger,
            IRefreshContestCommandHandler refreshHandler)
        {
            _logger = logger;
            _refreshHandler = refreshHandler;
        }

        public async Task Consume(ConsumeContext<ContestRefreshRequested> context)
        {
            var msg = context.Message;

            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = msg.CorrelationId,
                       ["ContestId"] = msg.ContestId,
                       ["Sport"] = msg.Sport
                   }))
            {
                var command = new RefreshContestCommand
                {
                    ContestId = msg.ContestId,
                    Sport = msg.Sport
                };

                var result = await _refreshHandler.ExecuteAsync(command, context.CancellationToken);

                if (result.IsSuccess)
                    return;

                // Permanent failures (4xx-class): log and move on. Retrying won't help.
                // Transient failures (Error, RateLimited): throw so MassTransit retries
                // and eventually DLQs if persistent.
                switch (result.Status)
                {
                    case ResultStatus.NotFound:
                    case ResultStatus.BadRequest:
                    case ResultStatus.Validation:
                    case ResultStatus.Forbid:
                    case ResultStatus.Unauthorized:
                        _logger.LogWarning(
                            "RefreshContest returned non-retryable status {Status}; not retrying.",
                            result.Status);
                        return;

                    default:
                        throw new Exception(
                            $"RefreshContest failed for ContestId={msg.ContestId} with status {result.Status}.");
                }
            }
        }
    }
}
