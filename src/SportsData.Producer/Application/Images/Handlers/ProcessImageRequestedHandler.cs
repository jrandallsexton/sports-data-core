using MassTransit;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Processing;

namespace SportsData.Producer.Application.Images.Handlers
{
    public class ProcessImageRequestedHandler :
        IConsumer<ProcessImageRequest>
    {
        private readonly ILogger<ProcessImageRequestedHandler> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ProcessImageRequestedHandler(
            ILogger<ProcessImageRequestedHandler> logger,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task Consume(ConsumeContext<ProcessImageRequest> context)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = context.Message.CorrelationId
                   }))
            {
                _logger.LogInformation("New ProcessImageRequest event received: {@message}", context.Message);
                _backgroundJobProvider.Enqueue<ImageRequestedProcessor>(x => x.Process(context.Message));
            }
        }
    }
}
