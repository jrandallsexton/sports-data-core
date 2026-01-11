using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using FluentValidation.Results;

namespace SportsData.Producer.Application.Franchises.Queries.GetFranchiseById;

public interface IGetFranchiseByIdQueryHandler
{
    Task<Result<FranchiseDto>> ExecuteAsync(GetFranchiseByIdQuery query, CancellationToken cancellationToken = default);
}

public class GetFranchiseByIdQueryHandler : IGetFranchiseByIdQueryHandler
{
    private readonly TeamSportDataContext _dataContext;
    private readonly ILogger<GetFranchiseByIdQueryHandler> _logger;

    public GetFranchiseByIdQueryHandler(
        TeamSportDataContext dataContext,
        ILogger<GetFranchiseByIdQueryHandler> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task<Result<FranchiseDto>> ExecuteAsync(
        GetFranchiseByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetFranchise query: idOrSlug={IdOrSlug}", query.Id);

        // Try to parse as GUID first, otherwise treat as slug
        var isGuid = Guid.TryParse(query.Id, out var franchiseId);

        var franchise = await _dataContext.Franchises
            .AsNoTracking()
            .Where(f => isGuid ? f.Id == franchiseId : f.Slug == query.Id)
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
                Slug = f.Slug,
                CreatedUtc = f.CreatedUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (franchise == null)
        {
            _logger.LogWarning("Franchise not found: {IdOrSlug}", query.Id);
            return new Failure<FranchiseDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("Id", $"Franchise with id or slug '{query.Id}' not found")]);
        }

        return new Success<FranchiseDto>(franchise);
    }
}
