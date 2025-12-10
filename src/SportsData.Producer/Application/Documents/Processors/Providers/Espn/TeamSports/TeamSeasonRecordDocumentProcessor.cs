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

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonRecord)]
public class TeamSeasonRecordDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly TDataContext _dataContext;
    private readonly ILogger<TeamSeasonRecordDocumentProcessor<TDataContext>> _logger;
    private readonly IEventBus _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalIdentityProvider;

    public TeamSeasonRecordDocumentProcessor(
        TDataContext dataContext,
        ILogger<TeamSeasonRecordDocumentProcessor<TDataContext>> logger,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalIdentityProvider)        
    {
        _dataContext = dataContext;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
        _externalIdentityProvider = externalIdentityProvider;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId
        }))
        {
            _logger.LogInformation("Processing TeamSeasonRecordDocument for FranchiseSeason {ParentId}", command.ParentId);
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
            command.CorrelationId,
            CausationId.Producer.TeamSeasonRecordDocumentProcessor));

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Successfully processed TeamSeasonRecord '{RecordName}' for FranchiseSeason {Id}", dto.Name, franchiseSeasonId);
    }
}
