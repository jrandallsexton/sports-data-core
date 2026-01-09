using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionPlay)]
public class EventCompetitionPlayDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public EventCompetitionPlayDocumentProcessor(
        ILogger<EventCompetitionPlayDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator)
    {
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId,
                   ["DocumentType"] = command.DocumentType,
                   ["Season"] = command.Season ?? 0,
                   ["CompetitionId"] = command.ParentId ?? "Unknown"
               }))
        {
            _logger.LogInformation("EventCompetitionPlayDocumentProcessor started. Ref={Ref}, UrlHash={UrlHash}", 
                command.GetDocumentRef(),
                command.UrlHash);

            try
            {
                await ProcessInternal(command);
                
                _logger.LogInformation("EventCompetitionPlayDocumentProcessor completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventCompetitionPlayDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var externalDto = command.Document.FromJson<EspnEventCompetitionPlayDto>();
            
        if (externalDto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionPlayDto.");
            return;
        }

        if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionPlayDto Ref is null.");
            return;
        }

        if (!command.Season.HasValue)
        {
            _logger.LogError("Command missing SeasonYear.");
            return;
        }

        if (string.IsNullOrEmpty(command.ParentId))
        {
            _logger.LogError("Command missing ParentId for CompetitionId.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("CompetitionId could not be parsed. ParentId={ParentId}", command.ParentId);
            return;
        }

        Guid? competitionDriveId = null;
            
        if (command.PropertyBag.TryGetValue("CompetitionDriveId", out var value))
        {
            if (Guid.TryParse(value, out var driveId))
            {
                competitionDriveId = driveId;
            }
        }

        var competition = await _dataContext.Competitions
            .Include(c => c.Status)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionId);

        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionId);
            throw new InvalidOperationException($"Competition with ID {competitionId} does not exist.");
        }

        var startFranchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            externalDto.Start.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

        var endFranchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            externalDto.End.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

        var playIdentity = _externalRefIdentityGenerator.Generate(externalDto.Ref);

        var entity = await _dataContext.CompetitionPlays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.Id == playIdentity.CanonicalId);

        if (entity is null)
        {
            _logger.LogInformation("Processing new CompetitionPlay entity. Ref={Ref}", externalDto.Ref);
            await ProcessNewEntity(
                command,
                externalDto,
                competition,
                competitionDriveId,
                startFranchiseSeasonId,
                endFranchiseSeasonId);
        }
        else
        {
            _logger.LogInformation("Processing CompetitionPlay update. PlayId={PlayId}, Ref={Ref}", entity.Id, externalDto.Ref);
            await ProcessUpdate(
                command,
                externalDto,
                competitionDriveId,
                entity,
                startFranchiseSeasonId,
                endFranchiseSeasonId);
        }
    }

    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnEventCompetitionPlayDto externalDto,
        Competition competition,
        Guid? competitionDriveId,
        Guid? startFranchiseSeasonId,
        Guid? endFranchiseSeasonId)
    {
        _logger.LogInformation("Creating new CompetitionPlay. CompetitionId={CompId}, DriveId={DriveId}, PlayType={PlayType}", 
            competition.Id,
            competitionDriveId,
            externalDto.Type?.Text);

        var play = externalDto.AsEntity(
            _externalRefIdentityGenerator,
            command.CorrelationId,
            competition.Id,
            competitionDriveId,
            startFranchiseSeasonId,
            endFranchiseSeasonId);

        // if the competition is underway,
        // broadcast a CompetitionPlayCompleted event
        if (competition.Status is not null && !competition.Status.IsCompleted)
        {
            _logger.LogInformation("Competition in progress, publishing CompetitionPlayCompleted event. CompetitionId={CompId}, PlayId={PlayId}",
                competition.Id,
                play.Id);

            await _publishEndpoint.Publish(new CompetitionPlayCompleted(
                CompetitionPlayId: play.Id,
                CompetitionId: competition.Id,
                ContestId: competition.ContestId,
                PlayDescription: play.Text,
                Ref: null,
                Sport: command.Sport,
                SeasonYear: command.Season,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionPlayDocumentProcessor));
        }

        await _dataContext.CompetitionPlays.AddAsync(play);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CompetitionPlay. CompetitionId={CompId}, PlayId={PlayId}, Sequence={Sequence}", 
            competition.Id,
            play.Id,
            play.SequenceNumber);
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnEventCompetitionPlayDto externalDto,
        Guid? competitionDriveId,
        CompetitionPlay entity,
        Guid? startFranchiseSeasonId,
        Guid? endFranchiseSeasonId)
    {
        _logger.LogInformation("Updating CompetitionPlay. PlayId={PlayId}, DriveId={DriveId}", 
            entity.Id,
            competitionDriveId);

        entity.StartFranchiseSeasonId = startFranchiseSeasonId;
        entity.EndFranchiseSeasonId = endFranchiseSeasonId;
        entity.DriveId = competitionDriveId;
        
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CompetitionPlay update. PlayId={PlayId}", entity.Id);
    }
}