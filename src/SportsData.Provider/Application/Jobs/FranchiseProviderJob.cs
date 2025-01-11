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
    public interface IProvideFranchises : IAmARecurringJob { }

    public class FranchiseProviderJob : IProvideFranchises
    {
        private readonly ILogger<FranchiseProviderJob> _logger;
        private readonly IProvideEspnApiData _espnApi;
        private readonly IMongoCollection<EspnFranchiseDto> _franchises;
        private readonly IBus _bus;

        public FranchiseProviderJob(
            ILogger<FranchiseProviderJob> logger,
            IProvideEspnApiData espnApi,
            DocumentService dataService,
            IBus bus)
        {
            _logger = logger;
            _espnApi = espnApi;
            _bus = bus;
            _franchises = dataService.Database?.GetCollection<EspnFranchiseDto>(nameof(EspnFranchiseDto));
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation($"Started {nameof(FranchiseProviderJob)}");

            // Get a list of all franchises
            var franchises = await _espnApi.Franchises(true);

            await franchises.items.ForEachAsync(async item =>
            {
                var franchise = await _espnApi.Franchise(item.id, true);

                var filter = Builders<EspnFranchiseDto>.Filter.Eq(x => x.Id, franchise.Id);

                var dbFranchiseResult = await _franchises.FindAsync(filter);
                var dbVenue = await dbFranchiseResult.FirstOrDefaultAsync();
                if (dbVenue != null)
                {
                    var venueJson = franchise.ToJson();
                    var venueDbJson = dbVenue.ToJson();

                    if (string.Compare(venueJson, venueDbJson, StringComparison.InvariantCultureIgnoreCase) != 0)
                    {
                        await _franchises.ReplaceOneAsync(filter, franchise);
                        var evt = new DocumentUpdated()
                        {
                            Id = franchise.Id.ToString(),
                            Name = nameof(EspnVenueDto),
                            SourceDataProvider = SourceDataProvider.Espn,
                            DocumentType = DocumentType.Franchise
                        };
                        await _bus.Publish(evt);
                        _logger.LogInformation("Document updated event {@evt}", evt);
                    }
                }
                else
                {
                    await _franchises.InsertOneAsync(franchise);
                    var evt = new DocumentCreated()
                    {
                        Id = franchise.Id.ToString(),
                        Name = nameof(EspnVenueDto),
                        SourceDataProvider = SourceDataProvider.Espn,
                        DocumentType = DocumentType.Franchise
                    };
                    await _bus.Publish(evt);
                    _logger.LogInformation("New document event {@evt}", evt);
                }

                // TODO: Images?
            });
        }
    }
}
