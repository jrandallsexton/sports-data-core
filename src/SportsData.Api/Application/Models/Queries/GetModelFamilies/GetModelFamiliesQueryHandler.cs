using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Models.Queries.GetModelFamilies;

public class ModelFamilyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public int ModelCount { get; set; }

    /// <summary>The version currently holding the family's lab seat; null = no active version.</summary>
    public string? ActiveModelName { get; set; }
}

public interface IGetModelFamiliesQueryHandler
{
    Task<Result<List<ModelFamilyDto>>> ExecuteAsync(CancellationToken cancellationToken);
}

public class GetModelFamiliesQueryHandler : IGetModelFamiliesQueryHandler
{
    private readonly AppDataContext _dataContext;

    public GetModelFamiliesQueryHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<List<ModelFamilyDto>>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var families = await _dataContext.ModelFamilies
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .Select(f => new ModelFamilyDto
            {
                Id = f.Id,
                Name = f.Name,
                Description = f.Description,
                ModelCount = f.Models.Count,
                ActiveModelName = f.Models
                    .Where(m => m.IsActive && m.Gateway == Model.LabSeatGateway)
                    .Select(m => m.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return new Success<List<ModelFamilyDto>>(families);
    }
}
