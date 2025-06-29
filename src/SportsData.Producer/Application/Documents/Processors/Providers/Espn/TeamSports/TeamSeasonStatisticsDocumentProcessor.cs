using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonStatistics)]
    public class TeamSeasonStatisticsDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<TeamSeasonStatisticsDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;

        public TeamSeasonStatisticsDocumentProcessor(
            ILogger<TeamSeasonStatisticsDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                _logger.LogInformation("Processing TeamSeasonStatistics for FranchiseSeason {ParentId}", command.ParentId);

                if (!Guid.TryParse(command.ParentId, out var franchiseSeasonId))
                {
                    _logger.LogError("Invalid ParentId: {ParentId}", command.ParentId);
                    return;
                }

                var franchiseSeason = await _dataContext.FranchiseSeasons
                    .Include(f => f.Statistics)
                    .ThenInclude(c => c.Stats)
                    .FirstOrDefaultAsync(f => f.Id == franchiseSeasonId);

                if (franchiseSeason == null)
                {
                    _logger.LogWarning("FranchiseSeason not found: {FranchiseSeasonId}", franchiseSeasonId);
                    return;
                }

                var dto = command.Document.FromJson<EspnTeamSeasonStatisticsDto>();
                if (dto?.Splits?.Categories == null || !dto.Splits.Categories.Any())
                {
                    _logger.LogWarning("No categories found in document for FranchiseSeason {Id}", franchiseSeasonId);
                    return;
                }

                // ✅ Delta Check
                if (franchiseSeason.Statistics.CategoriesMatch(dto.Splits.Categories))
                {
                    _logger.LogInformation("No changes detected for FranchiseSeason {ParentId}. Skipping insert.", command.ParentId);
                    return;
                }

                // ✅ Append new snapshot
                foreach (var dtoCategory in dto.Splits.Categories)
                {
                    var newCategory = dtoCategory.AsEntity(franchiseSeason.Id);
                    await _dataContext.FranchiseSeasonStatistics.AddAsync(newCategory);
                }

                await _dataContext.SaveChangesAsync();

                _logger.LogInformation("Inserted new TeamSeasonStatistics snapshot for FranchiseSeason {ParentId}", command.ParentId);
            }
        }

    }
}
