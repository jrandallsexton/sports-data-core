using MassTransit;

using Newtonsoft.Json;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Venues;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Models.Canonical;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents
{
    public class DocumentCreatedHandler :
        IConsumer<DocumentCreated>
    {
        private readonly ILogger<DocumentCreatedHandler> _logger;
        private readonly IProvideProviders _provider;
        private readonly IBus _bus;
        private readonly AppDataContext _dataContext;

        public DocumentCreatedHandler(
            ILogger<DocumentCreatedHandler> logger,
            IProvideProviders provider,
            IBus bus,
            AppDataContext dataContext)
        {
            _logger = logger;
            _provider = provider;
            _bus = bus;
            _dataContext = dataContext;
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

            if (document == null)
                _logger.LogWarning("Failed to obtain document: {@doc}", context.Message);

            _logger.LogInformation("obtained new document from Provider");

            // generate domain object from it
            switch (context.Message.SourceDataProvider)
            {
                case SourceDataProvider.Espn:
                    // deserialize the DTO
                    var espnVenue = document.FromJson<EspnVenueDto>(new JsonSerializerSettings
                    {
                        MetadataPropertyHandling = MetadataPropertyHandling.Ignore
                    });

                    // TODO: Determine if this entity exists. Do NOT trust that it says it is a new document!

                    // map to the entity and save it
                    var ety = new Venue()
                    {
                        Id = Guid.NewGuid(),
                        Name = espnVenue.FullName,
                        ShortName = espnVenue.ShortName,
                        IsIndoor = espnVenue.Indoor,
                        IsGrass = espnVenue.Grass,
                        CreatedUtc = DateTime.UtcNow,
                        CreatedBy = context.Message.CorrelationId
                    };
                    await _dataContext.AddAsync(ety);
                    await _dataContext.SaveChangesAsync();
                    var evt = new VenueCreated()
                    {
                        Id = ety.Id.ToString(),
                        Name = context.Message.Name
                    };

                    // TODO: Determine if I want to publish all data in the event instead of this chatty stuff
                    // broadcast integration event for external consumer(s)
                    await _bus.Publish(evt);
                    _logger.LogInformation("New {@type} event {@evt}", context.Message.DocumentType, evt);
                    break;
                case SourceDataProvider.SportsDataIO:
                case SourceDataProvider.Cbs:
                case SourceDataProvider.Yahoo:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }
    }
}
