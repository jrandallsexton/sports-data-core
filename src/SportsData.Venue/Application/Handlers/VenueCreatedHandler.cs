using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.Venues;
using SportsData.Venue.Infrastructure.Data;

namespace SportsData.Venue.Application.Handlers
{
    public class VenueCreatedHandler : IConsumer<VenueCreated>
    {
        private readonly ILogger<VenueCreatedHandler> _logger;
        private readonly AppDataContext _dataContext;

        public VenueCreatedHandler(
            ILogger<VenueCreatedHandler> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task Consume(ConsumeContext<VenueCreated> context)
        {
            _logger.LogInformation("VenueCreated received: {@evt}", context.Message);

            // get the canonical model
            var canonicalVenue = context.Message.Canonical;

            if (canonicalVenue is null)
            {
                _logger.LogError("Canonical data was null");
                throw new Exception("foo");
            }

            // the event says it is new - check anyway
            var exists = await _dataContext.Venues.AnyAsync(x => x.CanonicalId == canonicalVenue.Id);
            if (exists)
            {
                _logger.LogWarning($"Venue already exists for CanonicalId: {canonicalVenue.Id}");
                return;
            }

            // map it to our entity
            var entity = new Infrastructure.Data.Entities.Venue()
            {
                Id = -1, // TODO: Revisit once we bring this service online (not anytime soon)
                Name = canonicalVenue.Name,
                ShortName = canonicalVenue.ShortName,
                CreatedUtc = DateTime.UtcNow,
                CanonicalId = canonicalVenue.Id,
                IsGrass = canonicalVenue.IsGrass,
                IsIndoor = canonicalVenue.IsIndoor,
                CreatedBy = context.Message.CorrelationId,
                UrlHash = Guid.NewGuid().ToString() // TODO: This will point to the URL in Producer
            };

            // store it
            await _dataContext.Venues.AddAsync(entity);

            // TODO: Raise another event?  Perhaps to tell API to invalidate cache?

            await _dataContext.SaveChangesAsync();
        }
    }
}