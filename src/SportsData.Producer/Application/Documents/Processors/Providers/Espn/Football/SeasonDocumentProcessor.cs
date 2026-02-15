using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Season)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Seasons)]
public class SeasonDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : BaseDataContext
{
    public SeasonDocumentProcessor(
        ILogger<SeasonDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        IEventBus publishEndpoint)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        // Step 1: Deserialize
        var dto = command.Document.FromJson<EspnFootballSeasonDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnFootballSeasonDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnFootballSeasonDto Ref is null or empty. {@Command}", command);
            return;
        }

        // Step 2: Map DTO -> Canonical Entity
        var mappedSeason = dto.AsEntity(_externalRefIdentityGenerator, command.CorrelationId);

        _logger.LogInformation("Mapped season: {@mappedSeason}", mappedSeason);

        // Step 3: Load existing from DB
        var existingSeason = await _dataContext.Seasons
            .Include(s => s.Phases)
            .Include(s => s.ExternalIds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == mappedSeason.Id);

        if (existingSeason != null)
        {
            await ProcessUpdateAsync(existingSeason, mappedSeason);
        }
        else
        {
            await ProcessNewEntity(command, dto);
        }

        // Step 4: Save changes
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Finished processing season {SeasonId}", mappedSeason.Id);
    }

    private async Task ProcessNewEntity(ProcessDocumentCommand command, EspnFootballSeasonDto dto)
    {
        var season = dto.AsEntity(_externalRefIdentityGenerator, command.CorrelationId);

        var seasonType = dto.Type;
        var seasonPhase = seasonType.AsEntity(season.Id, _externalRefIdentityGenerator, command.CorrelationId);

        await _dataContext.Seasons.AddAsync(season);
        await _dataContext.SeasonPhases.AddAsync(seasonPhase);
        await _dataContext.SaveChangesAsync();

        //await _dataContext.Seasons
        //    .Where(s => s.Id == season.Id && s.ActivePhaseId == null)
        //    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActivePhaseId, _ => seasonPhase.Id));

        var existingSeason = await _dataContext.Seasons
            .FirstOrDefaultAsync(s => s.Id == season.Id && s.ActivePhaseId == null);

        if (existingSeason is not null)
        {
            existingSeason.ActivePhaseId = seasonPhase.Id;
            await _dataContext.SaveChangesAsync();
        }

        _logger.LogInformation("Linked ActivePhaseId for Season {SeasonId} -> Phase {PhaseId}",
            season.Id, seasonPhase.Id);

        var publishEvents = false;

        if (dto.Types?.Ref is not null)
        {
            await PublishChildDocumentRequest(
                command,
                dto.Types,
                season.Id,
                DocumentType.SeasonType);
            _logger.LogInformation("Found {Count} season phases", dto.Types.Count);
            publishEvents = true;
        }

        // Rankings are here, but cannot be processed until we have FranchiseSeason entities created

        // Had to remove this for now as it creates a circular dependency between SeasonDocumentProcessor and AthleteSeasonDocumentProcessor
        //if (dto.Athletes?.Ref is not null)
        //{
        //    await _publishEndpoint.Publish(new DocumentRequested(
        //        Id: Guid.NewGuid().ToString(),
        //        ParentId: null,  // we do not have it; AthleteSeasonDocumentProcessor will need to find the parent Athlete
        //        Uri: dto.Athletes.Ref,
        //        Sport: command.Sport,
        //        SeasonYear: dto.Year,
        //        DocumentType: DocumentType.AthleteSeason,
        //        SourceDataProvider: SourceDataProvider.Espn,
        //        CorrelationId: command.CorrelationId,
        //        CausationId: CausationId.Producer.SeasonDocumentProcessor
        //    ));
        //}

        if (dto.Futures?.Ref is not null)
        {
            await PublishChildDocumentRequest(
                command,
                dto.Futures,
                season.Id,
                DocumentType.SeasonFuture);
            publishEvents = true;
        }

        // Leaders are here, but cannot be processed until we have AthleteSeason entities created

        if (publishEvents)
        {
            await _dataContext.SaveChangesAsync();
        }

        _logger.LogInformation("Created new Season entity: {SeasonId}", season.Id);
    }

    private async Task ProcessUpdateAsync(Season existingSeason, Season mappedSeason)
    {
        _logger.LogWarning("Season update detected. Not implemented");
        await Task.CompletedTask;
    }
}