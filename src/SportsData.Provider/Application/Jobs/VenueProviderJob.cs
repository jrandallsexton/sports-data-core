using MassTransit;

using MongoDB.Driver;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;

namespace SportsData.Provider.Application.Jobs
{
    public interface IProvideVenues : IAmARecurringJob { }

    public class VenueProviderJob : IProvideVenues
    {
        private readonly ILogger<VenueProviderJob> _logger;
        private readonly IProvideEspnApiData _espnApi;
        private readonly IMongoCollection<EspnVenueDto> _venues;
        private readonly IBus _bus;

        public VenueProviderJob(
            ILogger<VenueProviderJob> logger,
            IProvideEspnApiData espnApi,
            DocumentService dataService,
            IBus bus)
        {
            _logger = logger;
            _espnApi = espnApi;
            _bus = bus;
            _venues = dataService.Database?.GetCollection<EspnVenueDto>(nameof(EspnVenueDto));
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation($"Started {nameof(VenueProviderJob)}");

            // Get a list of all venues
            var venues = await _espnApi.Venues(true);

            await venues.items.ForEachAsync(async item =>
            {
                var venue = await _espnApi.Venue(item.id, true);

                var filter = Builders<EspnVenueDto>.Filter.Eq(x => x.Id, venue.Id);
                
                var dbVenueResult = await _venues.FindAsync(filter);
                var dbVenue = await dbVenueResult.FirstOrDefaultAsync();
                if (dbVenue != null)
                {
                    var venueJson = venue.ToJson();
                    var venueDbJson = dbVenue.ToJson();

                    if (string.Compare(venueJson, venueDbJson, StringComparison.InvariantCultureIgnoreCase) != 0)
                    {
                        await _venues.ReplaceOneAsync(filter, venue);
                        var evt = new DocumentUpdated()
                        {
                            Id = venue.Id.ToString(),
                            Name = nameof(EspnVenueDto),
                            SourceDataProvider = SourceDataProvider.Espn,
                            DocumentType = DocumentType.Venue
                        };
                        await _bus.Publish(evt);
                        _logger.LogInformation("Document updated event {@evt}", evt);
                    }
                }
                else
                {
                    await _venues.InsertOneAsync(venue);
                    var evt = new DocumentCreated()
                    {
                        Id = venue.Id.ToString(),
                        Name = nameof(EspnVenueDto),
                        SourceDataProvider = SourceDataProvider.Espn,
                        DocumentType = DocumentType.Venue
                    };
                    await _bus.Publish(evt);
                    _logger.LogInformation("New document event {@evt}", evt);
                }

                // TODO: Images?
            });
        }
    }
}
