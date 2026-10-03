using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Models.Queries.GetModels;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Models.Queries.GetModelById;

public interface IGetModelByIdQueryHandler
{
    Task<Result<ModelDto>> ExecuteAsync(Guid modelId, CancellationToken cancellationToken);
}

public class GetModelByIdQueryHandler : IGetModelByIdQueryHandler
{
    private readonly AppDataContext _dataContext;

    public GetModelByIdQueryHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<ModelDto>> ExecuteAsync(Guid modelId, CancellationToken cancellationToken)
    {
        var model = await _dataContext.Models
            .AsNoTracking()
            .Where(m => m.Id == modelId)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (model is null)
        {
            return new Failure<ModelDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(modelId), "Model not found")]);
        }

        return new Success<ModelDto>(model);
    }
}
