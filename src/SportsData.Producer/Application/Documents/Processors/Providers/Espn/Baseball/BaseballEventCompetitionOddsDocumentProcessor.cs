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

        // Contract violation, not a transient issue — the URI doesn't map to
        // a Competition we can derive. Retrying won't help. Log + return so
        // the message is acked and the DLQ stays clean.
        if (competitionId is null)
        {
            _logger.LogError("Unable to determine CompetitionId from ParentId or URI");
            return;
        }

        // Same — message envelope is missing a required field. Retrying the
        // same malformed message produces the same result.
        if (!command.SeasonYear.HasValue)
        {
            _logger.LogError("Command missing SeasonYear.");
            return;
        }

        var competition = await _dataContext.Competitions
            .AsNoTracking()
            .Include(x => x.Contest)
            .FirstOrDefaultAsync(x => x.Id == competitionId.Value);

        // Transient — the parent Competition document is likely still
        // being processed upstream. Throw so Hangfire's AutomaticRetryAttribute
        // re-runs us with backoff (eventual consistency). InvalidOperationException
        // is a better fit than ArgumentException here: nothing about the
        // arguments is invalid, the expected entity simply doesn't exist yet.
        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionId.Value);
            throw new InvalidOperationException(
                $"Competition {competitionId.Value} not found; will retry.");
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

        // --- Synthesize per-item identity + persist ---
        // Listing URL serves as the identity base; appending /provider/{id}
        // to the *path* makes each provider's identity unique and stable.
        // The provider id MUST live in the path (not query/fragment) because
        // IGenerateExternalRefIdentities.Generate hashes against the
        // ToCleanUrl normalization, which strips both — so query- or
        // fragment-based synthesis would collapse every item onto the
        // same canonical id and clobber rows on persist. ESPN never sees
        // this synthesized URL; it's only fed to the hash function.
        var listingUri = command.SourceUri;

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

            // UriBuilder preserves scheme/host/port/query; we extend the path
            // with a /provider/{id} segment that's clearly synthetic (won't
            // be mistaken for a fetchable ESPN URL during debugging).
            var refBuilder = new UriBuilder(listingUri)
            {
                Path = listingUri.AbsolutePath.TrimEnd('/')
                       + "/provider/"
                       + Uri.EscapeDataString(item.Provider.Id)
            };
            var syntheticRef = refBuilder.Uri;
            item.Ref = syntheticRef;

            // Defensive guard — Competition.Contest is loaded via .Include
            // above and the FK should be non-nullable, but a missing parent
            // would NRE on the franchise-season accessors below. Throw with
            // context so the operator can investigate (data integrity issue,
            // or a race where the Contest hasn't been persisted yet —
            // Hangfire's retry policy will pick it up).
            if (competition.Contest is null)
            {
                _logger.LogError(
                    "Competition.Contest is null; cannot resolve franchise-season ids. CompetitionId={CompId}, CorrelationId={CorrelationId}",
                    competition.Id, command.CorrelationId);
                throw new InvalidOperationException(
                    $"Competition {competition.Id} has no Contest loaded; cannot persist odds. CorrelationId={command.CorrelationId}");
            }

            var entity = item.AsEntity(
                externalRefIdentityGenerator: _externalRefIdentityGenerator,
                competitionId: competition.Id,
                homeFranchiseSeasonId: competition.Contest.HomeTeamFranchiseSeasonId,
                awayFranchiseSeasonId: competition.Contest.AwayTeamFranchiseSeasonId,
                correlationId: command.CorrelationId,
                contentHash: wrapperContentHash);

            // Defer the existing-row removal until we know at least one item
            // is going to be staged. If every wrapper item is malformed (e.g.
            // missing provider.id and the loop continues out), RemoveRange
            // would still mark the prior rows Deleted in the change tracker
            // and a downstream SaveChanges would wipe valid data with no
            // replacement. Cascade delete on the entity config removes Teams,
            // Links, and ExternalIds along with each odds row.
            if (!addedAny)
            {
                if (hadExisting)
                {
                    _logger.LogInformation(
                        "Replacing MLB odds for Competition. CompetitionId={CompId}, ExistingCount={ExistingCount}",
                        competition.Id, existing.Count);
                    _dataContext.CompetitionOdds.RemoveRange(existing);
                }
                else
                {
                    _logger.LogInformation(
                        "Creating MLB odds for Competition. CompetitionId={CompId}",
                        competition.Id);
                }
            }

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
