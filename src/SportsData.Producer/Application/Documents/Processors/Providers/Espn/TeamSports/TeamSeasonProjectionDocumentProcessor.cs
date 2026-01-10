using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonProjection)]
public class TeamSeasonProjectionDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public TeamSeasonProjectionDocumentProcessor(
        ILogger<TeamSeasonProjectionDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId
        }))
        {
            _logger.LogInformation("Processing TeamSeasonProjectionDocument for FranchiseSeason {ParentId}", command.ParentId);
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
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == franchiseSeasonId);

        if (franchiseSeason is null)
        {
            _logger.LogWarning("FranchiseSeason not found: {FranchiseSeasonId}", franchiseSeasonId);
            return;
        }

        var dto = command.Document.FromJson<EspnTeamSeasonProjectionDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnTeamSeasonProjectionDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnTeamSeasonProjectionDto Ref is null or empty. {@Command}", command);
            return;
        }

        // Check if a projection already exists for this FranchiseSeason
        var existing = await _dataContext.FranchiseSeasonProjections
            .FirstOrDefaultAsync(x => x.FranchiseSeasonId == franchiseSeasonId);

        if (existing != null)
        {
            // Update existing entity
            existing.ChanceToWinDivision = dto.ChanceToWinDivision;
            existing.ChanceToWinConference = dto.ChanceToWinConference;
            existing.ProjectedWins = dto.ProjectedWins;
            existing.ProjectedLosses = dto.ProjectedLosses;
            existing.ModifiedBy = command.CorrelationId;
            existing.ModifiedUtc = DateTime.UtcNow;
            _logger.LogInformation("Updated existing FranchiseSeasonProjection for FranchiseSeason {FranchiseSeasonId}", franchiseSeasonId);
        }
        else
        {
            // Create new entity
            var projection = dto.AsEntity(
                franchiseSeasonId,
                franchiseSeason.FranchiseId,
                franchiseSeason.SeasonYear,
                command.CorrelationId);
            await _dataContext.FranchiseSeasonProjections.AddAsync(projection);
            _logger.LogInformation("Created new FranchiseSeasonProjection for FranchiseSeason {FranchiseSeasonId}", franchiseSeasonId);
        }

        await _dataContext.SaveChangesAsync();
    }
}