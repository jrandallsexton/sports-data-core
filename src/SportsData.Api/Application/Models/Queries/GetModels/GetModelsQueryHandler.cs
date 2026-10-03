using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Models.Queries.GetModels;

public class ModelDto
{
    public Guid Id { get; set; }
    public Guid ModelProviderId { get; set; }
    public string ProviderName { get; set; } = default!;
    public ModelProviderKind ProviderKind { get; set; }
    public string Name { get; set; } = default!;
    public string ApiModelId { get; set; } = default!;
    public ModelGateway Gateway { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public DateTime? KnowledgeCutoffUtc { get; set; }
    public string? CutoffEvidence { get; set; }
    public DateTime? CutoffVerifiedUtc { get; set; }
    public decimal? InputCostPerMTok { get; set; }
    public decimal? OutputCostPerMTok { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public interface IGetModelsQueryHandler
{
    Task<Result<List<ModelDto>>> ExecuteAsync(CancellationToken cancellationToken);
}

public class GetModelsQueryHandler : IGetModelsQueryHandler
{
    private readonly AppDataContext _dataContext;

    public GetModelsQueryHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<List<ModelDto>>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var models = await _dataContext.Models
            .AsNoTracking()
            .OrderByDescending(m => m.IsDefault)
            .ThenBy(m => m.ModelProvider!.Name)
            .ThenBy(m => m.Name)
            .Select(m => new ModelDto
            {
                Id = m.Id,
                ModelProviderId = m.ModelProviderId,
                ProviderName = m.ModelProvider!.Name,
                ProviderKind = m.ModelProvider.Kind,
                Name = m.Name,
                ApiModelId = m.ApiModelId,
                Gateway = m.Gateway,
                ReleaseDate = m.ReleaseDate,
                KnowledgeCutoffUtc = m.KnowledgeCutoffUtc,
                CutoffEvidence = m.CutoffEvidence,
                CutoffVerifiedUtc = m.CutoffVerifiedUtc,
                InputCostPerMTok = m.InputCostPerMTok,
                OutputCostPerMTok = m.OutputCostPerMTok,
                IsActive = m.IsActive,
                IsDefault = m.IsDefault,
                CreatedUtc = m.CreatedUtc
            })
            .ToListAsync(cancellationToken);

        return new Success<List<ModelDto>>(models);
    }
}
