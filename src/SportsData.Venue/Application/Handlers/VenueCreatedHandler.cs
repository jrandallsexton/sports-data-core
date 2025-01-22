using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.Venues;
using SportsData.Core.Infrastructure.Clients.Producer;
using SportsData.Venue.Infrastructure.Data;

namespace SportsData.Venue.Application.Handlers
{
    public class VenueCreatedHandler : IConsumer<VenueCreated>
    {
        private readonly ILogger<VenueCreatedHandler> _logger;
        private readonly IProvideProducers _producer;
        private readonly AppDataContext _dataContext;

        public VenueCreatedHandler(
            ILogger<VenueCreatedHandler> logger,
            IProvideProducers producer,
            AppDataContext dataContext)
        {
            _logger = logger;
            _producer = producer;
            _dataContext = dataContext;
        }

        public async Task Consume(ConsumeContext<VenueCreated> context)
        {
            _logger.LogInformation("VenueCreated received: {@evt}", context.Message);

            // call the producer to get the canonical model
            var canonicalVenue = await _producer.GetVenue(context.Message.Id);

            if (canonicalVenue == null)
            {
                _logger.LogError("Could not obtain canonical model for {@evt}", context.Message);
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
                Name = canonicalVenue.Name,
                ShortName = canonicalVenue.ShortName,
                CreatedUtc = DateTime.UtcNow,
                CanonicalId = canonicalVenue.Id,
                IsGrass = canonicalVenue.IsGrass,
                IsIndoor = canonicalVenue.IsIndoor,
                CreatedBy = context.Message.CorrelationId
            };

            // store it
            await _dataContext.Venues.AddAsync(entity);
            await _dataContext.SaveChangesAsync();
        }
    }
}