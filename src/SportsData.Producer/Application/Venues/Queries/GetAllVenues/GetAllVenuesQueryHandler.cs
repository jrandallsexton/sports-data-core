using AutoMapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Venues.Queries.GetAllVenues;

public interface IGetAllVenuesQueryHandler
{
    Task<Result<GetAllVenuesResponse>> ExecuteAsync(
        GetAllVenuesQuery query,
        CancellationToken cancellationToken = default);
}

public class GetAllVenuesQueryHandler : IGetAllVenuesQueryHandler
{
    private readonly ILogger<GetAllVenuesQueryHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IMapper _mapper;
    private readonly IGenerateResourceRefs _refGenerator;

    public GetAllVenuesQueryHandler(
        ILogger<GetAllVenuesQueryHandler> logger,
        TeamSportDataContext dataContext,
        IMapper mapper,
        IGenerateResourceRefs refGenerator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _mapper = mapper;
        _refGenerator = refGenerator;
    }

    public async Task<Result<GetAllVenuesResponse>> ExecuteAsync(
        GetAllVenuesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetAllVenues started");

        var venues = await _dataContext.Venues
            .Include(v => v.Images)
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<VenueDto>>(venues);

        foreach (var dto in dtos)
        {
            dto.Ref = _refGenerator.ForVenue(dto.Id);
        }

        _logger.LogInformation("GetAllVenues completed. Count={Count}", dtos.Count);

        return new Success<GetAllVenuesResponse>(new GetAllVenuesResponse()
        {
            Count = dtos.Count,
            Items = dtos
        });
    }
}
