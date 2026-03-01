using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonTypeWeek)]
public class SeasonTypeWeekDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : BaseDataContext
{

    public SeasonTypeWeekDocumentProcessor(
        ILogger<SeasonTypeWeekDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        IEventBus publishEndpoint)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        if (command.Season is null) 
        {
            _logger.LogError("Command does not contain a valid Season. {@Command}", command);
            return;
        }

        var externalProviderDto = command.Document.FromJson<EspnFootballSeasonTypeWeekDto>();

        if (externalProviderDto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnFootballSeasonTypeWeekDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(externalProviderDto.Ref?.ToString()))
        {
            _logger.LogError("EspnFootballSeasonTypeWeekDto Ref is null or empty. {@Command}", command);
            return;
        }

        var seasonPhaseIdNullable = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.SeasonTypeWeekToSeasonType);

        if (seasonPhaseIdNullable == null)
        {
            _logger.LogError("Unable to determine SeasonPhaseId from ParentId or URI");
            return;
        }

        var seasonPhaseId = seasonPhaseIdNullable.Value;

        var seasonPhase = await _dataContext.SeasonPhases
            .Include(x => x.Weeks)
            .ThenInclude(w => w.ExternalIds)
            .AsSplitQuery()
            .Where(x => x.Id == seasonPhaseId)
            .FirstOrDefaultAsync();

        if (seasonPhase == null)
        {
            var seasonPhaseRef = EspnUriMapper.SeasonTypeWeekToSeasonType(externalProviderDto.Ref);

            await PublishDependencyRequest<string?>(
                command,
                new EspnLinkDto { Ref = seasonPhaseRef },
                parentId: null,
                DocumentType.SeasonType);

            throw new ExternalDocumentNotSourcedException($"SeasonPhase {seasonPhaseRef} not found. Will retry.");
        }

        var dtoIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Ref);

        var seasonWeek = seasonPhase.Weeks
            .FirstOrDefault(w => w.ExternalIds.Any(id => id.SourceUrlHash == dtoIdentity.UrlHash &&
                                                         id.Provider == command.SourceDataProvider));
        if (seasonWeek is null)
        {
            await ProcessNewEntity(externalProviderDto, seasonPhase, command, dtoIdentity);
        }
        else
        {
            await ProcessExistingEntity();
        }

    }

    private async Task ProcessNewEntity(
        EspnFootballSeasonTypeWeekDto dto,
        SeasonPhase seasonPhase,
        ProcessDocumentCommand command,
        ExternalRefIdentity dtoIdentity)
    {
        var seasonWeek = dto.AsEntity(
            seasonPhase.SeasonId,
            seasonPhase.Id,
            _externalRefIdentityGenerator,
            command.CorrelationId);

        // Note: Rankings are available here, but we skip processing them for now

        await _dataContext.SeasonWeeks.AddAsync(seasonWeek);

        try
        {
            await _dataContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // Another pod won the race and already inserted this SeasonWeek.
            // Detach the tracked entity so the DbContext is left clean, then return —
            // the row is already present so there is nothing more to do.
            _logger.LogWarning(
                "Duplicate key on SeasonWeek insert — another process already created it. " +
                "Id={Id}, CorrelationId={CorrelationId}",
                seasonWeek.Id, command.CorrelationId);

            _dataContext.Entry(seasonWeek).State = EntityState.Detached;

            foreach (var externalId in seasonWeek.ExternalIds)
                _dataContext.Entry(externalId).State = EntityState.Detached;
        }
    }

    private async Task ProcessExistingEntity()
    {
        _logger.LogError("Update detected. Not implemented");
        await Task.CompletedTask;
    }
}