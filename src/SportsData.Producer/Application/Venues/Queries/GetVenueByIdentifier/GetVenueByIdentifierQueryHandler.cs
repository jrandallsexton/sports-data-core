using AutoMapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Venues.Queries.GetVenueByIdentifier;

public interface IGetVenueByIdentifierQueryHandler
{
    Task<Result<VenueDto>> ExecuteAsync(
        GetVenueByIdentifierQuery query,
        CancellationToken cancellationToken = default);
}

public class GetVenueByIdentifierQueryHandler : IGetVenueByIdentifierQueryHandler
{
    private readonly ILogger<GetVenueByIdentifierQueryHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IMapper _mapper;

    public GetVenueByIdentifierQueryHandler(
        ILogger<GetVenueByIdentifierQueryHandler> logger,
        TeamSportDataContext dataContext,
        IMapper mapper)
    {
        _logger = logger;
        _dataContext = dataContext;
        _mapper = mapper;
    }

    public async Task<Result<VenueDto>> ExecuteAsync(
        GetVenueByIdentifierQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetVenueByIdentifier started. Identifier={Identifier}",
            query.Identifier);

        Venue? venue;

        if (Guid.TryParse(query.Identifier, out var id))
        {
            venue = await _dataContext.Venues
                .Include(v => v.Images)
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (venue == null)
            {
                _logger.LogWarning("Venue not found by ID. Id={Id}", id);
                return new Failure<VenueDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(query.Identifier), $"Venue not found with ID: {id}")]);
            }

            _logger.LogInformation("Venue found by ID. Id={Id}, Name={Name}", id, venue.Name);
        }
        else
        {
            venue = await _dataContext.Venues
                .Include(v => v.Images)
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Slug == query.Identifier, cancellationToken);

            if (venue == null)
            {
                _logger.LogWarning("Venue not found by slug. Slug={Slug}", query.Identifier);
                return new Failure<VenueDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(query.Identifier), $"Venue not found with slug: {query.Identifier}")]);
            }

            _logger.LogInformation("Venue found by slug. Slug={Slug}, Name={Name}", query.Identifier, venue.Name);
        }

        var dto = _mapper.Map<VenueDto>(venue);

        return new Success<VenueDto>(dto);
    }
}
