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

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.CoachRecord)]
public class CoachRecordDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public CoachRecordDocumentProcessor(
        ILogger<CoachRecordDocumentProcessor<TDataContext>> logger,
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
            _logger.LogInformation("Began processing CoachRecord with {@Command}", command);
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
        var dto = command.Document.FromJson<EspnCoachRecordDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnCoachRecordDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnCoachRecordDto Ref is null or empty. {@Command}", command);
            return;
        }

        if (command.ParentId is null)
        {
            _logger.LogError("CoachRecord requires a valid ParentId for Coach. {@Command}", command);
            return;
        }

        if (!Guid.TryParse(command.ParentId.ToString(), out var coachId))
        {
            _logger.LogError("CoachRecord ParentId is not a valid GUID. {@Command}", command);
            return;
        }

        var coach = await _dataContext.Coaches
            .Include(x => x.Records)
                .ThenInclude(r => r.Stats)
            .Include(x => x.Records)
                .ThenInclude(r => r.ExternalIds)
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.Id == coachId);

        if (coach is null)
        {
            _logger.LogError("Parent Coach not found with ID {CoachId}. {@Command}", command.ParentId, command);
            return;
        }

        var newRecord = dto.AsEntity(coach.Id, _externalRefIdentityGenerator, command.CorrelationId);

        // Replace any existing CoachRecord with same identity (same SourceUrlHash)
        var existing = coach.Records.FirstOrDefault(r =>
            r.ExternalIds.Any(e => e.SourceUrlHash == newRecord.ExternalIds.First().SourceUrlHash));

        if (existing is not null)
        {
            _dataContext.CoachRecordStats.RemoveRange(existing.Stats);
            _dataContext.CoachRecords.Remove(existing);
        }

        await _dataContext.CoachRecords.AddAsync(newRecord);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CoachRecord for Coach {CoachId}", coach.Id);
    }
}
