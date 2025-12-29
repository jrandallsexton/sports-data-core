using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonRecordAts)]
public class TeamSeasonRecordAtsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public TeamSeasonRecordAtsDocumentProcessor(
        ILogger<TeamSeasonRecordAtsDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator)
    {
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId
        }))
        {
            _logger.LogInformation("Processing TeamSeasonRecordAtsDocument for FranchiseSeason {ParentId}", command.ParentId);
            try
            {
                await ProcessInternal(command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnTeamSeasonRecordAtsDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnTeamSeasonRecordAtsDto. {@Command}", command);
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var franchiseSeasonId))
        {
            _logger.LogError("Invalid ParentId: {ParentId}", command.ParentId);
            return;
        }

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .Include(fs => fs.RecordsAts)
            .FirstOrDefaultAsync(s => s.Id == franchiseSeasonId);

        if (franchiseSeason is null)
        {
            _logger.LogWarning("FranchiseSeason not found: {FranchiseSeasonId}", franchiseSeasonId);
            return;
        }

        foreach (var item in dto.Items)
        {
            var category = await _dataContext.FranchiseSeasonRecordAtsCategories
                .FirstOrDefaultAsync(c => c.Name == item.Type.Name);

            if (category == null)
            {
                _logger.LogError(
                    "AtsCategory not found for Name '{CategoryName}'. Creating new one with fallback Id.",
                    item.Type.Name
                );

                category = new FranchiseSeasonRecordAtsCategory
                {
                    Id = await GetNextCategoryIdAsync(),
                    Name = item.Type.Name,
                    Description = item.Type.Description,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = command.CorrelationId
                };

                await _dataContext.FranchiseSeasonRecordAtsCategories.AddAsync(category);
                await _dataContext.SaveChangesAsync();
            }

            // Prevent duplicate records for same category
            var alreadyExists = franchiseSeason.RecordsAts.Any(r => r.CategoryId == category.Id);
            if (alreadyExists)
            {
                _logger.LogInformation("Record for CategoryId {CategoryId} already exists. Skipping.", category.Id);
                continue;
            }

            var entity = item.AsEntity(franchiseSeasonId, category.Id, command.CorrelationId);

            await _dataContext.FranchiseSeasonRecordsAts.AddAsync(entity);
        }

        await _dataContext.SaveChangesAsync();
    }

    private async Task<int> GetNextCategoryIdAsync()
    {
        var maxId = await _dataContext.FranchiseSeasonRecordAtsCategories
            .MaxAsync(c => (int?)c.Id) ?? 0;

        return maxId + 1;
    }


}