using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.GroupSeason)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.GroupSeason)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.GroupSeason)]
public class GroupSeasonDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    /// <summary>
    /// Filter for the member-list fan-out. Provider walks a leaf group's
    /// `teams` index and delivers each entry as a TeamSeason document with
    /// this group as ParentId; TeamSeasonDocumentProcessor takes the parent as
    /// the authoritative conference. Document-only (empty): membership needs
    /// no TeamSeason children, and the empty filter is enforced at the
    /// child-document publish choke point even for new entities, so a list
    /// never cascades into records, rosters, or schedules. A team-season the
    /// list creates is correctly grouped but otherwise minimal: only its logo
    /// image requests go out (they publish directly, not through the choke
    /// point). Ordinary sourcing hydrates it later and never overwrites the
    /// group.
    ///
    /// Known gap, accepted: Provider's historical republish cooldown
    /// (DocumentPublishCooldownMinutes, 90 in prod) applies to null/empty
    /// filters, so a member list processed within the window after ordinary
    /// sourcing published the same TeamSeason documents is dropped. That only
    /// happens during fresh historical sourcing (a rebuilt database, a new
    /// league's history); afterwards, run
    /// POST api/franchise-seasons/seasonYear/{year}/conferences/source once
    /// the window has passed.
    /// </summary>
    public static readonly IReadOnlyCollection<DocumentType> MembershipLinkedDocumentTypes = [];

    public GroupSeasonDocumentProcessor(
        ILogger<GroupSeasonDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnGroupSeasonDto>();
        if (dto is null)
        {
            _logger.LogError("Invalid GroupSeason document. {@Command}", command);
            return;
        }

        var identity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var groupSeason = await _dataContext.GroupSeasons
            .Include(gs => gs.ExternalIds)
            .FirstOrDefaultAsync(gs => gs.Id == identity.CanonicalId);

        if (groupSeason is null)
        {
            await HandleNew(dto, command);
        }
        else
        {
            await HandleExisting(dto, groupSeason, command);
        }
    }

    private async Task HandleNew(
        EspnGroupSeasonDto dto,
        ProcessDocumentCommand command)
    {
        // Derive the season year from the document's own $ref URL (e.g. .../seasons/2016/types/3/groups/151).
        // DO NOT use command.Season here — the parent command carries the *parent's* year, which can differ
        // when ESPN reuses older group structures as child refs (the source of duplicate SeasonYear bugs).
        int seasonYear;
        if (EspnUriMapper.TryExtractSeasonYear(dto.Ref, out var yearFromUrl))
        {
            if (command.SeasonYear.HasValue && command.SeasonYear.Value != yearFromUrl)
            {
                _logger.LogWarning(
                    "GroupSeason SeasonYear mismatch: URL contains {UrlYear} but command.SeasonYear is {CommandSeasonYear}. "
                    + "Using URL year. Ref={Ref}",
                    yearFromUrl, command.SeasonYear.Value, dto.Ref);
            }
            seasonYear = yearFromUrl;
        }
        else
        {
            if (!command.SeasonYear.HasValue)
            {
                _logger.LogError(
                    "Cannot determine SeasonYear: URL extraction failed and command.SeasonYear is null. Ref={Ref}",
                    dto.Ref);
                return;
            }
            _logger.LogWarning(
                "Could not extract season year from GroupSeason ref; falling back to command.SeasonYear={CommandSeasonYear}. Ref={Ref}",
                command.SeasonYear, dto.Ref);
            seasonYear = command.SeasonYear.Value;
        }

        var groupSeasonEntity = dto.AsEntity(
            _externalRefIdentityGenerator,
            seasonYear,
            command.CorrelationId);

        // seasonRef
        if (dto.Season?.Ref is not null)
        {
            var seasonId = await _dataContext.ResolveIdAsync<
                Season, SeasonExternalId>(
                dto.Season,
                command.SourceDataProvider,
                () => _dataContext.Seasons,
                externalIdsNav: "ExternalIds",
                key: s => s.Id);

            if (seasonId is null)
            {
                await PublishDependencyRequest<string?>(
                    command,
                    dto.Season,
                    parentId: null,
                    DocumentType.Season);

                throw new ExternalDocumentNotSourcedException($"Season {dto.Season.Ref} not found. Requested. Will retry.");
            }
            groupSeasonEntity.SeasonId = seasonId;
        }

        // handle parent
        if (dto.Parent?.Ref is not null)
        {
            var parentId = await _dataContext.ResolveIdAsync<
                GroupSeason, GroupSeasonExternalId>(
                dto.Parent,
                command.SourceDataProvider,
                () => _dataContext.GroupSeasons,
                externalIdsNav: "ExternalIds",
                key: gs => gs.Id);

            if (parentId is null)
            {
                await PublishDependencyRequest<string?>(
                    command,
                    dto.Parent,
                    parentId: null,
                    DocumentType.GroupSeason);

                throw new ExternalDocumentNotSourcedException($"GroupSeason parent {dto.Parent.Ref} not found. Requested. Will retry.");
            }
            groupSeasonEntity.ParentId = parentId;
        }

        await ProcessChildren(dto, groupSeasonEntity, command, isNew: true);

        await ProcessLogos(dto, groupSeasonEntity, seasonYear, command);

        await _dataContext.GroupSeasons.AddAsync(groupSeasonEntity);
        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessLogos(
        EspnGroupSeasonDto dto,
        GroupSeason groupSeasonEntity,
        int seasonYear,
        ProcessDocumentCommand command)
    {
        // The conference/division logo (dto.Logos) was previously dropped — no
        // image request was published, so the GroupSeasonLogo table stayed empty.
        // Publish a request per logo (the GroupSeasonLogoResponseProcessor writes
        // the row after download), mirroring TeamSeasonDocumentProcessor.
        if (dto.Logos is null || dto.Logos.Count == 0)
        {
            return;
        }

        var imageEvents = EventFactory.CreateProcessImageRequests(
            _externalRefIdentityGenerator,
            dto.Logos,
            groupSeasonEntity.Id,
            command.Sport,
            seasonYear,
            DocumentType.GroupSeasonLogo,
            command.SourceDataProvider,
            command.CorrelationId,
            command.MessageId);

        if (imageEvents.Count > 0)
        {
            _logger.LogInformation(
                "Publishing {Count} GroupSeasonLogo image requests for GroupSeason {GroupSeasonId} ({SeasonYear}).",
                imageEvents.Count, groupSeasonEntity.Id, seasonYear);

            await _publishEndpoint.PublishBatch(imageEvents);
        }
    }

    private async Task ProcessChildren(
        EspnGroupSeasonDto dto,
        GroupSeason groupSeasonEntity,
        ProcessDocumentCommand command,
        bool isNew)
    {
        if (dto.Children?.Ref is not null &&
            (isNew || IsExplicitlyRequested(DocumentType.GroupSeason, command)))
        {
            await PublishChildDocumentRequest(
                command,
                dto.Children,
                groupSeasonEntity.Id,
                DocumentType.GroupSeason);
        }

        // Member list: authoritative conference membership. ESPN's TeamSeason
        // document links each team to its CURRENT conference even for old
        // seasons (TCU 2005 -> Big 12), but the group's own `teams` index is
        // season-accurate (2005 Mountain West contains TCU). Leaf groups only:
        // a conference with divisions lists its members again at the
        // conference level, and the division is the more specific answer.
        // ESPN serves no `children` link on leaf groups.
        if (dto.Children?.Ref is null &&
            dto.Teams?.Ref is not null &&
            (isNew || IsExplicitlyRequested(DocumentType.TeamSeason, command)))
        {
            await PublishChildDocumentRequest(
                command,
                dto.Teams,
                groupSeasonEntity.Id,
                DocumentType.TeamSeason,
                includeLinkedDocumentTypes: MembershipLinkedDocumentTypes,
                // The group's year comes from its own $ref (see HandleNew); the
                // command's year can belong to whichever parent reached it, and
                // TeamSeason keys the FranchiseSeason year off the request.
                seasonYear: groupSeasonEntity.SeasonYear);
        }

        // NOTE: Future enhancements:
        // - Standings: Can be derived from game results or sourced from ESPN standings endpoint
        // - Links: Additional related resources (if ESPN provides conference-level links)
    }

    /// <summary>
    /// Re-processing an existing group. Ordinary re-hits (a null filter) stay
    /// no-ops for children, as before: walking the subtree and every member
    /// list on each re-hit would re-process every team in the conference.
    /// Only a command that names the type walks it — the group backfill
    /// (FranchiseSeason conference re-sourcing) sends GroupSeason + TeamSeason.
    /// </summary>
    private async Task HandleExisting(
        EspnGroupSeasonDto dto,
        GroupSeason groupSeason,
        ProcessDocumentCommand command)
    {
        var abbreviation = GroupSeasonHierarchy.ResolveAbbreviation(dto);

        if (!string.Equals(groupSeason.Abbreviation, abbreviation, StringComparison.Ordinal) &&
            string.Equals(groupSeason.Abbreviation, GroupSeasonHierarchy.UnknownAbbreviation, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "GroupSeason abbreviation resolved. GroupSeasonId={GroupSeasonId}, Old={OldAbbreviation}, New={NewAbbreviation}",
                groupSeason.Id, groupSeason.Abbreviation, abbreviation);

            groupSeason.Abbreviation = abbreviation;
            await _dataContext.SaveChangesAsync();

            await RefreshMapsBelow(groupSeason);
        }

        await ProcessChildren(dto, groupSeason, command, isNew: false);

        await _dataContext.SaveChangesAsync();
    }

    /// <summary>
    /// A changed abbreviation changes the path of every group beneath it, so
    /// recompute GroupSeasonMap for the team-seasons in that subtree.
    /// </summary>
    private async Task RefreshMapsBelow(GroupSeason groupSeason)
    {
        var seasonGroups = await _dataContext.GroupSeasons
            .AsNoTracking()
            .Where(g => g.SeasonYear == groupSeason.SeasonYear)
            .Select(g => new { g.Id, g.ParentId })
            .ToListAsync();

        var subtree = new HashSet<Guid> { groupSeason.Id };
        bool added;
        do
        {
            added = false;
            foreach (var g in seasonGroups)
            {
                if (g.ParentId is not null && subtree.Contains(g.ParentId.Value) && subtree.Add(g.Id))
                    added = true;
            }
        } while (added);

        var franchiseSeasons = await _dataContext.FranchiseSeasons
            .Where(fs => fs.GroupSeasonId != null && subtree.Contains(fs.GroupSeasonId.Value))
            .ToListAsync();

        var maps = new Dictionary<Guid, string?>();
        foreach (var fs in franchiseSeasons)
        {
            var groupId = fs.GroupSeasonId!.Value;
            if (!maps.TryGetValue(groupId, out var map))
            {
                map = await GroupSeasonHierarchy.BuildMapAsync(_dataContext, groupId);
                maps[groupId] = map;
            }

            fs.GroupSeasonMap = map;
        }

        _logger.LogInformation(
            "GroupSeasonMap refreshed below GroupSeasonId={GroupSeasonId}. FranchiseSeasons={Count}",
            groupSeason.Id, franchiseSeasons.Count);
    }

    private static bool IsExplicitlyRequested(DocumentType documentType, ProcessDocumentCommand command) =>
        command.IncludeLinkedDocumentTypes?.Contains(documentType) == true;
}
