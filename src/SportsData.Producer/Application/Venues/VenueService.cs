using Microsoft.EntityFrameworkCore;

using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Geo;

namespace SportsData.Producer.Application.Venues
{
    public interface IVenueService
    {
        Task GeocodeVenue(Guid venueId);
    }

    public class VenueService : IVenueService
    {
        private readonly ILogger<VenueService> _logger;
        private readonly IGeocodingService _geocodingService;
        private readonly TeamSportDataContext _dataContext;

        public VenueService(
            ILogger<VenueService> logger,
            IGeocodingService geocodingService,
            TeamSportDataContext dataContext)
        {
            _logger = logger;
            _geocodingService = geocodingService;
            _dataContext = dataContext;
        }

        public async Task GeocodeVenue(Guid venueId)
        {
            var venue = await _dataContext.Venues
                .Where(x => x.Id == venueId)
                .FirstOrDefaultAsync();

            if (venue == null)
            {
                _logger.LogError("Venue not found: {VenueId}", venueId);
                return;
            }

            var result = await _geocodingService.TryGeocodeAsync(
                $"{venue.Name}, {venue.City}, {venue.State}, {venue.PostalCode}, {venue.Country}");

            if (result is { lat: not null, lng: not null })
            {
                _logger.LogInformation(
                    "Geocoded Venue {VenueId} - {VenueName}: Lat={Lat}, Lon={Lon}",
                    venue.Id,
                    venue.Name,
                    result.lat.Value,
                    result.lng.Value);
                venue.Latitude = (decimal)result.lat.Value;
                venue.Longitude = (decimal)result.lng.Value;
                _dataContext.Venues.Update(venue);
                await _dataContext.SaveChangesAsync();
            }
            else
            {
                _logger.LogError(
                    "Failed to geocode Venue {VenueId} - {VenueName}",
                    venue.Id,
                    venue.Name);
            }
        }
    }
}
