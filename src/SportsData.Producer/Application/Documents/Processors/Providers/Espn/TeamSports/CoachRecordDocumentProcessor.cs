using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.CoachRecord)]
public class CoachRecordDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<CoachRecordDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public CoachRecordDocumentProcessor(
        ILogger<CoachRecordDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IPublishEndpoint publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
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
            .Include(x => x.ExternalIds).Include(coach => coach.Records)
            .ThenInclude(coachRecord => coachRecord.ExternalIds)
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
