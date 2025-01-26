using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Producer.Infrastructure.Data;

namespace SportsData.Producer.Application.Images.Processors.Requests
{
    public class FranchiseSeasonLogoRequestProcessor : IProcessLogoAndImageRequests
    {
        private readonly ILogger<FranchiseSeasonLogoRequestProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideHashes _hashProvider;
        private readonly IDecodeDocumentProvidersAndTypes _documentTypeDecoder;
        private readonly IPublishEndpoint _bus;
        private readonly IProvideProviders _providerClient;

        public FranchiseSeasonLogoRequestProcessor(
            ILogger<FranchiseSeasonLogoRequestProcessor> logger,
            AppDataContext dataContext,
            IProvideHashes hashProvider,
            IDecodeDocumentProvidersAndTypes documentTypeDecoder,
            IPublishEndpoint bus,
            IProvideProviders providerClient)
        {
            _logger = logger;
            _dataContext = dataContext;
            _hashProvider = hashProvider;
            _documentTypeDecoder = documentTypeDecoder;
            _bus = bus;
            _providerClient = providerClient;
        }

        public async Task ProcessRequest(ProcessImageRequest request)
        {
            var franchiseSeason = await _dataContext.FranchiseSeasons
                .Include(v => v.Logos)
                .Where(x => x.Id == request.ParentEntityId)
                .FirstOrDefaultAsync();

            if (franchiseSeason == null)
            {
                _logger.LogError("Could not retrieve franchiseSeason");
                return;
            }

            var urlHash = _hashProvider.GenerateHashFromUrl(request.Url);

            var logo = franchiseSeason.Logos.FirstOrDefault(x => x.OriginalUrlHash == urlHash);

            var logoDocType = _documentTypeDecoder.GetLogoDocumentTypeFromDocumentType(request.DocumentType);

            if (logo is not null)
            {
                _logger.LogWarning("franchiseSeason logo already exists. Will publish event and exit.");

                // TODO: Do I REALLY need to publish this event? It will just cause more work for downstream

                var outgoingEvt = new ProcessImageResponse(
                    logo.Url,
                    logo.Id.ToString(),
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

            _logger.LogInformation("Requesting new image");

            var response = await _providerClient.GetExternalDocument(query);

            _logger.LogInformation("Obtained new image");

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
        }
    }
}
