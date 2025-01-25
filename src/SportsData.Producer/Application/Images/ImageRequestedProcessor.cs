using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Images
{
    public interface IProcessImageRequests
    {
        Task Process(ProcessImageRequest request);
    }

    public class ImageRequestedProcessor : IProcessImageRequests
    {
        private readonly ILogger<ImageRequestedProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IPublishEndpoint _bus;
        private readonly IProvideProviders _providerClient;

        public ImageRequestedProcessor(
            ILogger<ImageRequestedProcessor> logger,
            AppDataContext dataContext,
            IPublishEndpoint bus,
            IProvideProviders providerClient)
        {
            _logger = logger;
            _dataContext = dataContext;
            _bus = bus;
            _providerClient = providerClient;
        }

        public async Task Process(ProcessImageRequest request)
        {
            _logger.LogInformation("Began with {@request}", request);
            using (_logger.BeginScope(new Dictionary<string, Guid>()
                   {
                       { "CorrelationId", request.CorrelationId }
                   }))

            await ProcessImageRequest(request);
        }

        private async Task ProcessImageRequest(ProcessImageRequest request)
        {
            var logoEntity = await GetLogoParentEntityAndLogoType(request.DocumentType, request.ParentEntityId);

            //ProcessImageResponse outgoingEvt;

            //if (logoEntity.logo != null)
            //{
            //    // if so, just create the event
            //    outgoingEvt = new ProcessImageResponse(
            //        logoEntity.logo.Url,
            //        logoEntity.logo.Id.ToString(),
            //        request.ParentEntityId,
            //        request.Name,
            //        request.Sport,
            //        request.SeasonYear,
            //        logoEntity.logoDocumentType,
            //        request.SourceDataProvider,
            //        request.Height,
            //        request.Width,
            //        request.Rel,
            //        request.CorrelationId,
            //        request.CausationId);
            //}
            //else
            //{
            // if not, obtain the image from Provider
            // who will return a link to blob storage or
            // fetch the image, upload it, then return that blob storage url
            // either way, the link we pass to the response will be that of blob storage
            var query = new GetExternalDocumentQuery(
                Guid.NewGuid().ToString(),
                request.Url,
                request.SourceDataProvider,
                request.Sport,
                logoEntity.logoDocumentType,
                request.SeasonYear
            );

            _logger.LogInformation("Requesting new image");

            var response = await _providerClient.GetExternalDocument(query);

            _logger.LogInformation("Obtained new image");

            // raise an event for whoever requested this
            var outgoingEvt = new ProcessImageResponse(
                response.Href,
                response.CanonicalId,
                request.ParentEntityId,
                request.Name,
                request.Sport,
                request.SeasonYear,
                logoEntity.logoDocumentType,
                request.SourceDataProvider,
                request.Height,
                request.Width,
                request.Rel,
                request.CorrelationId,
                request.CausationId);
            //}

            await _bus.Publish(outgoingEvt);
        }

        private async Task<(ILogo? logo, DocumentType logoDocumentType)> GetLogoParentEntityAndLogoType(DocumentType documentType, Guid parentEntityId)
        {
            switch (documentType)
            {
                case DocumentType.Franchise:
                case DocumentType.FranchiseLogo:
                    return (await _dataContext.FranchiseLogos
                        .Where(l => l.FranchiseId == parentEntityId)
                        .FirstOrDefaultAsync(), DocumentType.FranchiseLogo);
                case DocumentType.GroupBySeason:
                case DocumentType.GroupLogo:
                    return (await _dataContext.GroupSeasonLogos
                        .Where(l => l.GroupSeasonId == parentEntityId)
                        .FirstOrDefaultAsync(), DocumentType.GroupBySeasonLogo);
                case DocumentType.TeamBySeason:
                    return (await _dataContext.FranchiseSeasonLogos
                        .Where(l => l.FranchiseSeasonId == parentEntityId)
                        .FirstOrDefaultAsync(), DocumentType.TeamBySeasonLogo);
                case DocumentType.Venue:
                case DocumentType.VenueImage:
                    return (await _dataContext.VenueImages
                        .Where(l => l.VenueId == parentEntityId)
                        .FirstOrDefaultAsync(), DocumentType.VenueImage);
                case DocumentType.Athlete:
                case DocumentType.AthleteBySeason:
                case DocumentType.Award:
                case DocumentType.CoachBySeason:
                case DocumentType.Contest:
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.Team:
                case DocumentType.TeamInformation:
                case DocumentType.Weeks:
                default:
                    throw new ArgumentOutOfRangeException(nameof(documentType), documentType, null);
            }
        }
    }
}
