# Refresh Contest — narrowing the document-sourcing cascade

Captured 2026-05-06 from a debugging session triggered by "Refresh Contest"
on the Contest Overview page sourcing far more than intended (entire
roster + per-athlete stats per team, twice). Persisted here in case the
session context is lost before the change is implemented.

## Problem

User clicks "Refresh Contest" on the Contest Overview page. API calls
Producer's `ContestUpdateProcessor.Process()`, which publishes a single
`DocumentRequested` for `DocumentType.Event`. The intent is *contest-level
live state* (status, score, broadcast, odds) — not roster, not athletes,
not per-athlete stats.

What actually happens: the `Event` doc is sourced, downstream processors
spawn `EventCompetition`, which spawns two `EventCompetitionCompetitor`
docs, each of which spawns `EventCompetitionCompetitorRoster`
(`EventCompetitionCompetitorDocumentProcessor.cs:203`), each roster
fans out into per-athlete docs, each athlete fans out further. Hundreds
of ESPN fetches for a refresh that should have been a few dozen.

This is not the same issue as PR #294 (the fan-out `BypassCache=true`
fix) or the deferred "in-season but completed games bypass cache"
concern (`memory/project_in_season_completed_cache.md`). Both of those
are about *whether* to hit ESPN. This is about *which documents* to
source at all.

## The mechanism that's already in place

`DocumentRequested` carries a nullable
`IReadOnlyCollection<DocumentType>? IncludeLinkedDocumentTypes` field.
`DocumentCreated` carries the same field. `ProcessDocumentCommand`
holds it. `DocumentProcessorBase.ShouldSpawn(documentType, command)`
consults it:

```csharp
protected bool ShouldSpawn(DocumentType documentType, ProcessDocumentCommand command)
{
    // null/empty → spawn all (current default behavior)
    if (command.IncludeLinkedDocumentTypes == null || command.IncludeLinkedDocumentTypes.Count == 0)
        return true;

    return command.IncludeLinkedDocumentTypes.Contains(documentType);
}
```

Every child-spawn site in every processor is wrapped in
`if (isNew || ShouldSpawn(DocumentType.X, command))`, so the filter
*could* narrow the cascade if it were populated.

## The propagation gap

Tracing the chain hop by hop for a `Refresh Contest` flow:

1. **API → Producer.** `ContestUpdateProcessor.cs:90` builds the seed
   `DocumentRequested` and does **not** set
   `IncludeLinkedDocumentTypes`. It defaults to `null` →
   `ShouldSpawn` returns `true` for everything downstream.
2. **Producer → Provider.** `IEventBus.Publish(evt)` carries
   `IncludeLinkedDocumentTypes` on the wire. Whatever Producer puts on
   it survives.
3. **Provider receive.**
   `DocumentRequestedHandler.cs:211` (leaf path) and
   `DocumentRequestedHandler.cs:360` (fan-out path) both correctly
   propagate `evt.IncludeLinkedDocumentTypes` onto the
   `ProcessResourceIndexItemCommand`.
4. **Provider publishes `DocumentCreated`.**
   `ResourceIndexItemProcessor` lines 363, 419, 461 (the three
   `DocumentCreated` construction sites) all include
   `command.IncludeLinkedDocumentTypes`. Survives.
5. **Producer consumes `DocumentCreated`.**
   `DocumentCreatedProcessor.cs:118` puts `evt.IncludeLinkedDocumentTypes`
   onto the `ProcessDocumentCommand` that hits the document
   processor. Survives.
6. **Producer's processor decides whether to spawn children.**
   Calls `ShouldSpawn(DocumentType.X, command)`. Filter respected. Good.
7. **Producer's processor publishes a new `DocumentRequested` for a
   child.** Goes through
   `DocumentProcessorBase.PublishDocumentRequestInternal` (lines
   358–369). **This is where the chain breaks.**

```csharp
// DocumentProcessorBase.cs:358–369 (current)
await _publishEndpoint.Publish(new DocumentRequested(
    Id: identity.CanonicalId.ToString(),
    ParentId: parentId?.ToString() ?? null,
    Uri: uri,
    Ref: null,
    Sport: command.Sport,
    SeasonYear: command.SeasonYear,
    DocumentType: documentType,
    SourceDataProvider: command.SourceDataProvider,
    CorrelationId: command.CorrelationId,
    CausationId: command.MessageId
    // IncludeLinkedDocumentTypes is NOT set — the field defaults to null.
));
```

Even when a parent processor *did* receive a non-null
`IncludeLinkedDocumentTypes` and *did* respect it locally via
`ShouldSpawn`, the new child `DocumentRequested` is born with `null`.
At the next processor in the cascade, `ShouldSpawn` defaults back to
"spawn everything." The filter dies at the first hop.

## The fix — two changes

### Change 1: propagate the filter through `PublishDocumentRequestInternal`

`src/SportsData.Producer/Application/Documents/Processors/DocumentProcessorBase.cs`,
inside `PublishDocumentRequestInternal`. Add one argument to the
`DocumentRequested` constructor call:

```csharp
await _publishEndpoint.Publish(new DocumentRequested(
    Id: identity.CanonicalId.ToString(),
    ParentId: parentId?.ToString() ?? null,
    Uri: uri,
    Ref: null,
    Sport: command.Sport,
    SeasonYear: command.SeasonYear,
    DocumentType: documentType,
    SourceDataProvider: command.SourceDataProvider,
    CorrelationId: command.CorrelationId,
    CausationId: command.MessageId,
    IncludeLinkedDocumentTypes: command.IncludeLinkedDocumentTypes // ← new
));
```

That single line is what's needed for the existing machinery to start
working end-to-end.

### Change 2: seed the filter at `ContestUpdateProcessor`

`src/SportsData.Producer/Application/Contests/ContestUpdateProcessor.cs:90`.
Replace the seed publish with one that carries a narrow filter:

```csharp
private static readonly IReadOnlyCollection<DocumentType> ContestRefreshDocumentTypes = new[]
{
    DocumentType.Event,
    DocumentType.EventCompetition,
    DocumentType.EventCompetitionStatus,
    DocumentType.EventCompetitionSituation,
    DocumentType.EventCompetitionBroadcast,
    DocumentType.EventCompetitionOdds,
    DocumentType.EventCompetitionCompetitor,
    DocumentType.EventCompetitionCompetitorScore,
    DocumentType.EventCompetitionCompetitorLineScore,
    DocumentType.EventCompetitionCompetitorRecord,
    DocumentType.EventCompetitionPlay,    // live-game tick
    DocumentType.EventCompetitionDrive    // football only; harmless for MLB
};

var evt = new DocumentRequested(
    Id: contestIdentity.UrlHash,
    ParentId: null,
    Uri: new Uri(contestIdentity.CleanUrl),
    Ref: null,
    Sport: command.Sport,
    SeasonYear: contest.SeasonYear,
    DocumentType: DocumentType.Event,
    SourceDataProvider: command.SourceDataProvider,
    CorrelationId: command.CorrelationId,
    CausationId: CausationId.Producer.ContestUpdateProcessor,
    IncludeLinkedDocumentTypes: ContestRefreshDocumentTypes.ToList()
);
```

The exact set is a product decision — list above is a reasonable
starting point, but worth confirming before shipping.

### What gets *excluded* (and why this is the win)

Anything not in the list gets skipped at every spawn site:

- `EventCompetitionCompetitorRoster` — the leaf that's currently
  triggering the cascade. Roster is sourced as part of the
  initial-sourcing path; it shouldn't change during a re-finalize.
- `EventCompetitionCompetitorStatistics` — same: per-team stats,
  derived elsewhere.
- All `Athlete*` docs the roster spawn fans into.

## Caveat — the `isNew` short-circuit

Every spawn site looks like:

```csharp
if (isNew || ShouldSpawn(DocumentType.X, command))
    await PublishChildDocumentRequest(...);
```

The `isNew` guard means: "if this is the first time we're seeing this
entity, spawn everything regardless of filter." That's intentional
and defensible — a brand-new entity needs full hydration. For a
"Refresh Contest" flow on a contest that already exists, `isNew=false`
on every entity in the cascade, so the filter is respected.

If a *sub*-entity happens to be new (rare on re-finalize, but possible
if e.g. a new EventCompetitionPlay arrives mid-game), the short-circuit
fires and the filter is bypassed for that sub-entity's children. This
isn't a bug — it's the documented behavior — but it's worth knowing
the filter isn't absolute.

## Implemented behavior

As shipped:

1. `DocumentProcessorBase.PublishDocumentRequestInternal` now sets
   `IncludeLinkedDocumentTypes: command.IncludeLinkedDocumentTypes?.ToList()`
   on the published `DocumentRequested`. Both `PublishDependencyRequest`
   and `PublishChildDocumentRequest` route through this shared helper,
   so the filter survives every cascade hop on both paths.
2. `ContestUpdateProcessor` defines a private
   `ContestRefreshDocumentTypes` static list and passes it via
   `IncludeLinkedDocumentTypes` on the seed `DocumentRequested` it
   publishes for `DocumentType.Event`. Membership: `Event`,
   `EventCompetition`, `EventCompetitionStatus`,
   `EventCompetitionSituation`, `EventCompetitionBroadcast`,
   `EventCompetitionOdds`, `EventCompetitionCompetitor`,
   `EventCompetitionCompetitorScore`,
   `EventCompetitionCompetitorLineScore`,
   `EventCompetitionCompetitorRecord`, `EventCompetitionPlay`,
   `EventCompetitionDrive`.

## Test coverage

Verified by `DocumentProcessorBaseTests` in
`test/unit/SportsData.Producer.Tests.Unit/Application/Documents/Processors/`:

- `PublishDependencyRequest_Should_Propagate_IncludeLinkedDocumentTypes_To_Child_Request`
  — asserts a non-null filter on the parent command appears verbatim
  on the published `DocumentRequested`.
- `PublishDependencyRequest_Should_Publish_Null_Filter_When_Command_Has_No_Filter`
  — guards the null-filter contract so a missing filter never
  serializes as an empty list (which would flip `ShouldSpawn`'s
  spawn-all default).

Both tests exercise `PublishDocumentRequestInternal` through the
exposed `PublishDependencyRequestPublic` test seam; since
`PublishChildDocumentRequest` also routes through
`PublishDocumentRequestInternal`, the same propagation contract is
covered for the child path.

## Production validation

Trigger Refresh Contest from the UI, then in Seq filter on the
resulting CorrelationId. Expect to see exclusively the document types
in `ContestRefreshDocumentTypes`. No `EventCompetitionCompetitorRoster`
events. No `Athlete*`.

## Related

- PR #294 — fan-out `BypassCache` fix and `(Cache)` /
  `(EspnUnchanged)` log disambiguation. Different concern (whether to
  hit ESPN), but related symptoms (excess ESPN volume on refresh).
- `memory/project_in_season_completed_cache.md` — deferred policy
  question about treating completed current-season games as
  cache-eligible. Orthogonal to this work.
