using SportsData.Core.Eventing.Events.Images;

namespace SportsData.Producer.Application.Images.Processors;

public interface IProcessLogoAndImageResponses
{
    Task ProcessResponse(ProcessImageResponse response);
}