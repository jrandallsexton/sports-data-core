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
    private readonly IGenerateResourceRefs _refGenerator;

    public GetAllVenuesQueryHandler(
        ILogger<GetAllVenuesQueryHandler> logger,
        TeamSportDataContext dataContext,
        IGenerateResourceRefs refGenerator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _refGenerator = refGenerator;
    }

    public async Task<Result<GetAllVenuesResponse>> ExecuteAsync(
        GetAllVenuesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetAllVenues started. PageNumber={PageNumber}, PageSize={PageSize}",
            query.PageNumber,
            query.PageSize);

        // Validate and clamp pagination parameters
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, GetAllVenuesQuery.MinPageSize, GetAllVenuesQuery.MaxPageSize);

        // Get total count for pagination metadata
        var totalCount = await _dataContext.Venues
            .AsNoTracking()
            .CountAsync(cancellationToken);

        // Calculate pagination values
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = (pageNumber - 1) * pageSize;

        // Get paginated data
        var dtos = await _dataContext.Venues
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Skip(skip)
            .Take(pageSize)
            .Select(v => new VenueDto
            {
                Id = v.Id,
                Name = v.Name,
                ShortName = v.ShortName,
                Slug = v.Slug,
                Capacity = v.Capacity,
                IsGrass = v.IsGrass,
                IsIndoor = v.IsIndoor,
                Latitude = v.Latitude,
                Longitude = v.Longitude,
                Address = new AddressDto
                    {
                        City = v.City,
                        State = v.State
                    }
                // TODO: Add Images projection once LogoDtoBase issue is resolved
            })
            .ToListAsync(cancellationToken);

        var response = new GetAllVenuesResponse
        {
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasPreviousPage = pageNumber > 1,
            HasNextPage = pageNumber < totalPages,
            Items = dtos
        };

        _logger.LogInformation(
            "GetAllVenues completed. TotalCount={TotalCount}, PageNumber={PageNumber}, PageSize={PageSize}, ItemsReturned={ItemsReturned}",
            totalCount,
            pageNumber,
            pageSize,
            dtos.Count);

        return new Success<GetAllVenuesResponse>(response);
    }
}
