using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Images.Processors.Requests
{
    [ImageRequestProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Franchise)]
    [ImageRequestProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.FranchiseLogo)]
    public class FranchiseLogoRequestProcessor<TDataContext> : IProcessLogoAndImageRequests
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<FranchiseLogoRequestProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IDecodeDocumentProvidersAndTypes _documentTypeDecoder;
        private readonly IEventBus _bus;
        private readonly IProvideProviders _providerClient;

        public FranchiseLogoRequestProcessor(
            ILogger<FranchiseLogoRequestProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IDecodeDocumentProvidersAndTypes documentTypeDecoder,
            IEventBus bus,
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
            var franchise = await _dataContext.Franchises
                .AsNoTracking()
                .Include(v => v.Logos)
                .Where(x => x.Id == request.ParentEntityId)
                .FirstOrDefaultAsync();

            if (franchise == null)
            {
                _logger.LogError("Could not retrieve franchise");
                return;
            }

            var urlHash = HashProvider.GenerateHashFromUri(request.Url);

            var logo = franchise.Logos.FirstOrDefault(x => x.OriginalUrlHash == urlHash);

            var logoDocType = _documentTypeDecoder.GetLogoDocumentTypeFromDocumentType(request.DocumentType);

            if (logo is not null)
            {
                _logger.LogWarning("Venue image already exists. Will publish event and exit.");

                // TODO: Do I REALLY need to publish this event? It will just cause more work for downstream

                var outgoingEvt = new ProcessImageResponse(
                    logo.Uri,
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
            var query = new GetExternalImageQuery(
                Guid.NewGuid().ToString(),
                request.Url,
                request.SourceDataProvider,
                request.Sport,
                logoDocType,
                request.SeasonYear
            );

            _logger.LogInformation("Requesting new image");

            var response = await _providerClient.GetExternalImage(query);

            _logger.LogInformation("Obtained new image {@DocumentType}", query.DocumentType);

            // raise an event for whoever requested this
            var outgoingEvt2 = new ProcessImageResponse(
                response.Uri,
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
