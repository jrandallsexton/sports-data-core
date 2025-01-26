using MassTransit;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Processing;

namespace SportsData.Producer.Application.Images.Handlers
{
    public class ProcessImageResponseHandler :
        IConsumer<ProcessImageResponse>
    {
        private readonly ILogger<ProcessImageResponseHandler> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ProcessImageResponseHandler(
            ILogger<ProcessImageResponseHandler> logger,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task Consume(ConsumeContext<ProcessImageResponse> context)
        {
            _logger.LogInformation("new ProcessImageResponse event received: {@message}", context.Message);
            using (_logger.BeginScope(new Dictionary<string, Guid>()
            {
                       { "CorrelationId", context.Message.CorrelationId }
                   }))

                _backgroundJobProvider.Enqueue<ImageProcessedProcessor>(x => x.Process(context.Message));
        }
    }
}
