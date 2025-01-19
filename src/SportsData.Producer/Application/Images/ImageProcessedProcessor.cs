using SportsData.Core.Eventing.Events.Images;

namespace SportsData.Producer.Application.Images
{
    public interface IProcessProcessedImages
    {
        Task Process(ProcessImageResponse response);
    }

    public class ImageProcessedProcessor : IProcessProcessedImages
    {
        public Task Process(ProcessImageResponse response)
        {
            throw new NotImplementedException();
        }
    }
}
