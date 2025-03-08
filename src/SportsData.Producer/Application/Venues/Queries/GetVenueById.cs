using AutoMapper;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Models.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Venues.Queries
{
    public class GetVenueById
    {
        public class Query(Guid id) : IRequest<Result<Dto>>
        {
            public Guid Id { get; init; } = id;
        }

        public class Dto : VenueCanonicalModel, IMapFrom<Venue>
        {
            public void Mapping(Profile profile)
            {
                profile.CreateMap<Venue, Dto>();
            }
        }

        public class Validator : AbstractValidator<Query>
        {
            public Validator()
            {
                RuleFor(x => x.Id).NotNull().NotEmpty().WithMessage("VenueId must be present and valid");
            }
        }

        public class Handler : IRequestHandler<Query, Result<Dto>>
        {
            private readonly ILogger<Handler> _logger;
            private readonly BaseDataContext _dataContext;
            private readonly IMapper _mapper;

            public Handler(
                ILogger<Handler> logger,
                BaseDataContext dataContext,
                IMapper mapper)
            {
                _logger = logger;
                _dataContext = dataContext;
                _mapper = mapper;
            }

            public async Task<Result<Dto>> Handle(Query query, CancellationToken cancellationToken)
            {
                _logger.LogInformation("Request began with {@query}", query);

                var entity = await _dataContext.Venues
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == query.Id, cancellationToken: cancellationToken);
                
                // TODO: Handle null result here to eventually produce Http404

                var dto = _mapper.Map<Dto>(entity);

                return new Success<Dto>(dto);
            }
        }
    }
}
