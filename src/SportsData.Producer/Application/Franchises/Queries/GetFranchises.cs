using AutoMapper;

using MediatR;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Franchises.Queries
{
    public class GetFranchises
    {
        public class Query : IRequest<Result<List<Dto>>>
        {
            public Query()
            {
            }
        }

        public class Dto : FranchiseDto, IMapFrom<Franchise>
        {
            public void Mapping(Profile profile)
            {
                profile.CreateMap<Franchise, Dto>();
            }
        }

        public class Handler : IRequestHandler<Query, Result<List<Dto>>>
        {
            private readonly ILogger<Handler> _logger;
            private readonly TeamSportDataContext _dataContext;
            private readonly IMapper _mapper;

            public Handler(
                ILogger<Handler> logger,
                TeamSportDataContext dataContext,
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

                var entities = await _dataContext.Franchises
                    .AsNoTracking()
                    .ToListAsync(cancellationToken: cancellationToken);

                var dto = _mapper.Map<List<Dto>>(entities);

                return new Success<List<Dto>>(dto);
            }
        }
    }
}
