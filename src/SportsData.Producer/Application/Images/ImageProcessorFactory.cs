using SportsData.Core.Common;
using SportsData.Producer.Application.Images.Processors;
using SportsData.Producer.Application.Images.Processors.Requests;
using SportsData.Producer.Application.Images.Processors.Responses;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;

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
                case DocumentType.Athlete:
                case DocumentType.AthleteBySeason:
                case DocumentType.AthleteImage:
                    return _serviceProvider.GetRequiredService<AthleteImageRequestProcessor<FootballDataContext>>();
                case DocumentType.Venue:
                case DocumentType.VenueImage:
                    return _serviceProvider.GetRequiredService<VenueImageRequestProcessor<FootballDataContext>>();
                case DocumentType.GroupBySeason:
                case DocumentType.GroupBySeasonLogo:
                    return _serviceProvider.GetRequiredService<GroupSeasonLogoRequestProcessor<FootballDataContext>>();
                case DocumentType.GroupLogo:
                    return _serviceProvider.GetRequiredService<GroupLogoRequestProcessor<FootballDataContext>>();
                case DocumentType.Franchise:
                case DocumentType.FranchiseLogo:
                    return _serviceProvider.GetRequiredService<FranchiseLogoRequestProcessor<FootballDataContext>>();
                case DocumentType.TeamInformation:
                case DocumentType.TeamBySeason:
                case DocumentType.TeamBySeasonLogo:
                    return _serviceProvider.GetRequiredService<FranchiseSeasonLogoRequestProcessor<FootballDataContext>>();
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
                case DocumentType.Athlete:
                case DocumentType.AthleteBySeason:
                case DocumentType.AthleteImage:
                    return _serviceProvider.GetRequiredService<AthleteImageResponseProcessor<FootballDataContext>>();
                case DocumentType.Venue:
                case DocumentType.VenueImage:
                    return _serviceProvider.GetRequiredService<VenueImageResponseProcessor<FootballDataContext>>();
                case DocumentType.GroupBySeason:
                case DocumentType.GroupBySeasonLogo:
                    return _serviceProvider.GetRequiredService<GroupSeasonLogoResponseProcessor<FootballDataContext>>();
                case DocumentType.GroupLogo:
                    return _serviceProvider.GetRequiredService<GroupLogoResponseProcessor<FootballDataContext>>();
                case DocumentType.Franchise:
                case DocumentType.FranchiseLogo:
                    return _serviceProvider.GetRequiredService<FranchiseLogoResponseProcessor<FootballDataContext>>();
                case DocumentType.TeamInformation:
                case DocumentType.TeamBySeason:
                case DocumentType.TeamBySeasonLogo:
                    return _serviceProvider.GetRequiredService<FranchiseSeasonLogoResponseProcessor<FootballDataContext>>();
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
