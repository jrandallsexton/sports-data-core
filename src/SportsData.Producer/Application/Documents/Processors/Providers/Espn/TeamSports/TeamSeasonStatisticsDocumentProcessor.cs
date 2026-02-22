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

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonStatistics)]
public class TeamSeasonStatisticsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public TeamSeasonStatisticsDocumentProcessor(
        ILogger<TeamSeasonStatisticsDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        if (!Guid.TryParse(command.ParentId, out var franchiseSeasonId))
        {
            _logger.LogError("Invalid ParentId: {ParentId}", command.ParentId);
            return;
        }

        const int maxRetries = 3;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            attempt++;

            try
            {
                await TryProcessStatistics(franchiseSeasonId, command, attempt);
                return; // Success - exit retry loop
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt >= maxRetries)
                {
                    _logger.LogError(ex,
                        "Concurrency conflict persisted after {MaxRetries} attempts. " +
                        "FranchiseSeasonId={FranchiseSeasonId}, CorrelationId={CorrelationId}",
                        maxRetries, franchiseSeasonId, command.CorrelationId);
                    throw;
                }

                // Exponential backoff: 100ms, 200ms, 400ms
                var delayMs = 100 * (int)Math.Pow(2, attempt - 1);

                _logger.LogWarning(ex,
                    "Concurrency conflict detected (attempt {Attempt}/{MaxRetries}). " +
                    "Another process updated FranchiseSeason concurrently. " +
                    "Retrying after {DelayMs}ms. FranchiseSeasonId={FranchiseSeasonId}, CorrelationId={CorrelationId}",
                    attempt, maxRetries, delayMs, franchiseSeasonId, command.CorrelationId);

                await Task.Delay(delayMs);
                // Loop will retry with fresh data
            }
        }
    }

    /// <summary>
    /// Attempts to process statistics with optimistic concurrency control.
    /// Throws DbUpdateConcurrencyException if another process modified FranchiseSeason concurrently.
    /// </summary>
    private async Task TryProcessStatistics(Guid franchiseSeasonId, ProcessDocumentCommand command, int attempt)
    {
        // Re-fetch fresh data on each attempt to get latest RowVersion
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
                        "Skipping stale statistics update (attempt {Attempt}). FranchiseSeasonId={Id}, " +
                        "ExistingGamesPlayed={ExistingGames}, IncomingGamesPlayed={IncomingGames}, CorrelationId={CorrelationId}",
                        attempt, franchiseSeasonId, existingGamesPlayed.Value, incomingGamesPlayed.Value, command.CorrelationId);
                    return; // Skip update - incoming data is older
                }

                if (incomingGamesPlayed.Value == existingGamesPlayed.Value)
                {
                    _logger.LogDebug(
                        "Statistics snapshot has same games played (attempt {Attempt}). FranchiseSeasonId={Id}, GamesPlayed={Games}. " +
                        "Updating anyway (may include corrections). CorrelationId={CorrelationId}",
                        attempt, franchiseSeasonId, incomingGamesPlayed.Value, command.CorrelationId);
                }
            }
        }

        // Remove existing statistics (cascade delete configured in entity)
        if (franchiseSeason.Statistics?.Any() == true)
        {
            _dataContext.FranchiseSeasonStatistics.RemoveRange(franchiseSeason.Statistics.ToList());
            _logger.LogInformation(
                "Removed existing TeamSeasonStatistics for FranchiseSeason {Id} (attempt {Attempt}). " +
                "GamesPlayed: {ExistingGames} → {IncomingGames}, CorrelationId={CorrelationId}",
                franchiseSeasonId, attempt,
                ExtractGamesPlayedFromExisting(franchiseSeason.Statistics) ?? 0,
                incomingGamesPlayed ?? 0,
                command.CorrelationId);
        }

        // Insert new categories
        foreach (var dtoCategory in dto.Splits.Categories)
        {
            var newCategory = dtoCategory.AsEntity(franchiseSeasonId);
            await _dataContext.FranchiseSeasonStatistics.AddAsync(newCategory);
        }

        // This will throw DbUpdateConcurrencyException if FranchiseSeason.RowVersion changed
        // since we loaded it (another process updated it concurrently)
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Inserted new TeamSeasonStatistics snapshot for FranchiseSeason {Id} (attempt {Attempt}). " +
            "GamesPlayed={GamesPlayed}, Categories={CategoryCount}, CorrelationId={CorrelationId}",
            franchiseSeasonId, attempt, incomingGamesPlayed ?? 0, dto.Splits.Categories.Count, command.CorrelationId);
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