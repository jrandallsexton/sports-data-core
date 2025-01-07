using AutoMapper;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Venue.Infrastructure.Data;

namespace SportsData.Venue.Application.Queries;

public class GetVenueById
{
    public class Query : IRequest<Result<Dto>>
    {
        public int Id { get; set; }
    }

    public class Dto : Infrastructure.Data.Entities.Venue, IMapFrom<Infrastructure.Data.Entities.Venue>
    {
        public void Mapping(Profile profile)
        {
            profile.CreateMap<Infrastructure.Data.Entities.Venue, Dto>();
        }
    }

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .NotNull()
                .NotEmpty()
                .GreaterThan(0)
                .WithMessage("VenueId must be present and valid");
        }
    }

    public class Handler : IRequestHandler<Query, Result<Dto>>
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

        public async Task<Result<Dto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var validation = await new Validator().ValidateAsync(query, cancellationToken);
            if (!validation.IsValid)
            {
                return new Failure<Dto>(null, validation.Errors);
            }

            _logger.LogInformation("Request began with {@query}", query);

            var venues = await _dataContext.Venues
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == query.Id, cancellationToken: cancellationToken);

            var dto = _mapper.Map<Dto>(venues);

            return new Success<Dto>(dto);
        }
    }
}