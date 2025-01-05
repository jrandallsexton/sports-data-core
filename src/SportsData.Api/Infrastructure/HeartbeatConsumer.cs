using MassTransit;

using SportsData.Core.Eventing.Events;

namespace SportsData.Api.Infrastructure
{
    public class HeartbeatConsumer(ILogger<HeartbeatConsumer> logger) :
        IConsumer<Heartbeat>
    {
        public async Task Consume(ConsumeContext<Heartbeat> context)
        {
            logger.LogInformation("heartbeat received: {@heartbeat}", context.Message);
            await Task.CompletedTask;
        }
    }
}
