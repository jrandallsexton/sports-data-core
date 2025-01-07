using MassTransit;
using MongoDB.Driver;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Provider.Infrastructure.Providers.Espn;

namespace SportsData.Provider.Application.Jobs
{
    public interface IProvideFranchises : IAmARecurringJob { }

    public class FranchiseProviderJob : IProvideFranchises
    {
        private readonly ILogger<VenueProviderJob> _logger;
        private readonly IProvideEspnApiData _espnApi;
        private readonly IMongoCollection<EspnVenueDto> _venues;
        private readonly IBus _bus;

        public Task ExecuteAsync()
        {
            throw new NotImplementedException();
        }
    }
}
