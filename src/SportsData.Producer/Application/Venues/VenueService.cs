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
        private readonly TeamSportDataContext _dataContext;
        private readonly IGeocodingService _geocodingService;

        public VenueService(
            ILogger<VenueService> logger,
            TeamSportDataContext dataContext,
            IGeocodingService geocodingService)
        {
            _logger = logger;
            _dataContext = dataContext;
            _geocodingService = geocodingService;
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
