using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Core.Eventing.Events.Seasons;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

// Alias to disambiguate from the sibling SportsData.Producer.Application.SeasonWeek
// namespace, which name-resolution prefers over the entity type when the bare name
// is used in this file.
using SeasonWeekEntity = SportsData.Producer.Infrastructure.Data.Entities.SeasonWeek;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonTypeWeekRankings)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.SeasonTypeWeekRankings)]
public class SeasonTypeWeekRankingsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{

    public SeasonTypeWeekRankingsDocumentProcessor(
        ILogger<SeasonTypeWeekRankingsDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        IEventBus publishEndpoint)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnFootballSeasonTypeWeekRankingsDto. {@Command}", command);
            return;
        }

        if (command.SeasonYear is null)
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

            seasonPollId = seasonPoll.Id;
        }

        // Determine the SeasonWeek for this poll
        // can be null: preseason/postseason polls
        SeasonWeekEntity? seasonWeek = null;

        if (dto.Season.Type.Week is not null)
        {
            // Note: ESPN publishes the poll at the end of the week
            // Example: Week 9 poll is published on the Sunday after Week 9 games
            // therefore we use it for Week 10
            // TODO:  At the end of the season, correct this data and adjust for next season
            seasonWeek = await _dataContext.SeasonWeeks
                .Include(x => x.Season)
                .Include(x => x.ExternalIds)
                .Include(x => x.Rankings)
                .ThenInclude(r => r.ExternalIds)
                .Where(x => x.Season!.Year == command.SeasonYear!.Value && x.Number == dto.Season.Type.Week.Number + 1)
                .AsSplitQuery()
                .FirstOrDefaultAsync();

            if (seasonWeek == null)
            {
                var seasonPhaseIdentity = _externalRefIdentityGenerator.Generate(dto.Season.Type.Ref);

                await PublishDependencyRequest(
                    command,
                    dto.Season.Type.Week,
                    seasonPhaseIdentity.CanonicalId,
                    DocumentType.SeasonTypeWeek);

                throw new ExternalDocumentNotSourcedException("SeasonWeek not found. Sourcing requested. Will retry.");
            }
        }

        var dtoIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var pollWeek = await _dataContext.SeasonPollWeeks
            .Where(x => x.Id == dtoIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (pollWeek is null)
        {
            await ProcessNewEntity(dto, dtoIdentity, seasonPollId, seasonWeek, command);
        }
        else
        {
            ProcessExistingEntity(dto, pollWeek);
        }
    }

    private async Task ProcessNewEntity(
        EspnFootballSeasonTypeWeekRankingsDto dto,
        ExternalRefIdentity dtoIdentity,
        Guid seasonPollId,
        SeasonWeekEntity? seasonWeek,
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
            foreach (var missing in missingFranchiseSeasons)
            {
                _logger.LogError("Missing FranchiseSeason for Team Ref {TeamRef} with expected URI {Uri}",
                    missing.Key, missing.Value);

                var franchiseRef = EspnUriMapper.TeamSeasonToFranchiseRef(missing.Value);
                var franchiseId = _externalRefIdentityGenerator.Generate(franchiseRef).CanonicalId;

                // Create a temporary EspnLinkDto for the helper method
                var teamLinkDto = new EspnLinkDto { Ref = missing.Value };
                await PublishDependencyRequest(
                    command,
                    teamLinkDto,
                    franchiseId,
                    DocumentType.TeamSeason);
            }

            await _dataContext.SaveChangesAsync();

            throw new ExternalDocumentNotSourcedException($"{missingFranchiseSeasons.Count} FranchiseSeasons could not be resolved. Sourcing requested. Will retry this job.");
        }

        // Create the entity from the DTO
        var entity = dto.AsEntity(
            seasonPollId,
            seasonWeek?.Id,
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

        // Resolve poll slug for the event payload. The SeasonWeek row was
        // already loaded earlier in ProcessInternal and threaded in here, so
        // we reuse its dates without re-querying. SeasonPoll wasn't loaded
        // earlier — one cheap PK lookup, only fires on poll publication
        // (weekly per sport during NCAAFB season).
        var seasonPoll = await _dataContext.SeasonPolls
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == seasonPollId);

        // Publish BEFORE SaveChanges so the bus-outbox interceptor captures
        // the message in the same transaction as the entity write — if the
        // save fails, the captured publish is rolled back with it.
        await _publishEndpoint.Publish(new SeasonPollWeekCreated(
            SeasonPollWeekId: entity.Id,
            SeasonPollId: seasonPollId,
            SeasonWeekId: seasonWeek?.Id,
            SeasonWeekStartDate: seasonWeek?.StartDate,
            SeasonWeekEndDate: seasonWeek?.EndDate,
            SeasonYear: command.SeasonYear,
            PollSlug: seasonPoll?.Slug,
            Ref: null,
            Sport: command.Sport,
            CorrelationId: command.CorrelationId,
            CausationId: Guid.NewGuid()));

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Created SeasonPollWeek entity {@SeasonRankingId}", entity.Id);
    }

    private async Task PublishFranchiseSeasonDocumentRequestedEvent(
        ProcessDocumentCommand command,
        ExternalRefIdentity identity)
    {
        try
        {
            // Create a temporary EspnLinkDto for the helper method
            var teamLinkDto = new EspnLinkDto { Ref = new Uri(identity.CleanUrl) };
            await PublishChildDocumentRequest<string?>(
                command,
                teamLinkDto,
                parentId: null,
                DocumentType.TeamSeason);
        }
        catch (UriFormatException ex)
        {
            _logger.LogError(ex,
                "❌ INVALID_URI: Failed to parse URI for FranchiseSeason document request. " +
                "InvalidUrl={InvalidUrl}",
                identity.CleanUrl);
        }
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


    private void ProcessExistingEntity(
        EspnFootballSeasonTypeWeekRankingsDto dto,
        SeasonPollWeek existing)
    {
        var incomingLastUpdated = dto.LastUpdated.TryParseUtcNullable();

        // ESPN stamps each poll-week with a lastUpdated timestamp. During backfills
        // and at-least-once redelivery the same document arrives repeatedly with
        // identical content, so its lastUpdated has not advanced past what we stored
        // — there is nothing to do. This is the overwhelmingly common path (and was
        // previously logged at Error, which spammed Seq during re-sourcing); keep it
        // quiet.
        var isRevision = incomingLastUpdated is not null &&
                         (existing.LastUpdatedUtc is null || incomingLastUpdated > existing.LastUpdatedUtc);

        if (!isRevision)
        {
            _logger.LogDebug(
                "SeasonPollWeek {SeasonPollWeekId} already current (stored lastUpdated {StoredLastUpdated}); no update needed.",
                existing.Id, existing.LastUpdatedUtc);
            return;
        }

        // ESPN revised this poll-week (e.g. an end-of-week correction). Applying the
        // revision in place — reconciling scalar fields plus the ranked Entries
        // collection — is not yet implemented; surfaced at Information (not Error) so a
        // genuine revision stays visible without drowning Seq. Revisions do not occur
        // for completed seasons, so this path is dormant during historical backfills.
        _logger.LogInformation(
            "SeasonPollWeek {SeasonPollWeekId} was revised upstream (incoming lastUpdated {IncomingLastUpdated} > stored {StoredLastUpdated}); in-place update not yet implemented.",
            existing.Id, incomingLastUpdated, existing.LastUpdatedUtc);
    }
}