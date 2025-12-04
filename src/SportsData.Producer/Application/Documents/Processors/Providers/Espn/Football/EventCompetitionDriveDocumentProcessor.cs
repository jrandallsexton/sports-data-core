using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionDrive)]
public class EventCompetitionDriveDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : FootballDataContext
{
    private readonly ILogger<EventCompetitionDriveDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IEventBus _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public EventCompetitionDriveDocumentProcessor(
        ILogger<EventCompetitionDriveDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
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
            _logger.LogInformation("Began with {@command}", command);

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
        var externalDto = command.Document.FromJson<EspnEventCompetitionDriveDto>();

        if (externalDto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnEventCompetitionDriveDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionDriveDto Ref is null. {@Command}", command);
            return;
        }

        var competitionId = await GetCompetitionId(command);

        if (competitionId is null)
        {
            _logger.LogError("CompetitionId could not be determined");
            return;
        }

        var startFranchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            externalDto.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

        var endFranchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            externalDto.EndTeam,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

        // Determine if this entity exists. Do NOT trust that it says it is a new document!
        var entity = await _dataContext.Drives
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x =>
                x.ExternalIds.Any(z => z.SourceUrlHash == command.UrlHash &&
                                       z.Provider == command.SourceDataProvider));

        if (entity is null)
        {
            await ProcessNewEntity(
                command,
                externalDto,
                competitionId!.Value,
                startFranchiseSeasonId,
                endFranchiseSeasonId);
        }
        else
        {
            await ProcessUpdate(
                command,
                competitionId!.Value,
                externalDto,
                entity);
        }
    }

    private async Task<Guid?> GetCompetitionId(ProcessDocumentCommand command)
    {
        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("CompetitionId could not be parsed");
            return null;
        }

        var competitionExists = await _dataContext.Competitions
            .AsNoTracking()
            .AnyAsync(x => x.Id == competitionId);

        if (!competitionExists)
        {
            _logger.LogError("Competition not found for {CompetitionId}", competitionId);
            return null;
        }

        return competitionId;
    }
        
    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnEventCompetitionDriveDto externalDto,
        Guid competitionId,
        Guid? startFranchiseSeasonId,
        Guid? endFranchiseSeasonId)
    {
        var entity = externalDto.AsEntity(
            command.CorrelationId,
            _externalRefIdentityGenerator,
            competitionId,
            startFranchiseSeasonId,
            endFranchiseSeasonId);

        // TODO: If sequence is "-1", log a warning with the URL

        await _dataContext.Drives.AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        await ProcessPlays(
            command,
            competitionId,
            externalDto,
            entity);
    }

    private async Task ProcessPlays(
        ProcessDocumentCommand command,
        Guid competitionId,
        EspnEventCompetitionDriveDto externalDto,
        CompetitionDrive drive)
    {
        if (externalDto.Plays?.Items != null)
        {
            foreach (var play in externalDto.Plays.Items)
            {
                var playIdentity = _externalRefIdentityGenerator.Generate(play.Ref);

                // if we have the play, link it to the drive
                var playEntity = await _dataContext.CompetitionPlays
                    .FirstOrDefaultAsync(x => x.Id == playIdentity.CanonicalId);

                if (playEntity != null)
                {
                    playEntity.DriveId = drive.Id;
                }
                else
                {
                    // do we want to request sourcing?
                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: playIdentity.UrlHash,
                        ParentId: competitionId.ToString(),
                        Uri: new Uri(playIdentity.CleanUrl),
                        Sport: command.Sport,
                        SeasonYear: command.Season!.Value,
                        DocumentType: DocumentType.EventCompetitionPlay,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.GroupSeasonDocumentProcessor,
                        PropertyBag: new Dictionary<string, string>()
                        {
                            { "CompetitionDriveId", drive.Id.ToString()}
                        }
                    ));

                    await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                }

                await _dataContext.SaveChangesAsync();
            }
        }
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        Guid competitionId,
        EspnEventCompetitionDriveDto externalDto,
        CompetitionDrive entity)
    {
        await ProcessPlays(
            command,
            competitionId,
            externalDto,
            entity);
    }
}