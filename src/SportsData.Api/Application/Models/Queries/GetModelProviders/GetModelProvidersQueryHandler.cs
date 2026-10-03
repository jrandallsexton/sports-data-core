using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Models.Queries.GetModelProviders;

public class ModelProviderDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public ModelProviderKind Kind { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int ModelCount { get; set; }
}

public interface IGetModelProvidersQueryHandler
{
    Task<Result<List<ModelProviderDto>>> ExecuteAsync(CancellationToken cancellationToken);
}

public class GetModelProvidersQueryHandler : IGetModelProvidersQueryHandler
{
    private readonly AppDataContext _dataContext;

    public GetModelProvidersQueryHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<List<ModelProviderDto>>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var providers = await _dataContext.ModelProviders
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ModelProviderDto
            {
                Id = p.Id,
                Name = p.Name,
                Kind = p.Kind,
                Description = p.Description,
                IsActive = p.IsActive,
                ModelCount = p.Models.Count
            })
            .ToListAsync(cancellationToken);

        return new Success<List<ModelProviderDto>>(providers);
    }
}
