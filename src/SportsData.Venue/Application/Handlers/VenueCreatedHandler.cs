using MassTransit;

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
            var venue = await _producer.GetVenue(context.Message.Id);

            // map it to our domain model

            // store it
        }
    }
}