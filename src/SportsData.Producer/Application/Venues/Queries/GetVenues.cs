using AutoMapper;

using MediatR;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Models.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Venues.Queries
{
    public class GetVenues
    {
        public class Query : IRequest<Result<List<Dto>>>
        {
            public Query()
            {
            }
        }

        public class Dto : VenueCanonicalModel, IMapFrom<Venue>
        {
            public void Mapping(Profile profile)
            {
                profile.CreateMap<Venue, Dto>();
            }
        }

        public class Handler<TDataContext> : IRequestHandler<Query, Result<List<Dto>>> where TDataContext : BaseDataContext
        {
            private readonly ILogger<Handler<TDataContext>> _logger;
            private readonly TDataContext _dataContext;
            private readonly IMapper _mapper;

            public Handler(
                ILogger<Handler<TDataContext>> logger,
                TDataContext dataContext,
                IMapper mapper)
            {
                _logger = logger;
                _dataContext = dataContext;
                _mapper = mapper;
            }

            public async Task<Result<List<Dto>>> Handle(Query query, CancellationToken cancellationToken)
            {
                _logger.LogInformation("Request began with {@query}", query);

                // TODO: Support paging and filtering

                var entities = await _dataContext.Venues
                    .AsNoTracking()
                    .ToListAsync(cancellationToken: cancellationToken);

                var dto = _mapper.Map<List<Dto>>(entities);

                return new Success<List<Dto>>(dto);
            }
        }
    }
}
