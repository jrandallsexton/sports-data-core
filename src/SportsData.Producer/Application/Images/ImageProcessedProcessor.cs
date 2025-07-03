using SportsData.Core.Eventing.Events.Images;

namespace SportsData.Producer.Application.Images
{
    public interface IProcessProcessedImages
    {
        Task Process(ProcessImageResponse response);
    }

    public class ImageProcessedProcessor : IProcessProcessedImages
    {
        private readonly ILogger<ImageProcessedProcessor> _logger;
        private readonly IImageProcessorFactory _imageProcessorFactory;

        public ImageProcessedProcessor(
            ILogger<ImageProcessedProcessor> logger,
            IImageProcessorFactory imageProcessorFactory)
        {
            _logger = logger;
            _imageProcessorFactory = imageProcessorFactory;
        }

        public async Task Process(ProcessImageResponse response)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = response.CorrelationId
                   }))
            {
                _logger.LogInformation("Began with {@Response}", response);
                await ProcessInternal(response);
            }
        }

        private async Task ProcessInternal(ProcessImageResponse response)
        {
            var processor = _imageProcessorFactory.GetResponseProcessor(response.DocumentType);

            await processor.ProcessResponse(response);
        }
    }
}
