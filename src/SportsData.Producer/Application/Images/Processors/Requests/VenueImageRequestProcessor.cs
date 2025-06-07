using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Images.Processors.Requests
{
    public class VenueImageRequestProcessor<TDataContext> : IProcessLogoAndImageRequests
        where TDataContext : BaseDataContext
    {
        private readonly ILogger<VenueImageRequestProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IDecodeDocumentProvidersAndTypes _documentTypeDecoder;
        private readonly IPublishEndpoint _bus;
        private readonly IProvideProviders _providerClient;

        public VenueImageRequestProcessor(
            ILogger<VenueImageRequestProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IDecodeDocumentProvidersAndTypes documentTypeDecoder,
            IPublishEndpoint bus,
            IProvideProviders providerClient)
        {
            _logger = logger;
            _dataContext = dataContext;
            _documentTypeDecoder = documentTypeDecoder;
            _bus = bus;
            _providerClient = providerClient;
        }

        public async Task ProcessRequest(ProcessImageRequest request)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = request.CorrelationId
                   }))
            {
                await ProcessRequestInternal(request);
            }
        }

        private async Task ProcessRequestInternal(ProcessImageRequest request)
        {
            var entity = await _dataContext.Venues
                .Include(v => v.Images)
                .Where(x => x.Id == request.ParentEntityId)
                .FirstOrDefaultAsync();

            if (entity == null)
            {
                _logger.LogError("Could not retrieve venue");
                return;
            }

            var urlHash = HashProvider.GenerateHashFromUrl(request.Url);

            var img = entity.Images.FirstOrDefault(x => x.OriginalUrlHash == urlHash);

            var logoDocType = _documentTypeDecoder.GetLogoDocumentTypeFromDocumentType(request.DocumentType);

            if (img is not null)
            {
                await HandleExisting(img, urlHash, request, logoDocType);
                return;
            }

            // if not, obtain the image from Provider
            // who will return a link to blob storage or
            // fetch the image, upload it, then return that blob storage url
            // either way, the link we pass to the response will be that of blob storage
            var query = new GetExternalDocumentQuery(
                Guid.NewGuid().ToString(),
                request.Url,
                request.SourceDataProvider,
                request.Sport,
                logoDocType,
                request.SeasonYear
            );

            _logger.LogInformation("Requesting new image with {@Query}", query);

            var response = await _providerClient.GetExternalDocument(query);

            _logger.LogInformation("Obtained new image {@DocumentType}", query.DocumentType);

            // raise an event for whoever requested this
            var outgoingEvt2 = new ProcessImageResponse(
                response.Href,
                response.CanonicalId,
                urlHash,
                request.ParentEntityId,
                request.Name,
                request.Sport,
                request.SeasonYear,
                logoDocType,
                request.SourceDataProvider,
                request.Height,
                request.Width,
                request.Rel,
                request.CorrelationId,
                request.CausationId);

            await _bus.Publish(outgoingEvt2);

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Published ProcessImageResponse");
        }

        private async Task HandleExisting(
            VenueImage img,
            string urlHash,
            ProcessImageRequest request,
            DocumentType logoDocType)
        {
            _logger.LogWarning("Venue image already exists. Will publish event and exit.");

            // TODO: Do I REALLY need to publish this event? It will just cause more work for downstream

            var outgoingEvt = new ProcessImageResponse(
                img.Url,
                img.Id.ToString(),
                urlHash,
                request.ParentEntityId,
                request.Name,
                request.Sport,
                request.SeasonYear,
                logoDocType,
                request.SourceDataProvider,
                request.Height,
                request.Width,
                request.Rel,
                request.CorrelationId,
                request.CausationId);

            await _bus.Publish(outgoingEvt);

            await _dataContext.SaveChangesAsync();
        }
    }
}
