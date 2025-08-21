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
    [ImageRequestProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Athlete)]
    [ImageRequestProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthleteSeason)]
    [ImageRequestProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthleteImage)]
    public class AthleteImageRequestProcessor<TDataContext> : IProcessLogoAndImageRequests
        where TDataContext : BaseDataContext
    {
        private readonly ILogger<AthleteImageRequestProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IDecodeDocumentProvidersAndTypes _documentTypeDecoder;
        private readonly IEventBus _bus;
        private readonly IProvideProviders _providerClient;

        public AthleteImageRequestProcessor(
            ILogger<AthleteImageRequestProcessor<TDataContext>> logger,
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

            if (!response.IsSuccess)
            {
                _logger.LogError("Failed to obtain image from Provider");
                throw new Exception("Failed to obtain image from Provider");
            }

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

            _logger.LogInformation("Published ProcessImageResponse");

        }

        private async Task HandleExisting(
            AthleteImage img,
            string urlHash,
            ProcessImageRequest request,
            DocumentType logoDocType)
        {
            await Task.Delay(100);
            _logger.LogWarning("Update detected; not implemented");
            return;
        }
    }
}
