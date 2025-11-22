using AutoMapper;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.DependencyInjection;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Geo;

[Route("api/venues")]
[ApiController]
public class VenuesController : ControllerBase
{
    private readonly ILogger<VenuesController> _logger;
    private readonly IDataContextFactory _contextFactory;
    private readonly IAppMode _appMode;
    private readonly IMapper _mapper;
    private readonly IGeocodingService _geocodingService;

    public VenuesController(
        ILogger<VenuesController> logger,
        IDataContextFactory contextFactory,
        IAppMode appMode,
        IMapper mapper,
        IGeocodingService geocodingService)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        _appMode = appMode;
        _mapper = mapper;
        _geocodingService = geocodingService;
    }

    [HttpGet]
    public async Task<IActionResult> GetVenues(
        CancellationToken cancellationToken)
    {
        var context = _contextFactory.Resolve(_appMode.CurrentSport);

        _logger.LogDebug("Fetching all venues for sport: {Sport}", _appMode.CurrentSport);

        var venues = await context.Venues
            .Include(v => v.Images)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Fetched {VenueCount} venues", venues.Count);

        var dtos = _mapper.Map<List<VenueDto>>(venues);
        return Ok(dtos);
    }

    [HttpGet("{identifier}")]
    public async Task<IActionResult> GetVenueById(
        string identifier,
        CancellationToken cancellationToken)
    {
        var context = _contextFactory.Resolve(_appMode.CurrentSport);

        _logger.LogDebug("Resolving venue by identifier: {Identifier}", identifier);

        if (Guid.TryParse(identifier, out var id))
        {
            var venueById = await context.Venues
                .Include(v => v.Images)
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (venueById == null)
            {
                _logger.LogWarning("Venue not found by ID: {Id}", id);
                return NotFound();
            }

            _logger.LogInformation("Venue resolved by ID: {Id}", id);
            return Ok(_mapper.Map<VenueDto>(venueById));
        }

        var venue = await context.Venues
            .Include(v => v.Images)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Slug == identifier, cancellationToken);

        if (venue == null)
        {
            _logger.LogWarning("Venue not found by Slug: {Slug}", identifier);
            return NotFound();
        }

        _logger.LogInformation("Venue resolved by Slug: {Slug}", identifier);
        return Ok(_mapper.Map<VenueDto>(venue));
    }

    [HttpPost("geo-code")]
    public async Task<IActionResult> Foo()
    {
        var context = _contextFactory.Resolve(_appMode.CurrentSport);

        var venues = await context.Venues
            .Where(x => x.Latitude == 0 || x.Longitude == 0)
            .ToListAsync();

        var venueCount = venues.Count;
        var encodedCount = 0;

        foreach (var venue in venues)
        {
            await Task.Delay(1000); // to avoid rate limiting

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
                context.Venues.Update(venue);
                await context.SaveChangesAsync();
                encodedCount++;
            }
            else
            {
                _logger.LogWarning(
                    "Failed to geocode Venue {VenueId} - {VenueName}",
                    venue.Id,
                    venue.Name);
            }
        }

        return Ok($"{encodedCount}/{venueCount}");
    }

}
