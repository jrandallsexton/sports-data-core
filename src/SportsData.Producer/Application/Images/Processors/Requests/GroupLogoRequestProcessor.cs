using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Images.Processors.Requests
{
    public class GroupLogoRequestProcessor<TDataContext> : IProcessLogoAndImageRequests
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<GroupLogoRequestProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IDecodeDocumentProvidersAndTypes _documentTypeDecoder;
        private readonly IPublishEndpoint _bus;
        private readonly IProvideProviders _providerClient;

        public GroupLogoRequestProcessor(
            ILogger<GroupLogoRequestProcessor<TDataContext>> logger,
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
            var group = await _dataContext.Groups
                .Include(v => v.Logos)
                .Where(x => x.Id == request.ParentEntityId)
                .FirstOrDefaultAsync();

            if (group == null)
            {
                _logger.LogError("Could not retrieve group");
                return;
            }

            var urlHash = HashProvider.GenerateHashFromUrl(request.Url);

            var logo = group.Logos.FirstOrDefault(x => x.OriginalUrlHash == urlHash);

            var logoDocType = _documentTypeDecoder.GetLogoDocumentTypeFromDocumentType(request.DocumentType);

            if (logo is not null)
            {
                _logger.LogWarning("group image already exists. Will publish event and exit.");

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
        }
    }
}
