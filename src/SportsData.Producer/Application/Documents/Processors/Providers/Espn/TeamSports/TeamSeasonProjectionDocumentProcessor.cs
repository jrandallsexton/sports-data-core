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
using SportsData.Core.Infrastructure.DataSources.Espn;

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

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var franchiseSeasonId = TryGetOrDeriveParentId(
            command, 
            EspnUriMapper.TeamSeasonProjectionRefToTeamSeasonRef);

        if (franchiseSeasonId == null)
        {
            _logger.LogError("Unable to determine FranchiseSeasonId from ParentId or URI");
            return;
        }

        var franchiseSeasonIdValue = franchiseSeasonId.Value;

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == franchiseSeasonIdValue);

        if (franchiseSeason is null)
        {
            _logger.LogWarning("FranchiseSeason not found: {FranchiseSeasonId}", franchiseSeasonIdValue);
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
            .FirstOrDefaultAsync(x => x.FranchiseSeasonId == franchiseSeasonIdValue);

        if (existing != null)
        {
            // Update existing entity
            existing.ChanceToWinDivision = dto.ChanceToWinDivision;
            existing.ChanceToWinConference = dto.ChanceToWinConference;
            existing.ProjectedWins = dto.ProjectedWins;
            existing.ProjectedLosses = dto.ProjectedLosses;
            existing.ModifiedBy = command.CorrelationId;
            existing.ModifiedUtc = DateTime.UtcNow;
            _logger.LogInformation("Updated existing FranchiseSeasonProjection for FranchiseSeason {FranchiseSeasonId}", franchiseSeasonIdValue);
        }
        else
        {
            // Create new entity
            var projection = dto.AsEntity(
                franchiseSeasonIdValue,
                franchiseSeason.FranchiseId,
                franchiseSeason.SeasonYear,
                command.CorrelationId);
            await _dataContext.FranchiseSeasonProjections.AddAsync(projection);
            _logger.LogInformation("Created new FranchiseSeasonProjection for FranchiseSeason {FranchiseSeasonId}", franchiseSeasonIdValue);
        }

        await _dataContext.SaveChangesAsync();
    }
}