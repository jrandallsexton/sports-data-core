using SportsData.Core.Common;
using SportsData.Producer.Application.Images.Processors;
using SportsData.Producer.Application.Images.Processors.Requests;
using SportsData.Producer.Application.Images.Processors.Responses;

namespace SportsData.Producer.Application.Images
{
    public interface IImageProcessorFactory
    {
        IProcessLogoAndImageRequests GetRequestProcessor(DocumentType documentType);

        IProcessLogoAndImageResponses GetResponseProcessor(DocumentType documentType);
    }

    public class ImageProcessorFactory : IImageProcessorFactory
    {
        private readonly IDecodeDocumentProvidersAndTypes _documentTypeDecoder;
        private readonly IServiceProvider _serviceProvider;

        public ImageProcessorFactory(
            IDecodeDocumentProvidersAndTypes documentTypeDecoder,
            IServiceProvider serviceProvider)
        {
            _documentTypeDecoder = documentTypeDecoder;
            _serviceProvider = serviceProvider;
        }

        public IProcessLogoAndImageRequests GetRequestProcessor(DocumentType documentType)
        {
            var imageLogoDocumentType = _documentTypeDecoder.GetLogoDocumentTypeFromDocumentType(documentType);

            switch (imageLogoDocumentType)
            {
                case DocumentType.Venue:
                case DocumentType.VenueImage:
                    return _serviceProvider.GetRequiredService<VenueImageRequestProcessor>();
                case DocumentType.GroupBySeason:
                case DocumentType.GroupBySeasonLogo:
                    return _serviceProvider.GetRequiredService<GroupSeasonLogoRequestProcessor>();
                case DocumentType.GroupLogo:
                    return _serviceProvider.GetRequiredService<GroupLogoRequestProcessor>();
                case DocumentType.Franchise:
                case DocumentType.FranchiseLogo:
                    return _serviceProvider.GetRequiredService<FranchiseLogoRequestProcessor>();
                case DocumentType.Team:
                case DocumentType.TeamInformation:
                case DocumentType.TeamBySeason:
                case DocumentType.TeamBySeasonLogo:
                    return _serviceProvider.GetRequiredService<FranchiseSeasonLogoRequestProcessor>();
                case DocumentType.Athlete:
                case DocumentType.AthleteBySeason:
                case DocumentType.Award:
                case DocumentType.CoachBySeason:
                case DocumentType.Contest:
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.Weeks:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IProcessLogoAndImageResponses GetResponseProcessor(DocumentType documentType)
        {
            var imageLogoDocumentType = _documentTypeDecoder.GetLogoDocumentTypeFromDocumentType(documentType);

            switch (imageLogoDocumentType)
            {
                case DocumentType.Venue:
                case DocumentType.VenueImage:
                    return _serviceProvider.GetRequiredService<VenueImageResponseProcessor>();
                case DocumentType.GroupBySeason:
                case DocumentType.GroupBySeasonLogo:
                    return _serviceProvider.GetRequiredService<GroupSeasonLogoResponseProcessor>();
                case DocumentType.GroupLogo:
                    return _serviceProvider.GetRequiredService<GroupLogoResponseProcessor>();
                case DocumentType.Franchise:
                case DocumentType.FranchiseLogo:
                    return _serviceProvider.GetRequiredService<FranchiseLogoResponseProcessor>();
                case DocumentType.Team:
                case DocumentType.TeamInformation:
                case DocumentType.TeamBySeason:
                case DocumentType.TeamBySeasonLogo:
                    return _serviceProvider.GetRequiredService<FranchiseSeasonLogoResponseProcessor>();
                case DocumentType.Athlete:
                case DocumentType.AthleteBySeason:
                case DocumentType.Award:
                case DocumentType.CoachBySeason:
                case DocumentType.Contest:
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.Weeks:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
