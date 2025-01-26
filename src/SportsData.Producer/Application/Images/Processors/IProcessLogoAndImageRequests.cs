using SportsData.Core.Eventing.Events.Images;

namespace SportsData.Producer.Application.Images.Processors
{
    public interface IProcessLogoAndImageRequests
    {
        Task ProcessRequest(ProcessImageRequest request);
    }
}
