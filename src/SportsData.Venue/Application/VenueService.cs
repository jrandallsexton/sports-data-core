using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using SportsData.Venue.Infrastructure.Data;

namespace SportsData.Venue.Application
{
    public class VenueService : Venue.VenueBase
    {
        private readonly ILogger<VenueService> _logger;
        private readonly AppDataContext _dataContext;

        public VenueService(
            ILogger<VenueService> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public override async Task<GetVenueResponse> GetVenue(GetVenueRequest request, ServerCallContext context)
        {
            var venue = await _dataContext.Venues.SingleOrDefaultAsync(x => x.Id == request.Id);

            return new GetVenueResponse()
            {
                Id = request.Id,
                IsIndoor = venue.IsIndoor,
                IsGrass = venue.IsGrass,
                Name = venue.Name
            };
        }
    }
}
