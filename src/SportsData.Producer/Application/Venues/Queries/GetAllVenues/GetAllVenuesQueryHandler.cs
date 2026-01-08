using AutoMapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Venues.Queries.GetAllVenues;

public interface IGetAllVenuesQueryHandler
{
    Task<Result<List<VenueDto>>> ExecuteAsync(
        GetAllVenuesQuery query,
        CancellationToken cancellationToken = default);
}

public class GetAllVenuesQueryHandler : IGetAllVenuesQueryHandler
{
    private readonly ILogger<GetAllVenuesQueryHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IMapper _mapper;

    public GetAllVenuesQueryHandler(
        ILogger<GetAllVenuesQueryHandler> logger,
        TeamSportDataContext dataContext,
        IMapper mapper)
    {
        _logger = logger;
        _dataContext = dataContext;
        _mapper = mapper;
    }

    public async Task<Result<List<VenueDto>>> ExecuteAsync(
        GetAllVenuesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetAllVenues started");

        var venues = await _dataContext.Venues
            .Include(v => v.Images)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<VenueDto>>(venues);

        _logger.LogInformation("GetAllVenues completed. Count={Count}", dtos.Count);

        return new Success<List<VenueDto>>(dtos);
    }
}
