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
    [ImageRequestProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthleteImage)]
    public class AthleteImageRequestProcessor<TDataContext> : IProcessLogoAndImageRequests
        where TDataContext : BaseDataContext
    {
        private readonly ILogger<AthleteImageRequestProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IDecodeDocumentProvidersAndTypes _documentTypeDecoder;
        private readonly IEventBus _bus;
        private readonly IProvideProviders _providerClient;
        private readonly IGenerateExternalRefIdentities _externalIdentityProvider;

        public AthleteImageRequestProcessor(
            ILogger<AthleteImageRequestProcessor<TDataContext>> logger,
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
            var entity = await _dataContext.Athletes
                .AsNoTracking()
                .Include(v => v.Images)
                .Where(x => x.Id == request.ParentEntityId)
                .FirstOrDefaultAsync();

            if (entity == null)
            {
                _logger.LogError("Could not retrieve AthleteDto");
                return;
            }

            var urlHash = HashProvider.GenerateHashFromUri(request.Url);

            var img = entity.Images.FirstOrDefault(x => x.OriginalUrlHash == urlHash);

            var logoDocType = _documentTypeDecoder.GetLogoDocumentTypeFromDocumentType(request.DocumentType);

            if (img is not null)
            {
                await HandleExisting(img, urlHash, request, logoDocType);
                return;
            }

            var imageIdentity = _externalIdentityProvider.Generate(request.Url);

            // if not, obtain the image from Provider
            // who will return a link to blob storage or
            // fetch the image, upload it, then return that blob storage url
            // either way, the link we pass to the response will be that of blob storage
            var query = new GetExternalImageQuery(
                imageIdentity.CanonicalId.ToString(),
                request.Url,
                request.SourceDataProvider,
                request.Sport,
                logoDocType,
                request.SeasonYear
            );

            _logger.LogInformation("Requesting new image");

            var response = await _providerClient.GetExternalImage(query);

            if (!response.IsSuccess)
            {
                _logger.LogError("Failed to obtain image from Provider");
                throw new Exception("Failed to obtain image from Provider");
            }

            _logger.LogInformation("Obtained new image {@DocumentType}", query.DocumentType);

            // raise an event for whoever requested this
            var outgoingEvt = CreateProcessImageResponse(response, urlHash, request, logoDocType);

            await _bus.Publish(outgoingEvt);
            
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Published ProcessImageResponse");

        }

        private ProcessImageResponse CreateProcessImageResponse(
            SportsData.Core.Infrastructure.Clients.Provider.Commands.GetExternalImageResponse providerResponse,
            string urlHash,
            ProcessImageRequest request,
            DocumentType logoDocType)
        {
            return new ProcessImageResponse(
                providerResponse.Uri,
                providerResponse.CanonicalId,
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
        }

        private async Task HandleExisting(
            AthleteImage img,
            string urlHash,
            ProcessImageRequest request,
            DocumentType logoDocType)
        {
            _logger.LogInformation(
                "Existing AthleteImage found. ImageId={ImageId}, AthleteId={AthleteId}, OriginalUrlHash={OriginalUrlHash}",
                img.Id,
                request.ParentEntityId,
                urlHash);

            // Always call Provider to check if blob storage URI has changed
            // (e.g., storage account migration, re-upload, etc.)
            var imageIdentity = _externalIdentityProvider.Generate(request.Url);

            var query = new GetExternalImageQuery(
                imageIdentity.CanonicalId.ToString(),
                request.Url,
                request.SourceDataProvider,
                request.Sport,
                logoDocType,
                request.SeasonYear
            );

            _logger.LogInformation("Checking for blob storage URI changes for existing image");

            var response = await _providerClient.GetExternalImage(query);

            if (!response.IsSuccess)
            {
                _logger.LogError("Failed to obtain image from Provider during update check");
                throw new Exception("Failed to obtain image from Provider during update check");
            }

            // Check if blob storage URI has changed
            if (img.Uri != response.Uri)
            {
                _logger.LogInformation(
                    "Blob storage URI changed for existing image. ImageId={ImageId}, OldUri={OldUri}, NewUri={NewUri}",
                    img.Id,
                    img.Uri,
                    response.Uri);

                // Publish update event to trigger response processor
                var outgoingEvt = CreateProcessImageResponse(response, urlHash, request, logoDocType);

                await _bus.Publish(outgoingEvt);
                await _dataContext.SaveChangesAsync();

                _logger.LogInformation("Published ProcessImageResponse for updated blob storage URI");
            }
            else
            {
                _logger.LogInformation(
                    "Blob storage URI unchanged for existing image. ImageId={ImageId}, Skipping update.",
                    img.Id);
            }
        }
    }
}
