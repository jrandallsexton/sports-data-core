using MassTransit;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Application.Images;

namespace SportsData.Producer.Application.Handlers
{
    public class ProcessImageRequestedHandler :
        IConsumer<ProcessImageRequest>
    {
        private readonly ILogger<ProcessImageRequestedHandler> _logger;
        private readonly IProcessImageRequests _imageRequestProcessor;

        public ProcessImageRequestedHandler(
            ILogger<ProcessImageRequestedHandler> logger,
            IProcessImageRequests imageRequestProcessor)
        {
            _logger = logger;
            _imageRequestProcessor = imageRequestProcessor;
        }

        public async Task Consume(ConsumeContext<ProcessImageRequest> context)
        {
            _logger.LogInformation("new ProcessImageRequest event received: {@message}", context.Message);

            var message = context.Message;

            await _imageRequestProcessor.Process(message);
        }
    }
}
