using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonRecord)]
public class TeamSeasonRecordDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public TeamSeasonRecordDocumentProcessor(
        ILogger<TeamSeasonRecordDocumentProcessor<TDataContext>> logger,
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

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == franchiseSeasonId);

        if (franchiseSeason is null)
        {
            _logger.LogError("FranchiseSeason not found: {FranchiseSeasonId}", franchiseSeasonId);
            return;
        }

        var dto = command.Document.FromJson<EspnTeamSeasonRecordDto>();

        if (dto is null)
        {
            _logger.LogError("DTO is null for TeamSeasonRecord processing. ParentId: {ParentId}", command.ParentId);
            return;
        }

        // Find existing record by FranchiseSeasonId, Name, and Type (natural key)
        var existing = await _dataContext.FranchiseSeasonRecords
            .FirstOrDefaultAsync(r => r.FranchiseSeasonId == franchiseSeasonId 
                                   && r.Name == dto.Name 
                                   && r.Type == dto.Type);

        if (existing is not null)
        {
            // delete then re-add to simplify processing logic
            _dataContext.FranchiseSeasonRecords.Remove(existing);
            await _dataContext.SaveChangesAsync();
        }

        var entity = dto.AsEntity(
            franchiseSeasonId,
            franchiseSeason.FranchiseId,
            franchiseSeason.SeasonYear,
            Guid.Empty);

        await _dataContext.FranchiseSeasonRecords.AddAsync(entity);

        var canonical = entity.AsCanonical();
        await _publishEndpoint.Publish(new FranchiseSeasonRecordCreated(
            canonical,
            null,
            command.Sport,
            command.Season,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonRecordDocumentProcessor));

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Successfully processed TeamSeasonRecord '{RecordName}' for FranchiseSeason {Id}", dto.Name, franchiseSeasonId);
    }
}
