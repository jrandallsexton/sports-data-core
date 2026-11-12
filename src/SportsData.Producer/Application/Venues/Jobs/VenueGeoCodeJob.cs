using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Venues.Commands.GeocodeVenue;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Geo;

namespace SportsData.Producer.Application.Venues.Jobs;

public class VenueGeoCodeJob : IAmARecurringJob
{
    private readonly ILogger<VenueGeoCodeJob> _logger;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly TeamSportDataContext _dataContext;
    private readonly IGeoTimeZoneResolver _timeZoneResolver;

    public VenueGeoCodeJob(
        ILogger<VenueGeoCodeJob> logger,
        IProvideBackgroundJobs backgroundJobProvider,
        TeamSportDataContext dataContext,
        IGeoTimeZoneResolver timeZoneResolver)
    {
        _logger = logger;
        _backgroundJobProvider = backgroundJobProvider;
        _dataContext = dataContext;
        _timeZoneResolver = timeZoneResolver;
    }

    public async Task ExecuteAsync()
    {
        await ResolveTimeZonesAsync();

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

    /// <summary>
    /// Geocoded venues without a time zone: existing venues (the column came
    /// after their coordinates) and any whose zone failed to resolve. The
    /// lookup is offline, so this runs inline in one pass.
    /// </summary>
    private async Task ResolveTimeZonesAsync()
    {
        var venues = await _dataContext.Venues
            .Where(x => x.TimeZone == null && x.Latitude != 0 && x.Longitude != 0)
            .ToListAsync();

        if (venues.Count == 0)
            return;

        var resolved = 0;
        foreach (var venue in venues)
        {
            venue.TimeZone = _timeZoneResolver.Resolve(venue.Latitude, venue.Longitude);
            if (venue.TimeZone is not null)
                resolved++;
            else
                _logger.LogWarning(
                    "No time zone for venue. VenueId={VenueId}, Name={Name}, Lat={Lat}, Lon={Lon}",
                    venue.Id, venue.Name, venue.Latitude, venue.Longitude);
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Resolved time zones for {Resolved} of {Count} venues", resolved, venues.Count);
    }
}
