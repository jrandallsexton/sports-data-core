using Microsoft.EntityFrameworkCore;

using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Venues
{
    public class VenueGeoCodeJob : IAmARecurringJob
    {
        private readonly ILogger<VenueGeoCodeJob> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        // TODO: This should use BaseDataContext, but it is abstract - needs refactor
        private readonly TeamSportDataContext _dataContext;

        public VenueGeoCodeJob(
            ILogger<VenueGeoCodeJob> logger,
            IProvideBackgroundJobs backgroundJobProvider,
            TeamSportDataContext dataContext)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
            _dataContext = dataContext;
        }

        public async Task ExecuteAsync()
        {
            var venues = await _dataContext.Venues
                .Where(x => x.Latitude == 0 || x.Longitude == 0)
                .ToListAsync();

            foreach (var venue in venues)
            {
                _backgroundJobProvider.Enqueue<IVenueService>(p => p.GeocodeVenue(venue.Id));

                await Task.Delay(2000); // small delay to avoid overwhelming the queue
            }
        }
    }
}
