using AutoMapper;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Franchises.Queries
{
    public class GetFranchiseById
    {
        public class Query(Guid id) : IRequest<Result<Dto>>
        {
            public Guid Id { get; init; } = id;
        }

        public record Dto : FranchiseDto, IMapFrom<Franchise>
        {
            public void Mapping(Profile profile)
            {
                profile.CreateMap<Franchise, Dto>();
            }
        }

        public class Validator : AbstractValidator<Query>
        {
            public Validator()
            {
                RuleFor(x => x.Id).NotNull().NotEmpty().WithMessage("FranchiseId must be present and valid");
            }
        }

        public class Handler : IRequestHandler<Query, Result<Dto>>
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

            public async Task<Result<Dto>> Handle(Query query, CancellationToken cancellationToken)
            {
                _logger.LogInformation("Request began with {@query}", query);

                // TODO: Support paging and filtering

                var entities = await _dataContext.Franchises
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == query.Id, cancellationToken: cancellationToken);

                var dto = _mapper.Map<Dto>(entities);

                return new Success<Dto>(dto);
            }
        }
    }
}
