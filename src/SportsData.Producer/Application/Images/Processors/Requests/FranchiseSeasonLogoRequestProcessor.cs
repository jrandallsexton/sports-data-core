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
    [ImageRequestProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeason)]
    [ImageRequestProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.FranchiseSeasonLogo)]
    public class FranchiseSeasonLogoRequestProcessor<TDataContext> : IProcessLogoAndImageRequests
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<FranchiseSeasonLogoRequestProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IDecodeDocumentProvidersAndTypes _documentTypeDecoder;
        private readonly IEventBus _bus;
        private readonly IProvideProviders _providerClient;
        private readonly IGenerateExternalRefIdentities _externalIdentityProvider;

        public FranchiseSeasonLogoRequestProcessor(
            ILogger<FranchiseSeasonLogoRequestProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IDecodeDocumentProvidersAndTypes documentTypeDecoder,
            IEventBus bus,
            IProvideProviders providerClient,
            IGenerateExternalRefIdentities externalIdentityProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _documentTypeDecoder = documentTypeDecoder;
            _bus = bus;
            _providerClient = providerClient;
            _externalIdentityProvider = externalIdentityProvider;
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

            var urlHash = HashProvider.GenerateHashFromUri(request.Url);

            var logo = franchiseSeason.Logos.FirstOrDefault(x => x.OriginalUrlHash == urlHash);

            var logoDocType = _documentTypeDecoder.GetLogoDocumentTypeFromDocumentType(request.DocumentType);

            if (logo is not null)
            {
                _logger.LogWarning("franchiseSeason logo already exists. Will publish event and exit.");

                // TODO: Do I REALLY need to publish this event? It will just cause more work for downstream

                var outgoingEvt = new ProcessImageResponse(
                    logo.Uri,
                    logo.Id.ToString(),
                    urlHash,
                    request.ParentEntityId,
                    request.Name,
                    null,
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

            var logoIdentity = _externalIdentityProvider.Generate(request.Url);

            // if not, obtain the image from Provider
            // who will return a link to blob storage or
            // fetch the image, upload it, then return that blob storage url
            // either way, the link we pass to the response will be that of blob storage
            var query = new GetExternalImageQuery(
                logoIdentity.CanonicalId.ToString(),
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
                null,
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
