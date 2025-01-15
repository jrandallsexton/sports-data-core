using MassTransit;

using Newtonsoft.Json;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Eventing.Events.Venues;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
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

            // call Provider to obtain new document
            var document = await _provider.GetDocumentByIdAsync(
                context.Message.SourceDataProvider,
                context.Message.DocumentType,
                int.Parse(context.Message.Id));

            if (document is null or "null")
            {
                _logger.LogError("Failed to obtain document: {@doc}", context.Message);
                return;
            }

            _logger.LogInformation("obtained new document from Provider");

            // TODO: pass this to an on-demand Hangfire job?

            switch (context.Message.DocumentType)
            {
                case DocumentType.TeamBySeason:
                    await HandleTeamBySeasonCreated(context, document);
                    break;
                case DocumentType.Venue:
                    await HandleVenueDocumentCreated(context, document);
                    break;
                case DocumentType.Franchise:
                    await HandleFranchiseCreated(context, document);
                    break;
                case DocumentType.Athlete:
                case DocumentType.Award:
                case DocumentType.Event:
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.Team:
                case DocumentType.TeamInformation:
                case DocumentType.Weeks:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task HandleFranchiseCreated(ConsumeContext<DocumentCreated> context, string document)
        {
            switch (context.Message.SourceDataProvider)
            {
                case SourceDataProvider.Espn:

                    // deserialize the DTO
                    //var espnFranchise = document.FromJson<EspnFranchiseDto>(new JsonSerializerSettings
                    //{
                    //    MetadataPropertyHandling = MetadataPropertyHandling.Ignore
                    //});

                    var espnFranchise = document.FromJson<EspnFranchiseDto>();

                    // TODO: Determine if this entity exists. Do NOT trust that it says it is a new document!

                    // 1. map to the entity and save it
                    // TODO: Move to extension method?
                    var franchiseId = Guid.NewGuid();
                    var franchiseEntity = new Franchise()
                    {
                        Id = franchiseId,
                        Abbreviation = espnFranchise.Abbreviation,
                        ColorCodeHex = espnFranchise.Color,
                        DisplayName = espnFranchise.DisplayName,
                        CreatedUtc = DateTime.UtcNow,
                        CreatedBy = context.Message.CorrelationId,
                        ExternalIds = [new ExternalId() { Id = espnFranchise.Id.ToString(), Provider = SourceDataProvider.Espn }],
                        GlobalId = Guid.NewGuid(),
                        DisplayNameShort = espnFranchise.ShortDisplayName,
                        IsActive = espnFranchise.IsActive,
                        Name = espnFranchise.Name,
                        Nickname = espnFranchise.Nickname,
                        Slug = espnFranchise.Slug,
                        Logos = espnFranchise.Logos.Select(x => new FranchiseLogo()
                        {
                            Id = Guid.NewGuid(),
                            CreatedBy = context.Message.CorrelationId,
                            CreatedUtc = DateTime.UtcNow,
                            FranchiseId = franchiseId,
                            Height = x.Height,
                            Width = x.Width,
                            Url = x.Href.ToString()
                        }).ToList()
                    };
                    await _dataContext.AddAsync(franchiseEntity);
                    await _dataContext.SaveChangesAsync();

                    // 2. raise an event
                    // TODO: Determine if I want to publish all data in the event instead of this chatty stuff
                    var evt = new FranchiseCreated()
                    {
                        Id = espnFranchise.Id.ToString(),
                        Name = context.Message.Name
                    };
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

        private async Task HandleTeamBySeasonCreated(ConsumeContext<DocumentCreated> context, string document)
        {
            //// deserialize the DTO
            //var espnTeamSeason = document.FromJson<EspnTeamSeasonDto>(new JsonSerializerSettings
            //{
            //    MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            //});

            //// TODO: Determine if this entity exists. Do NOT trust that it says it is a new document!

            //// 1. map to the entity and save it
            //// TODO: Move to extension method?
            //var venueEntity = new Venue()
            //{
            //    Id = Guid.NewGuid(),
            //    Name = espnVenue.FullName,
            //    ShortName = espnVenue.ShortName,
            //    IsIndoor = espnVenue.Indoor,
            //    IsGrass = espnVenue.Grass,
            //    CreatedUtc = DateTime.UtcNow,
            //    CreatedBy = context.Message.CorrelationId,
            //    ExternalIds = [new ExternalId() { Id = espnVenue.Id.ToString(), Provider = SourceDataProvider.Espn }],
            //    GlobalId = Guid.NewGuid()
            //};
            //await _dataContext.AddAsync(venueEntity);
            //await _dataContext.SaveChangesAsync();

            //// 2. raise an event
            //// TODO: Determine if I want to publish all data in the event instead of this chatty stuff
            //var evt = new VenueCreated()
            //{
            //    Id = venueEntity.Id.ToString(),
            //    Name = context.Message.Name
            //};
            //await _bus.Publish(evt);
            //_logger.LogInformation("New {@type} event {@evt}", context.Message.DocumentType, evt);

        }

        private async Task HandleVenueDocumentCreated(ConsumeContext<DocumentCreated> context, string document)
        {
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

                    // 1. map to the entity and save it
                    // TODO: Move to extension method?
                    var venueEntity = new Venue()
                    {
                        Id = Guid.NewGuid(),
                        Name = espnVenue.FullName,
                        ShortName = espnVenue.ShortName,
                        IsIndoor = espnVenue.Indoor,
                        IsGrass = espnVenue.Grass,
                        CreatedUtc = DateTime.UtcNow,
                        CreatedBy = context.Message.CorrelationId,
                        ExternalIds = [new ExternalId() { Id = espnVenue.Id.ToString(), Provider = SourceDataProvider.Espn }],
                        GlobalId = Guid.NewGuid()
                    };
                    await _dataContext.AddAsync(venueEntity);
                    await _dataContext.SaveChangesAsync();

                    // 2. raise an event
                    // TODO: Determine if I want to publish all data in the event instead of this chatty stuff
                    var evt = new VenueCreated()
                    {
                        Id = venueEntity.Id.ToString(),
                        Name = context.Message.Name
                    };
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
