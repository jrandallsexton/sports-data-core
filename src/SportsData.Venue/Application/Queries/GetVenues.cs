using AutoMapper;

using MediatR;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Common.Queries;
using SportsData.Venue.Infrastructure.Data;

namespace SportsData.Venue.Application.Queries;

public class GetVenues
{
    public class Query :
        CacheableQuery<Result<List<Dto>>> { }

    public class Dto : Infrastructure.Data.Entities.Venue, IMapFrom<Infrastructure.Data.Entities.Venue>
    {
        public void Mapping(Profile profile)
        {
            profile.CreateMap<Infrastructure.Data.Entities.Venue, Dto>();
        }
    }

    public class Handler : IRequestHandler<Query, Result<List<Dto>>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IMapper _mapper;

        public Handler(
            ILogger<Handler> logger,
            AppDataContext dataContext,
            IMapper mapper)
        {
            _logger = logger;
            _dataContext = dataContext;
            _mapper = mapper;
        }

        public async Task<Result<List<Dto>>> Handle(Query query, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Request began with {@query}", query);

            var venues = await _dataContext.Venues
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken: cancellationToken);

            var dtos = _mapper.Map<List<Dto>>(venues);

            return new Success<List<Dto>>(dtos);
        }
    }
}