using MassTransit;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Application.Images;

namespace SportsData.Producer.Application.Handlers
{
    public class ProcessImageResponseHandler :
        IConsumer<ProcessImageResponse>
    {
        private ILogger<ProcessImageResponseHandler> _logger;
        private readonly IProcessProcessedImages _imageProcessor;

        public ProcessImageResponseHandler(ILogger<ProcessImageResponseHandler> logger, IProcessProcessedImages imageProcessor)
        {
            _logger = logger;
            _imageProcessor = imageProcessor;
        }

        public async Task Consume(ConsumeContext<ProcessImageResponse> context)
        {
            _logger.LogInformation("new ProcessImageResponse event received: {@message}", context.Message);

            var message = context.Message;

            // TODO: Background job
            await _imageProcessor.Process(message);
        }
    }
}
