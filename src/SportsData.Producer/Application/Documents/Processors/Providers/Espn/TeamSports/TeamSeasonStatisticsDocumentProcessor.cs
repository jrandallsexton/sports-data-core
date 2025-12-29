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

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonStatistics)]
public class TeamSeasonStatisticsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public TeamSeasonStatisticsDocumentProcessor(
        ILogger<TeamSeasonStatisticsDocumentProcessor<TDataContext>> logger,
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
            _logger.LogInformation("Processing TeamSeasonStatistics for FranchiseSeason {ParentId}", command.ParentId);
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
        if (!Guid.TryParse(command.ParentId, out var franchiseSeasonId))
        {
            _logger.LogError("Invalid ParentId: {ParentId}", command.ParentId);
            return;
        }

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .Include(f => f.Statistics)
            .ThenInclude(c => c.Stats)
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.Id == franchiseSeasonId);

        if (franchiseSeason == null)
        {
            _logger.LogWarning("FranchiseSeason not found: {FranchiseSeasonId}", franchiseSeasonId);
            return;
        }

        var dto = command.Document.FromJson<EspnTeamSeasonStatisticsDto>();

        if (dto?.Splits?.Categories == null || dto.Splits.Categories.Count == 0)
        {
            _logger.LogWarning("No categories found in document for FranchiseSeason {Id}", franchiseSeasonId);
            return;
        }

        // Extract games played from incoming DTO for staleness check
        var incomingGamesPlayed = ExtractGamesPlayed(dto);

        // Staleness check: Only update if incoming snapshot is newer (more games played)
        if (franchiseSeason.Statistics?.Any() == true)
        {
            var existingGamesPlayed = ExtractGamesPlayedFromExisting(franchiseSeason.Statistics);

            if (existingGamesPlayed.HasValue && incomingGamesPlayed.HasValue)
            {
                if (incomingGamesPlayed.Value < existingGamesPlayed.Value)
                {
                    _logger.LogWarning(
                        "Skipping stale statistics update. FranchiseSeasonId={Id}, " +
                        "ExistingGamesPlayed={ExistingGames}, IncomingGamesPlayed={IncomingGames}",
                        franchiseSeasonId,
                        existingGamesPlayed.Value,
                        incomingGamesPlayed.Value);
                    return; // Skip update - incoming data is older
                }

                if (incomingGamesPlayed.Value == existingGamesPlayed.Value)
                {
                    _logger.LogDebug(
                        "Statistics snapshot has same games played. FranchiseSeasonId={Id}, GamesPlayed={Games}. " +
                        "Updating anyway (may include corrections).",
                        franchiseSeasonId,
                        incomingGamesPlayed.Value);
                }
            }
        }

        // Remove existing statistics (cascade delete configured in entity)
        if (franchiseSeason.Statistics?.Any() == true)
        {
            _dataContext.FranchiseSeasonStatistics.RemoveRange(franchiseSeason.Statistics);
            _logger.LogInformation(
                "Removed existing TeamSeasonStatistics for FranchiseSeason {Id}. GamesPlayed: {ExistingGames} → {IncomingGames}",
                franchiseSeasonId,
                ExtractGamesPlayedFromExisting(franchiseSeason.Statistics) ?? 0,
                incomingGamesPlayed ?? 0);
        }

        // Insert new categories
        foreach (var dtoCategory in dto.Splits.Categories)
        {
            var newCategory = dtoCategory.AsEntity(franchiseSeasonId);
            await _dataContext.FranchiseSeasonStatistics.AddAsync(newCategory);
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Inserted new TeamSeasonStatistics snapshot for FranchiseSeason {Id}. GamesPlayed={GamesPlayed}, Categories={CategoryCount}",
            franchiseSeasonId,
            incomingGamesPlayed ?? 0,
            dto.Splits.Categories.Count);
    }

    /// <summary>
    /// Extracts the "teamGamesPlayed" stat value from the incoming DTO.
    /// This stat appears in multiple categories (typically "general"), so we find the first occurrence.
    /// </summary>
    private static int? ExtractGamesPlayed(EspnTeamSeasonStatisticsDto dto)
    {
        foreach (var category in dto.Splits.Categories)
        {
            var gamesPlayedStat = category.Stats?.FirstOrDefault(s =>
                s.Name.Equals("teamGamesPlayed", StringComparison.OrdinalIgnoreCase));

            if (gamesPlayedStat != null)
            {
                return (int)gamesPlayedStat.Value;
            }
        }

        return null; // No games played stat found
    }

    /// <summary>
    /// Extracts the "teamGamesPlayed" stat value from existing statistics in the database.
    /// </summary>
    private static int? ExtractGamesPlayedFromExisting(ICollection<FranchiseSeasonStatisticCategory> existingCategories)
    {
        foreach (var category in existingCategories)
        {
            var gamesPlayedStat = category.Stats?.FirstOrDefault(s =>
                s.Name.Equals("teamGamesPlayed", StringComparison.OrdinalIgnoreCase));

            if (gamesPlayedStat != null)
            {
                return (int)gamesPlayedStat.Value;
            }
        }

        return null; // No games played stat found
    }
}