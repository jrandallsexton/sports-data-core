using SportsData.Core.Eventing.Events.Images;

namespace SportsData.Producer.Application.Images
{
    public interface IProcessImageRequests
    {
        Task Process(ProcessImageRequest request);
    }

    public class ImageRequestedProcessor : IProcessImageRequests
    {
        private readonly ILogger<ImageRequestedProcessor> _logger;
        private readonly IImageProcessorFactory _imageProcessorFactory;

        public ImageRequestedProcessor(
            ILogger<ImageRequestedProcessor> logger,
            IImageProcessorFactory imageProcessorFactory)
        {
            _logger = logger;
            _imageProcessorFactory = imageProcessorFactory;
        }

        public async Task Process(ProcessImageRequest request)
        {
            _logger.LogInformation("Began with {@request}", request);
            using (_logger.BeginScope(new Dictionary<string, Guid>()
                   {
                       { "CorrelationId", request.CorrelationId }
                   }))

            await ProcessInternal(request);
        }

        private async Task ProcessInternal(ProcessImageRequest request)
        {
            var processor = _imageProcessorFactory.GetRequestProcessor(request.DocumentType);

            await processor.ProcessRequest(request);
        }
    }
}
