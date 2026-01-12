using Microsoft.EntityFrameworkCore;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Franchises.Queries.GetAllFranchises;

public interface IGetAllFranchisesQueryHandler
{
    Task<Result<GetAllFranchisesResponse>> ExecuteAsync(
        GetAllFranchisesQuery query,
        CancellationToken cancellationToken = default);
}

public class GetAllFranchisesQueryHandler : IGetAllFranchisesQueryHandler
{
    private readonly ILogger<GetAllFranchisesQueryHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IGenerateResourceRefs _refGenerator;

    public GetAllFranchisesQueryHandler(
        ILogger<GetAllFranchisesQueryHandler> logger,
        TeamSportDataContext dataContext,
        IGenerateResourceRefs refGenerator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _refGenerator = refGenerator;
    }

    public async Task<Result<GetAllFranchisesResponse>> ExecuteAsync(
        GetAllFranchisesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetAllFranchises started. PageNumber={PageNumber}, PageSize={PageSize}",
            query.PageNumber,
            query.PageSize);

        // Validate and clamp pagination parameters
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, GetAllFranchisesQuery.MinPageSize, GetAllFranchisesQuery.MaxPageSize);

        // Get total count for pagination metadata
        var totalCount = await _dataContext.Franchises
            .AsNoTracking()
            .CountAsync(cancellationToken);

        // Calculate pagination values
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = (pageNumber - 1) * pageSize;

        // Get paginated data
        var dtos = await _dataContext.Franchises
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Skip(skip)
            .Take(pageSize)
            .Select(f => new FranchiseDto
            {
                Id = f.Id,
                Sport = f.Sport,
                Name = f.Name,
                Nickname = f.Nickname ?? string.Empty,
                Abbreviation = f.Abbreviation ?? string.Empty,
                DisplayName = f.DisplayName,
                DisplayNameShort = f.DisplayNameShort,
                ColorCodeHex = f.ColorCodeHex,
                ColorCodeAltHex = f.ColorCodeAltHex,
                Slug = f.Slug
            })
            .ToListAsync(cancellationToken);

        var response = new GetAllFranchisesResponse
        {
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasPreviousPage = pageNumber > 1,
            HasNextPage = pageNumber < totalPages,
            Items = dtos,
            Links = new Dictionary<string, Uri>
            {
                ["self"] = _refGenerator.ForFranchises(pageNumber, pageSize),
                ["first"] = _refGenerator.ForFranchises(1, pageSize),
                ["last"] = _refGenerator.ForFranchises(totalPages > 0 ? totalPages : 1, pageSize)
            }
        };

        // Add previous page link if not on first page
        if (pageNumber > 1)
        {
            response.Links["prev"] = _refGenerator.ForFranchises(pageNumber - 1, pageSize);
        }

        // Add next page link if not on last page
        if (pageNumber < totalPages)
        {
            response.Links["next"] = _refGenerator.ForFranchises(pageNumber + 1, pageSize);
        }

        _logger.LogInformation(
            "GetAllFranchises completed. TotalCount={TotalCount}, PageNumber={PageNumber}, PageSize={PageSize}, ItemsReturned={ItemsReturned}",
            totalCount,
            pageNumber,
            pageSize,
            dtos.Count);

        return new Success<GetAllFranchisesResponse>(response);
    }
}
