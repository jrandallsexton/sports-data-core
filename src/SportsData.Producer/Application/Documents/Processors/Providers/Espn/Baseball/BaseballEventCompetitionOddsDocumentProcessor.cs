using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

/// <summary>
/// MLB-specific processor for EventCompetitionOdds documents.
///
/// Why a separate processor: ESPN's MLB odds endpoint returns a paged
/// collection wrapper (<see cref="EspnEventCompetitionOddsListDto"/>) where
/// each item is a per-provider odds row that lacks its own <c>$ref</c> and
/// has no top-level <c>id</c>. The Provider's generic resource-index path
/// can't extract those items individually (see EspnResourceIndexClassifier
/// for the sport-aware leaf override that routes the whole wrapper here),
/// so this processor receives the entire document and splits it.
///
/// NCAAFB and NFL stay on <see cref="EventCompetitionOddsDocumentProcessor{TDataContext}"/>
/// — their odds wrappers carry per-item <c>$ref</c>s that the generic path
/// follows successfully, so each per-provider document arrives as a single
/// odds object the way that processor expects.
///
/// Identity per item: synthesized as <c>{listingUri}#provider={item.provider.id}</c>.
/// The fragment is never sent over the wire — it's only used as a deterministic
/// hash input so each provider's row gets a stable canonical id.
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.EventCompetitionOdds)]
public class BaseballEventCompetitionOddsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    private readonly IJsonHashCalculator _jsonHash;

    public BaseballEventCompetitionOddsDocumentProcessor(
        ILogger<BaseballEventCompetitionOddsDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        IJsonHashCalculator jsonHash)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
        _jsonHash = jsonHash;
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        // --- Deserialize wrapper ---
        var wrapper = command.Document.FromJson<EspnEventCompetitionOddsListDto>();
        if (wrapper?.Items is null || wrapper.Items.Count == 0)
        {
            _logger.LogWarning(
                "MLB odds document had no items. UrlHash={UrlHash}",
                command.UrlHash);
            return;
        }

        // ESPN paginates at 25 items per page. 25+ sportsbooks per game would
        // be unusual; if/when it happens, the streamer needs to fetch
        // additional pages and feed them as separate documents. Flag for now.
        if (wrapper.PageCount is > 1)
        {
            _logger.LogWarning(
                "MLB odds document is paginated; only page {PageIndex} is processed. UrlHash={UrlHash}, PageCount={PageCount}",
                wrapper.PageIndex, command.UrlHash, wrapper.PageCount);
        }

        // --- Resolve competition ---
        var competitionId = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.CompetitionOddsRefToCompetitionRef);

        if (competitionId is null)
        {
            _logger.LogError("Unable to determine CompetitionId from ParentId or URI");
            return;
        }

        if (!command.SeasonYear.HasValue)
        {
            _logger.LogError("Command missing SeasonYear.");
            return;
        }

        var competition = await _dataContext.Competitions
            .AsNoTracking()
            .Include(x => x.Contest)
            .FirstOrDefaultAsync(x => x.Id == competitionId.Value);

        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionId.Value);
            throw new ArgumentException("competition not found");
        }

        // --- Hash + skip-when-unchanged ---
        // One content hash for the entire wrapper. ESPN updates all providers
        // together, so per-provider hashing would buy little granularity at
        // the cost of N reserialization passes. Each persisted row carries
        // the wrapper hash; if any existing row already has it, we know the
        // wrapper hasn't changed since we last saved it.
        var wrapperContentHash = _jsonHash.NormalizeAndHash(command.Document);

        var existing = await _dataContext.CompetitionOdds
            .Include(o => o.ExternalIds)
            .Include(o => o.Teams)
            .Include(o => o.Links)
            .AsSplitQuery()
            .Where(o => o.CompetitionId == competition.Id)
            .ToListAsync();

        if (existing.Any(o => string.Equals(o.ContentHash, wrapperContentHash, StringComparison.Ordinal)))
        {
            _logger.LogInformation(
                "No MLB odds changes detected, skipping. CompetitionId={CompId}",
                competition.Id);
            return;
        }

        var hadExisting = existing.Count > 0;
        if (hadExisting)
        {
            _logger.LogInformation(
                "Replacing MLB odds for Competition. CompetitionId={CompId}, ExistingCount={ExistingCount}",
                competition.Id, existing.Count);

            // Cascade delete on entity config removes Teams, Links, ExternalIds.
            _dataContext.CompetitionOdds.RemoveRange(existing);
        }
        else
        {
            _logger.LogInformation(
                "Creating MLB odds for Competition. CompetitionId={CompId}",
                competition.Id);
        }

        // --- Synthesize per-item identity + persist ---
        // Listing URL serves as the identity base; the fragment makes each
        // provider's identity unique and deterministic. ESPN never sees this
        // synthesized URL — it only feeds the hash function.
        var listingUriBase = command.SourceUri.ToString();

        var addedAny = false;
        foreach (var item in wrapper.Items)
        {
            if (item.Provider?.Id is null)
            {
                _logger.LogWarning(
                    "MLB odds item missing provider.id, skipping item. CompetitionId={CompId}",
                    competition.Id);
                continue;
            }

            // If the listing URL already has a fragment (vanishingly unlikely
            // but defensive), extend it with `&provider=...` rather than
            // double-fragmenting.
            var separator = listingUriBase.Contains('#') ? '&' : '#';
            var syntheticRef = new Uri($"{listingUriBase}{separator}provider={item.Provider.Id}");
            item.Ref = syntheticRef;

            var entity = item.AsEntity(
                externalRefIdentityGenerator: _externalRefIdentityGenerator,
                competitionId: competition.Id,
                homeFranchiseSeasonId: competition.Contest.HomeTeamFranchiseSeasonId,
                awayFranchiseSeasonId: competition.Contest.AwayTeamFranchiseSeasonId,
                correlationId: command.CorrelationId,
                contentHash: wrapperContentHash);

            await _dataContext.CompetitionOdds.AddAsync(entity);
            addedAny = true;

            _logger.LogInformation(
                "Persisted MLB odds row. CompetitionId={CompId}, Provider={Prov}, OddsId={OddsId}",
                competition.Id, item.Provider.Id, entity.Id);
        }

        if (!addedAny)
        {
            _logger.LogWarning(
                "MLB odds wrapper produced no valid items to persist. CompetitionId={CompId}",
                competition.Id);
            return;
        }

        // --- Publish event ---
        // One event per wrapper-document arrival (analogous to one event per
        // provider-document arrival on NCAAFB/NFL). Subscribers care that the
        // contest's odds changed; per-provider granularity isn't useful yet.
        if (hadExisting)
        {
            await _publishEndpoint.Publish(new ContestOddsUpdated(
                competition.Contest.Id,
                "ContestOddsUpdated",
                null,
                command.Sport,
                command.SeasonYear,
                command.CorrelationId,
                CausationId.Producer.EventDocumentProcessor));
        }
        else
        {
            await _publishEndpoint.Publish(new ContestOddsCreated(
                competition.Contest.Id,
                null,
                command.Sport,
                command.SeasonYear,
                command.CorrelationId,
                command.MessageId));
        }

        await _dataContext.SaveChangesAsync();
    }
}
