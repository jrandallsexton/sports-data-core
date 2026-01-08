using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Venues.Commands.GeocodeVenue;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Venues;

public class VenueGeoCodeJob : IAmARecurringJob
{
    private readonly ILogger<VenueGeoCodeJob> _logger;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
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

        _logger.LogInformation("Found {Count} venues to geocode", venues.Count);

        foreach (var venue in venues)
        {
            var command = new GeocodeVenueCommand(venue.Id);
            _backgroundJobProvider.Enqueue<IGeocodeVenueCommandHandler>(
                h => h.ExecuteAsync(command, CancellationToken.None));

            await Task.Delay(2000); // small delay to avoid overwhelming the queue
        }
    }
}
