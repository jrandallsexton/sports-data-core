using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Infrastructure.Blobs;
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
        private readonly IProvideBlobStorage _blobStorage;
        private readonly IBus _bus;
        private readonly IProvideProviders _providerClient;

        public ImageRequestedProcessor(
            ILogger<ImageRequestedProcessor> logger,
            AppDataContext dataContext,
            IBus bus,
            IProvideBlobStorage blobStorage,
            IProvideProviders providerClient)
        {
            _logger = logger;
            _dataContext = dataContext;
            _bus = bus;
            _blobStorage = blobStorage;
            _providerClient = providerClient;
        }

        public async Task Process(ProcessImageRequest request)
        {
            _logger.LogInformation("Began with {@request}", request);

            var logoEntity = await GetLogoEntity(request.DocumentType, request.ParentEntityId);
            ProcessImageResponse outgoingEvt;

            if (logoEntity != null)
            {
                // if so, just create the event
                outgoingEvt = new ProcessImageResponse(
                    logoEntity.Url,
                    logoEntity.Id.ToString(),
                    request.ParentEntityId,
                    request.Name,
                    request.Sport,
                    request.SeasonYear,
                    request.DocumentType,
                    request.SourceDataProvider,
                    request.Height,
                    request.Width,
                    request.Rel);
            }
            else
            {
                // if not, obtain the image from Provider
                // who will return a link to blob storage or
                // fetch the image, upload it, then return that blob storage url
                // either way, the link we pass to the response will be that of blob storage
                var query = new GetExternalDocumentQuery()
                {
                    DocumentType = request.DocumentType,
                    SeasonYear = request.SeasonYear,
                    SourceDataProvider = request.SourceDataProvider,
                    Sport = request.Sport,
                    Url = request.Url
                };

                var response = await _providerClient.GetExternalDocument(query);

                // raise an event for whoever requested this
                outgoingEvt = new ProcessImageResponse(
                    response.Href,
                    request.ImageId.ToString(),
                    request.ParentEntityId,
                    request.Name,
                    request.Sport,
                    request.SeasonYear,
                    request.DocumentType,
                    request.SourceDataProvider,
                    request.Height,
                    request.Width,
                    request.Rel);
            }

            await _bus.Publish(outgoingEvt);
        }

        private async Task<ILogo?> GetLogoEntity(DocumentType documentType, Guid parentEntityId)
        {
            switch (documentType)
            {
                case DocumentType.Franchise:
                case DocumentType.FranchiseLogo:
                    return await _dataContext.FranchiseLogos
                        .Where(l => l.FranchiseId == parentEntityId)
                        .FirstOrDefaultAsync();
                case DocumentType.GroupBySeason:
                    return await _dataContext.GroupSeasonLogos
                        .Where(l => l.GroupSeasonId == parentEntityId)
                        .FirstOrDefaultAsync();
                case DocumentType.TeamBySeason:
                    return await _dataContext.FranchiseSeasonLogos
                        .Where(l => l.FranchiseSeasonId == parentEntityId)
                        .FirstOrDefaultAsync();
                case DocumentType.GroupLogo:
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
                case DocumentType.Venue:
                case DocumentType.Weeks:
                default:
                    throw new ArgumentOutOfRangeException(nameof(documentType), documentType, null);
            }
        }
    }
}
