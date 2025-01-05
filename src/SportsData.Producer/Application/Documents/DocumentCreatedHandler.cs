using MassTransit;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Venues;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Models.Canonical;

namespace SportsData.Producer.Application.Documents
{
    public class DocumentCreatedHandler :
        IConsumer<DocumentCreated>
    {
        private readonly ILogger<DocumentCreatedHandler> _logger;
        private readonly IProvideProviders _provider;
        private readonly IBus _bus;

        public DocumentCreatedHandler(
            ILogger<DocumentCreatedHandler> logger,
            IProvideProviders provider,
            IBus bus)
        {
            _logger = logger;
            _provider = provider;
            _bus = bus;
        }

        public async Task Consume(ConsumeContext<DocumentCreated> context)
        {
            _logger.LogInformation("new document event received: {@message}", context.Message);

            switch (context.Message.DocumentType)
            {
                case DocumentType.Athlete:
                case DocumentType.Award:
                case DocumentType.Event:
                case DocumentType.Franchise:
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.Team:
                case DocumentType.TeamInformation:
                case DocumentType.Weeks:
                    throw new ArgumentOutOfRangeException();
                case DocumentType.Venue:
                    await HandleVenueDocumentCreated(context);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task HandleVenueDocumentCreated(ConsumeContext<DocumentCreated> context)
        {
            // call Provider to obtain new document
            var document = await _provider.GetDocumentByIdAsync(context.Message.DocumentType, int.Parse(context.Message.Id));
            _logger.LogInformation("obtained new document from Provider");

            VenueCanonicalModel model;
            var evt = new VenueCreated()
            {
                Id = context.Message.Id,
                Name = context.Message.Name
            };

            // generate domain object from it
            switch (context.Message.SourceDataProvider)
            {
                case SourceDataProvider.Espn:
                    // deserialize the DTO
                    var venue = document.FromJson<EspnVenueDto>();

                    // map to the DomainModel

                    // broadcast the event
                    break;
                case SourceDataProvider.SportsDataIO:
                case SourceDataProvider.Cbs:
                case SourceDataProvider.Yahoo:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // broadcast integration event for external consumer(s)
            await _bus.Publish(evt);
            _logger.LogInformation("New {@type} event {@evt}", context.Message.DocumentType, evt);
        }
    }
}
