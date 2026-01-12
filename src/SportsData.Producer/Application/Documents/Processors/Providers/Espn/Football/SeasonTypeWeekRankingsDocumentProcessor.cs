using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonTypeWeekRankings)]
public class SeasonTypeWeekRankingsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    private readonly DocumentProcessingConfig _config;

    public SeasonTypeWeekRankingsDocumentProcessor(
        ILogger<SeasonTypeWeekRankingsDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        IEventBus publishEndpoint,
        DocumentProcessingConfig config)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
        _config = config;
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
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
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "Dependency not ready. Will retry later.");
                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                await _publishEndpoint.Publish(docCreated);
                
                await _dataContext.SaveChangesAsync();
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
        var dto = command.Document.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnFootballSeasonTypeWeekRankingsDto. {@Command}", command);
            return;
        }

        if (command.Season is null)
        {
            _logger.LogError("Command does not contain a valid SeasonYear. {@Command}", command);
            return;
        }

        // Determine the Poll to which this PollWeek belongs (eg. AP, Coaches)
        if (!Guid.TryParse(command.ParentId, out var seasonPollId))
        {
            _logger.LogWarning("SeasonPollId not on command.ParentId. Attempting to derive.");

            var seasonPollRef = EspnUriMapper.SeasonPollWeekRefToSeasonPollRef(dto.Ref);
            var seasonPollIdentity = _externalRefIdentityGenerator.Generate(seasonPollRef);

            var seasonPoll = await _dataContext.SeasonPolls
                .FirstOrDefaultAsync(x => x.Id == seasonPollIdentity.CanonicalId);

            if (seasonPoll is null)
            {
                _logger.LogError("SeasonPollId could not be derived/inferred. {@Command}", command);
                return;
            }
        }

        // Determine the SeasonWeek for this poll
        // can be null: preseason/postseason polls
        Guid? seasonWeekId = null;

        if (dto.Season.Type.Week is not null)
        {
            // Note: ESPN publishes the poll at the end of the week
            // Example: Week 9 poll is published on the Sunday after Week 9 games
            // therefore we use it for Week 10
            // TODO:  At the end of the season, correct this data and adjust for next season
            var seasonWeek = await _dataContext.SeasonWeeks
                .Include(x => x.Season)
                .Include(x => x.ExternalIds)
                .Include(x => x.Rankings)
                .ThenInclude(r => r.ExternalIds)
                .Where(x => x.Season!.Year == command.Season!.Value && x.Number == dto.Season.Type.Week.Number + 1)
                .AsSplitQuery()
                .FirstOrDefaultAsync();

            if (seasonWeek == null)
            {
                var seasonPhaseIdentity = _externalRefIdentityGenerator.Generate(dto.Season.Type.Ref);

                if (!_config.EnableDependencyRequests)
                {
                    _logger.LogWarning(
                        "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                        DocumentType.SeasonTypeWeek,
                        nameof(SeasonTypeWeekRankingsDocumentProcessor<TDataContext>),
                        dto.Season.Type.Week.Ref);
                    throw new ExternalDocumentNotSourcedException(
                        "SeasonWeek not found. Sourcing requested. Will retry.");
                }
                else
                {
                    // Legacy mode: keep existing DocumentRequested logic
                    _logger.LogWarning(
                        "SeasonWeek not found. Raising DocumentRequested (override mode). WeekRef={WeekRef}",
                        dto.Season.Type.Week.Ref);
                    
                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: HashProvider.GenerateHashFromUri(dto.Season.Type.Week.Ref),
                        ParentId: seasonPhaseIdentity.CanonicalId.ToString(),
                        Uri: dto.Season.Type.Week.Ref,
                        Ref: null,
                        Sport: Sport.FootballNcaa,
                        SeasonYear: command.Season,
                        DocumentType: DocumentType.SeasonTypeWeek,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.SeasonTypeWeekRankingsDocumentProcessor
                    ));
                    
                    await _dataContext.SaveChangesAsync();

                    _logger.LogError("SeasonWeek not found. Sourcing requested. Will retry.");
                    throw new ExternalDocumentNotSourcedException("SeasonWeek not found. Sourcing requested. Will retry.");
                }
            }

            seasonWeekId = seasonWeek.Id;
        }

        var dtoIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var pollWeek = await _dataContext.SeasonPollWeeks
            .Where(x => x.Id == dtoIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (pollWeek is null)
        {
            await ProcessNewEntity(dto, dtoIdentity, seasonPollId, seasonWeekId, command);
        }
        else 
        {
            await ProcessExistingEntity();
        }
    }

    private async Task ProcessNewEntity(
        EspnFootballSeasonTypeWeekRankingsDto dto,
        ExternalRefIdentity dtoIdentity,
        Guid seasonPollId,
        Guid? seasonWeekId,
        ProcessDocumentCommand command)
    {
        // We need to create a mapping of the Team's season ref to the FranchiseSeasonId
        var (franchiseDictionary, missingFranchiseSeasons) = await ResolveFranchiseSeasonIdsAsync(
            dto,
            _externalRefIdentityGenerator,
            _dataContext,
            command,
            _logger);

        if (missingFranchiseSeasons.Any())
        {
            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Count={Count}",
                    DocumentType.TeamSeason,
                    nameof(SeasonTypeWeekRankingsDocumentProcessor<TDataContext>),
                    missingFranchiseSeasons.Count);
                throw new ExternalDocumentNotSourcedException(
                    $"{missingFranchiseSeasons.Count} FranchiseSeasons could not be resolved. Sourcing requested. Will retry this job.");
            }
            else
            {
                // Legacy mode: keep existing DocumentRequested logic
                _logger.LogWarning(
                    "FranchiseSeasons not found. Raising DocumentRequested (override mode). Count={Count}",
                    missingFranchiseSeasons.Count);
                
                foreach (var missing in missingFranchiseSeasons)
                {
                    _logger.LogError("Missing FranchiseSeason for Team Ref {TeamRef} with expected URI {Uri}",
                        missing.Key, missing.Value);

                    var franchiseRef = EspnUriMapper.TeamSeasonToFranchiseRef(missing.Value);
                    var franchiseId = _externalRefIdentityGenerator.Generate(franchiseRef).CanonicalId;

                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: missing.Key.ToString(),
                        ParentId: franchiseId.ToString(),
                        Uri: missing.Value,
                        Ref: null,
                        Sport: Sport.FootballNcaa,
                        SeasonYear: command.Season!.Value,
                        DocumentType: DocumentType.TeamSeason,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.SeasonTypeWeekRankingsDocumentProcessor
                    ));
                }

                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException($"{missingFranchiseSeasons.Count} FranchiseSeasons could not be resolved. Sourcing requested. Will retry this job.");
            }
        }

        // Create the entity from the DTO
        var entity = dto.AsEntity(
            seasonPollId,
            seasonWeekId,
            _externalRefIdentityGenerator,
            franchiseDictionary,
            command.CorrelationId);

        // Request FranchiseSeason updates for all affected teams (ranked, dropped out, etc) using base helper
        foreach (var ranking in dto.Ranks)
        {
            var franchiseSeasonIdentity = _externalRefIdentityGenerator.Generate(ranking.Team.Ref);
            await PublishFranchiseSeasonDocumentRequestedEvent(command, franchiseSeasonIdentity);
        }

        if (dto.Others != null)
        {
            foreach (var ranking in dto.Others)
            {
                var franchiseSeasonIdentity = _externalRefIdentityGenerator.Generate(ranking.Team.Ref);
                await PublishFranchiseSeasonDocumentRequestedEvent(command, franchiseSeasonIdentity);
            }
        }

        if (dto.DroppedOut != null)
        {
            foreach (var ranking in dto.DroppedOut)
            {
                var franchiseSeasonIdentity = _externalRefIdentityGenerator.Generate(ranking.Team.Ref);
                await PublishFranchiseSeasonDocumentRequestedEvent(command, franchiseSeasonIdentity);
            }
        }

        // Add to EF and save
        await _dataContext.SeasonPollWeeks.AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Created SeasonPollWeek entity {@SeasonRankingId}", entity.Id);
    }

    private async Task PublishFranchiseSeasonDocumentRequestedEvent(
        ProcessDocumentCommand command,
        ExternalRefIdentity identity)
    {
        await _publishEndpoint.Publish(new DocumentRequested(
            Id: identity.UrlHash,
            ParentId: null,
            Uri: new Uri(identity.CleanUrl),
            Ref: null,
            Sport: command.Sport,
            SeasonYear: command.Season,
            DocumentType: DocumentType.TeamSeason,
            SourceDataProvider: command.SourceDataProvider,
            CorrelationId: command.CorrelationId,
            CausationId: CausationId.Producer.SeasonTypeWeekRankingsDocumentProcessor
        ));
    }

    private static async Task<(Dictionary<string, Guid> franchiseDictionary, Dictionary<Guid, Uri> missingFranchiseSeasons)>
        ResolveFranchiseSeasonIdsAsync(
            EspnFootballSeasonTypeWeekRankingsDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            TDataContext dataContext,
            ProcessDocumentCommand command,
            ILogger logger)
    {
        var franchiseDictionary = new Dictionary<string, Guid>();
        var missingFranchiseSeasons = new Dictionary<Guid, Uri>();

        foreach (var entry in dto.Ranks)
        {
            var teamRef = entry.Team?.Ref;
            if (teamRef is null)
                continue;

            var teamIdentity = externalRefIdentityGenerator.Generate(teamRef);

            var franchiseSeasonId = await dataContext.ResolveIdAsync<
                FranchiseSeason, FranchiseSeasonExternalId>(
                entry.Team!,
                command.SourceDataProvider,
                () => dataContext.FranchiseSeasons,
                externalIdsNav: "ExternalIds",
                key: fs => fs.Id);

            if (franchiseSeasonId.HasValue)
            {
                franchiseDictionary.TryAdd(teamRef.ToCleanUrl(), franchiseSeasonId.Value);
            }
            else
            {
                missingFranchiseSeasons.TryAdd(teamIdentity.CanonicalId, teamRef);
            }
        }

        if (dto.Others is not null)
        {
            foreach (var entry in dto.Others)
            {
                var teamRef = entry.Team?.Ref;
                if (teamRef is null)
                    continue;

                var teamIdentity = externalRefIdentityGenerator.Generate(teamRef);

                var franchiseSeasonId = await dataContext.ResolveIdAsync<
                    FranchiseSeason, FranchiseSeasonExternalId>(
                    entry.Team!,
                    command.SourceDataProvider,
                    () => dataContext.FranchiseSeasons,
                    externalIdsNav: "ExternalIds",
                    key: fs => fs.Id);

                if (franchiseSeasonId.HasValue)
                {
                    franchiseDictionary.TryAdd(teamRef.ToCleanUrl(), franchiseSeasonId.Value);
                }
                else
                {
                    missingFranchiseSeasons.TryAdd(teamIdentity.CanonicalId, teamRef);
                }
            }
        }

        if (dto.DroppedOut is not null)
        {
            foreach (var entry in dto.DroppedOut)
            {
                var teamRef = entry.Team?.Ref;
                if (teamRef is null)
                    continue;

                var teamIdentity = externalRefIdentityGenerator.Generate(teamRef);

                var franchiseSeasonId = await dataContext.ResolveIdAsync<
                    FranchiseSeason, FranchiseSeasonExternalId>(
                    entry.Team!,
                    command.SourceDataProvider,
                    () => dataContext.FranchiseSeasons,
                    externalIdsNav: "ExternalIds",
                    key: fs => fs.Id);

                if (franchiseSeasonId.HasValue)
                {
                    franchiseDictionary.TryAdd(teamRef.ToCleanUrl(), franchiseSeasonId.Value);
                }
                else
                {
                    missingFranchiseSeasons.TryAdd(teamIdentity.CanonicalId, teamRef);
                }
            }
        }

        return (franchiseDictionary, missingFranchiseSeasons);
    }


    private async Task ProcessExistingEntity()
    {
        _logger.LogError("Update detected. Not implemented");
        await Task.CompletedTask;
    }
}