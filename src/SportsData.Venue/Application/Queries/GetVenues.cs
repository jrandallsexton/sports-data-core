using MediatR;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Venue.DTOs;
using SportsData.Core.Infrastructure.Clients.Venue.Queries;

namespace SportsData.Venue.Application.Queries;

public class GetVenues
{
    public class Query : GetVenuesRequest, IRequest<Result<Dto>>
    {

    }

    public class Dto : GetVenuesResponse
    {

    }

    public class Handler : IRequestHandler<Query, Result<Dto>>
    {
        public async Task<Result<Dto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var dto = new Dto()
            {
                Venues =
                [
                    new VenueDto()
                    {
                        Id = "1",
                        Name = "Tiger Stadium"
                    }
                ]
            };
            return new Success<Dto>(dto);
        }
    }
}