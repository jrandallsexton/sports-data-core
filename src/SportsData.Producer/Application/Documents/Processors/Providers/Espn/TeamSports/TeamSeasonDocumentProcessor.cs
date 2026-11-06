using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeason)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.TeamSeason)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.TeamSeason)]
public class TeamSeasonDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public TeamSeasonDocumentProcessor(
        ILogger<TeamSeasonDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var externalProviderDto = command.Document.FromJson<EspnTeamSeasonDto>();

        if (externalProviderDto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnTeamSeasonDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(externalProviderDto.Ref?.ToString()))
        {
            _logger.LogError("EspnTeamSeasonDto Ref is null or empty. {@Command}", command);
            return;
        }

        if (command.SeasonYear is null)
        {
            _logger.LogError("Season year must be provided.");
            return;
        }

        var franchiseIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Franchise.Ref);

        var franchise = await _dataContext.Franchises
            .Include(f => f.Seasons)
            .FirstOrDefaultAsync(x => x.Id == franchiseIdentity.CanonicalId);

        if (franchise is null)
        {
            await PublishDependencyRequest<string?>(
                command,
                externalProviderDto.Franchise,
                parentId: null,
                DocumentType.Franchise);

            throw new ExternalDocumentNotSourcedException(
                $"Franchise {externalProviderDto.Franchise.Ref} not found. Requesting. Will retry.");
        }

        var franchiseSeasonIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Ref);
        var existingSeason = franchise.Seasons.FirstOrDefault(s => s.Id == franchiseSeasonIdentity.CanonicalId);

        // Delivered by a group's member-list fan-out (ParentId = GroupSeason)?
        // Then that group is the authoritative conference for this season.
        var memberOfGroupSeasonId = await ResolveMemberOfGroupSeasonId(command);

        if (existingSeason is not null)
        {
            await ProcessUpdateEntity(existingSeason, externalProviderDto, command, memberOfGroupSeasonId);
        }
        else
        {
            await ProcessNewEntity(franchise, command.SeasonYear.Value, externalProviderDto, command, memberOfGroupSeasonId);
        }

        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessDependencies(
        Franchise franchise,
        FranchiseSeason canonicalEntity,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command,
        Guid? memberOfGroupSeasonId)
    {
        if (string.IsNullOrEmpty(canonicalEntity.ColorCodeHex))
        {
            canonicalEntity.ColorCodeHex = franchise.ColorCodeHex;
        }

        if (string.IsNullOrEmpty(canonicalEntity.Abbreviation))
        {
            canonicalEntity.Abbreviation = franchise.Abbreviation ?? "UNK";
        }

        // Resolve VenueId via SourceUrlHash
        canonicalEntity.VenueId = await _dataContext.ResolveIdAsync<
            Venue, VenueExternalId>(
            dto.Venue,
            command.SourceDataProvider,
            () => _dataContext.Venues,
            externalIdsNav: "ExternalIds",
            key: v => v.Id);

        // The member list already named the conference; the document's own
        // `groups` link is the fallback, and for historical seasons it is
        // ESPN's CURRENT conference (see ApplyGroupMembership).
        if (memberOfGroupSeasonId is not null)
        {
            return;
        }

        if (dto.Groups?.Ref is null)
        {
            _logger.LogInformation("No group reference found in the DTO for TeamSeason {SeasonYear}", command.SeasonYear);
            return;
        }

        // Resolve GroupId via SourceUrlHash
        canonicalEntity.GroupSeasonId = await _dataContext.ResolveIdAsync<
            GroupSeason, GroupSeasonExternalId>(
            dto.Groups,
            command.SourceDataProvider,
            () => _dataContext.GroupSeasons,
            externalIdsNav: "ExternalIds",
            key: gs => gs.Id);

        if (canonicalEntity.GroupSeasonId is null)
        {
            await PublishDependencyRequest<string?>(
                command,
                dto.Groups,
                parentId: null,
                DocumentType.GroupSeason);

            throw new ExternalDocumentNotSourcedException(
                $"GroupSeason {dto.Groups.Ref} not found. Sourcing requested. Will retry.");
        }
    }

    private async Task ProcessNewEntity(
        Franchise franchise,
        int seasonYear,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command,
        Guid? memberOfGroupSeasonId)
    {
        var canonicalEntity = dto.AsEntity(
            _externalRefIdentityGenerator,
            franchise.Id,
            seasonYear,
            command.CorrelationId);

        await ProcessDependencies(franchise, canonicalEntity, dto, command, memberOfGroupSeasonId);

        await ApplyGroupMembership(canonicalEntity, memberOfGroupSeasonId);

        await ProcessChildren(canonicalEntity, dto, command, isNew: true);

        await _dataContext.FranchiseSeasons.AddAsync(canonicalEntity);

        await _publishEndpoint.Publish(new FranchiseSeasonCreated(
            canonicalEntity.ToCanonicalModel(),
            null,
            command.Sport,
            command.SeasonYear,
            command.CorrelationId,
            command.MessageId));
    }

    private async Task ProcessChildren(
        FranchiseSeason canonicalEntity,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command,
        bool isNew)
    {
        // logos - always spawn for new entities, respect ShouldSpawn for updates
        if (isNew || ShouldSpawn(DocumentType.FranchiseSeasonLogo, command))
        {
            await ProcessLogos(canonicalEntity.Id, dto, command);
        }

        // Wins/Losses/PtsFor/PtsAgainst
        if (isNew || ShouldSpawn(DocumentType.TeamSeasonRecord, command))
        {
            await PublishChildDocumentRequest(
                command,
                dto.Record,
                canonicalEntity.Id,
                DocumentType.TeamSeasonRecord);
        }

        // rankings
        if (isNew || ShouldSpawn(DocumentType.TeamSeasonRank, command))
        {
            await PublishChildDocumentRequest(
                command,
                dto.Ranks,
                canonicalEntity.Id,
                DocumentType.TeamSeasonRank);
        }

        // stats
        if (isNew || ShouldSpawn(DocumentType.TeamSeasonStatistics, command))
        {
            await PublishChildDocumentRequest(
                command,
                dto.Statistics,
                canonicalEntity.Id,
                DocumentType.TeamSeasonStatistics);
        }

        // athletes
        if (isNew || ShouldSpawn(DocumentType.AthleteSeason, command))
        {
            // ESPN renders the athletes $ref only on the CURRENT season's
            // TeamSeason document; historical documents omit it. Whether the
            // roster index itself (/seasons/{y}/teams/{id}/athletes) is
            // honest for historical seasons is PER-LEAGUE (verified by
            // content 2026-08-15):
            //   NCAAFB: honest — season-scoped rosters, counts vary by year.
            //   NFL:    fabricated — the CURRENT roster is rendered under
            //           every historical season path (Bengals 2015 contains
            //           Joe Burrow).
            //   MLB:    fabricated — identical counts across seasons.
            // Infer the index URL from the document's own ref ONLY for
            // leagues whose historical index is honest; for the others the
            // absent link stays absent and historical rosters remain
            // evidence-derived (athlete-season-fabrication-remediation.md).
            // The Provider fan-out carries this entity's id as ParentId,
            // which is what corroborates the FranchiseSeasonId binding in
            // AthleteSeasonDocumentProcessor.
            var athletes = dto.Athletes;
            if (athletes?.Ref is null && command.Sport == Sport.FootballNcaa)
            {
                athletes = new EspnResourceIndexItem
                {
                    Ref = EspnUriMapper.TeamSeasonRefToAthletesIndexRef(dto.Ref)
                };

                _logger.LogInformation(
                    "Athletes link absent on TeamSeason document (historical shape); inferred roster index ref. Ref={Ref}",
                    athletes.Ref);
            }

            await PublishChildDocumentRequest(
                command,
                athletes,
                canonicalEntity.Id,
                DocumentType.AthleteSeason);
        }

        // leaders
        if (isNew || ShouldSpawn(DocumentType.TeamSeasonLeaders, command))
        {
            await PublishChildDocumentRequest(
                command,
                dto.Leaders,
                canonicalEntity.Id,
                DocumentType.TeamSeasonLeaders);
        }

        // injuries
        if (isNew || ShouldSpawn(DocumentType.TeamSeasonInjuries, command))
        {
            await PublishChildDocumentRequest(
                command,
                dto.Injuries,
                canonicalEntity.Id,
                DocumentType.TeamSeasonInjuries);
        }

        if (isNew || ShouldSpawn(DocumentType.TeamSeasonNote, command))
        {
            await PublishChildDocumentRequest(
                command,
                dto.Notes,
                canonicalEntity.Id,
                DocumentType.TeamSeasonNote);
        }

        // Process record ATS (Against The Spread)
        if (isNew || ShouldSpawn(DocumentType.TeamSeasonRecordAts, command))
        {
            await PublishChildDocumentRequest(
                command,
                dto.AgainstTheSpreadRecords,
                canonicalEntity.Id,
                DocumentType.TeamSeasonRecordAts);
        }

        // Process awards
        if (isNew || ShouldSpawn(DocumentType.TeamSeasonAward, command))
        {
            await PublishChildDocumentRequest(
                command,
                dto.Awards,
                canonicalEntity.Id,
                DocumentType.TeamSeasonAward);
        }

        // Process projection
        if (isNew || ShouldSpawn(DocumentType.TeamSeasonProjection, command))
        {
            await PublishChildDocumentRequest(
                command,
                dto.Projection,
                canonicalEntity.Id,
                DocumentType.TeamSeasonProjection);
        }

        // Process events (schedule)
        if (isNew || ShouldSpawn(DocumentType.Event, command))
        {
            await PublishChildDocumentRequest(
                command,
                dto.Events,
                canonicalEntity.Id,
                DocumentType.Event);
        }

        // Process coaches
        if (isNew || ShouldSpawn(DocumentType.CoachSeason, command))
        {
            await PublishChildDocumentRequest(
                command,
                dto.Coaches,
                canonicalEntity.Id,
                DocumentType.CoachSeason);
        }

        // TODO: LOW: Add recruiting class sourcing - /v2/sports/football/leagues/college-football/recruiting/{year}/classes/{teamId}
        // TODO: LOW: Add college entity sourcing - /v2/colleges/{collegeId} (separate from franchise, may contain additional university metadata)
    }

    private async Task ProcessLogos(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Logos is null || dto.Logos.Count == 0)
        {
            _logger.LogInformation("No logos found in the DTO for TeamSeason {SeasonYear}", command.SeasonYear);
            return;
        }

        var imageEvents = EventFactory.CreateProcessImageRequests(
            _externalRefIdentityGenerator,
            dto.Logos,
            franchiseSeasonId,
            command.Sport,
            command.SeasonYear,
            DocumentType.FranchiseSeasonLogo,
            command.SourceDataProvider,
            command.CorrelationId,
            command.MessageId);

        if (imageEvents.Count > 0)
        {
            _logger.LogInformation("Publishing {Count} image requests for TeamSeason {SeasonYear}", imageEvents.Count, command.SeasonYear);
            await _publishEndpoint.PublishBatch(imageEvents);
        }
    }

    private async Task ProcessUpdateEntity(
        FranchiseSeason existing,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command,
        Guid? memberOfGroupSeasonId)
    {
        // The document's `groups` link never overwrites an existing value;
        // only a member list corrects it.
        await ApplyGroupMembership(existing, memberOfGroupSeasonId);

        await ProcessChildren(existing, dto, command, isNew: false);
    }

    private async Task<Guid?> ResolveMemberOfGroupSeasonId(ProcessDocumentCommand command)
    {
        if (!Guid.TryParse(command.ParentId, out var parentId))
            return null;

        var isGroupSeason = await _dataContext.GroupSeasons
            .AsNoTracking()
            .AnyAsync(g => g.Id == parentId);

        return isGroupSeason ? parentId : null;
    }

    /// <summary>
    /// Conference membership comes from the group's member list
    /// (GroupSeasonDocumentProcessor fans out its `teams` index with itself as
    /// ParentId), not from this document's `groups` link: ESPN renders the
    /// CURRENT conference on historical TeamSeason documents (TCU 2005 links
    /// the Big 12; the 2005 Mountain West list contains TCU). Also writes
    /// GroupSeasonMap, formerly built by hand with
    /// sql/pgsql/groupSeasonHierarchy.sql.
    /// </summary>
    private async Task ApplyGroupMembership(FranchiseSeason franchiseSeason, Guid? memberOfGroupSeasonId)
    {
        if (memberOfGroupSeasonId is not null)
        {
            var incomingId = await GroupSeasonHierarchy.MatchVariantAsync(
                _dataContext,
                _externalRefIdentityGenerator,
                memberOfGroupSeasonId.Value,
                franchiseSeason.GroupSeasonId,
                _logger);

            var currentId = franchiseSeason.GroupSeasonId;

            if (currentId is null)
            {
                franchiseSeason.GroupSeasonId = incomingId;

                _logger.LogInformation(
                    "GroupSeason set from member list. FranchiseSeasonId={FranchiseSeasonId}, GroupSeasonId={GroupSeasonId}",
                    franchiseSeason.Id, incomingId);
            }
            else if (currentId != incomingId)
            {
                // Most specific wins: a conference's list must not replace the
                // division its own children already assigned.
                if (await GroupSeasonHierarchy.IsAncestorAsync(_dataContext, incomingId, currentId.Value))
                {
                    _logger.LogDebug(
                        "Member list is an ancestor of the current group; keeping the more specific one. FranchiseSeasonId={FranchiseSeasonId}, CurrentGroupSeasonId={CurrentGroupSeasonId}, IncomingGroupSeasonId={IncomingGroupSeasonId}",
                        franchiseSeason.Id, currentId, incomingId);
                }
                else
                {
                    franchiseSeason.GroupSeasonId = incomingId;

                    _logger.LogInformation(
                        "GroupSeason corrected from member list. FranchiseSeasonId={FranchiseSeasonId}, OldGroupSeasonId={OldGroupSeasonId}, NewGroupSeasonId={NewGroupSeasonId}",
                        franchiseSeason.Id, currentId, incomingId);
                }
            }
        }

        if (franchiseSeason.GroupSeasonId is null)
            return;

        var map = await GroupSeasonHierarchy.BuildMapAsync(_dataContext, franchiseSeason.GroupSeasonId.Value);

        if (map is not null && !string.Equals(franchiseSeason.GroupSeasonMap, map, StringComparison.Ordinal))
        {
            franchiseSeason.GroupSeasonMap = map;
        }
    }
}
