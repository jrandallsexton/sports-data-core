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

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.CoachSeasonRecord)]
public class CoachSeasonRecordDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public CoachSeasonRecordDocumentProcessor(
        ILogger<CoachSeasonRecordDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnCoachSeasonRecordDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnCoachSeasonRecordDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnCoachSeasonRecordDto Ref is null or empty. {@Command}", command);
            return;
        }

        if (command.ParentId is null)
        {
            _logger.LogError("CoachSeasonRecord requires a valid ParentId for CoachSeason. {@Command}", command);
            return;
        }

        if (!Guid.TryParse(command.ParentId.ToString(), out var coachSeasonId))
        {
            _logger.LogError("CoachSeasonRecord ParentId is not a valid GUID. {@Command}", command);
            return;
        }

        var coachSeason = await _dataContext.CoachSeasons
            .Include(x => x.Records)
                .ThenInclude(r => r.Stats)
            .Include(x => x.Records)
                .ThenInclude(r => r.ExternalIds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == coachSeasonId);

        if (coachSeason is null)
        {
            _logger.LogError("Parent CoachSeason not found with ID {CoachSeasonId}. {@Command}", command.ParentId, command);
            return;
        }

        var newRecord = CoachSeasonRecordExtensions.AsEntity(dto, coachSeason.Id, _externalRefIdentityGenerator, command.CorrelationId);

        // Replace any existing CoachSeasonRecord with same identity (same SourceUrlHash)
        var existing = coachSeason.Records.FirstOrDefault(r =>
            r.ExternalIds.Any(e => e.SourceUrlHash == newRecord.ExternalIds.First().SourceUrlHash));

        if (existing is not null)
        {
            _dataContext.CoachSeasonRecordStats.RemoveRange(existing.Stats);
            _dataContext.CoachSeasonRecords.Remove(existing);
        }

        await _dataContext.CoachSeasonRecords.AddAsync(newRecord);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CoachSeasonRecord for CoachSeason {CoachSeasonId}", coachSeason.Id);
    }
}
