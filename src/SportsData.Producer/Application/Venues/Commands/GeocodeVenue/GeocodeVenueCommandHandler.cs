using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Geo;

namespace SportsData.Producer.Application.Venues.Commands.GeocodeVenue;

public interface IGeocodeVenueCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        GeocodeVenueCommand command,
        CancellationToken cancellationToken = default);
}

public class GeocodeVenueCommandHandler : IGeocodeVenueCommandHandler
{
    private readonly ILogger<GeocodeVenueCommandHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IGeocodingService _geocodingService;

    public GeocodeVenueCommandHandler(
        ILogger<GeocodeVenueCommandHandler> logger,
        TeamSportDataContext dataContext,
        IGeocodingService geocodingService)
    {
        _logger = logger;
        _dataContext = dataContext;
        _geocodingService = geocodingService;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        GeocodeVenueCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GeocodeVenue started. VenueId={VenueId}",
            command.VenueId);

        var venue = await _dataContext.Venues
            .Where(x => x.Id == command.VenueId)
            .FirstOrDefaultAsync(cancellationToken);

        if (venue == null)
        {
            _logger.LogError("Venue not found. VenueId={VenueId}", command.VenueId);
            return new Success<Guid>(command.VenueId);
        }

        var result = await _geocodingService.TryGeocodeAsync(
            $"{venue.Name}, {venue.City}, {venue.State}, {venue.PostalCode}, {venue.Country}");

        if (result is { lat: not null, lng: not null })
        {
            _logger.LogInformation(
                "Geocoded venue successfully. VenueId={VenueId}, Name={Name}, Lat={Lat}, Lon={Lon}",
                venue.Id,
                venue.Name,
                result.lat.Value,
                result.lng.Value);

            venue.Latitude = (decimal)result.lat.Value;
            venue.Longitude = (decimal)result.lng.Value;
            _dataContext.Venues.Update(venue);
            await _dataContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _logger.LogError(
                "Failed to geocode venue. VenueId={VenueId}, Name={Name}",
                venue.Id,
                venue.Name);
        }

        return new Success<Guid>(command.VenueId);
    }
}
