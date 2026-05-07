# Processor `ShouldSpawn` audit — closing the cascade-filter holes

Captured 2026-05-07 after a sourcing run produced an unexpectedly large
fan-out overnight. PR #296 wired `IncludeLinkedDocumentTypes` propagation
through `DocumentProcessorBase.PublishDocumentRequestInternal` and added
the seed filter on the Refresh-Contest path
(`docs/refresh-contest-cascade-narrowing.md`). That fix relies on every
child-spawn site in every processor honoring `ShouldSpawn`. This audit
verifies that contract and lists the holes that need to close.

## The established pattern — two helpers, two contracts

`DocumentProcessorBase` exposes two distinct publish helpers
(`DocumentProcessorBase.cs:225` and `:288`). They look similar but
behave differently, and several violations in this audit are the result
of using the wrong one.

### `PublishDependencyRequest` — pre-success, dedup-tracked

A *dependency* is something the current processor cannot proceed
without. The contract is: publish a request for the missing document,
then throw `ExternalDocumentNotSourcedException` to defer this attempt
to retry. The base `ProcessAsync` catch (lines 80–99) handles the
republish.

Critically, dependency requests are **deduped across retries**:

- `PublishDependencyRequest` records each request in
  `command.RequestedDependencies` (a `HashSet<RequestedDependency>`
  keyed by `(DocumentType, UrlHash)`) at lines 257–274.
- `ToDocumentCreated` (`ProcessDocumentCommandExtensions.cs:25`)
  serializes that hash set onto the next-attempt `DocumentCreated`.
- `DocumentCreatedProcessor` (`DocumentCreatedProcessor.cs:121–124`)
  rehydrates it onto the new `ProcessDocumentCommand`.

Net effect: if an attempt requests dependency X and throws to retry,
the next attempt sees X in `RequestedDependencies` and skips the
republish. Every subsequent retry is silent on that dependency until
it finally lands and processing succeeds.

### `PublishChildDocumentRequest` — post-success, no dedup

A *child* is something this document spawns *after* it processes
successfully (e.g. an Event spawning EventCompetition). The contract
explicitly says (xmldoc at `:281`): "Publishes on every attempt since
child spawning only happens when processing succeeds past
dependencies." There is no dedup because successful processing only
happens once.

### `ShouldSpawn` guard — child-only

Every child-spawn site must be wrapped:

```csharp
if (isNew || ShouldSpawn(DocumentType.X, command))
{
    await PublishChildDocumentRequest(command, dto.X, parentId, DocumentType.X);
}
```

`isNew` is the documented short-circuit — a brand-new entity always
hydrates fully. `ShouldSpawn` (`:136`) consults
`command.IncludeLinkedDocumentTypes`; a null/empty filter spawns
everything (current default), a populated filter narrows the cascade.

`PublishDependencyRequest` is intentionally *not* gated on
`ShouldSpawn`: a missing dependency is a hard precondition, not an
optional fan-out. Filtering it out would silently break the processor
that needs it.

### Filter propagation

Both helpers route through `PublishDocumentRequestInternal` (`:326`),
which propagates `command.IncludeLinkedDocumentTypes` onto the next-hop
`DocumentRequested` (line 375). The filter survives every cascade hop —
*if* the call site uses one of the two helpers. Direct calls to
`_publishEndpoint.Publish(new DocumentRequested(...))` bypass it.

`TeamSeasonDocumentProcessor` is the gold-standard exemplar for child
spawns: 11 sites, all wrapped. Lines 175, 181, 191, 201, 211 (and 6
more) all use the `isNew || ShouldSpawn(DocumentType.X, command)` shape.

## Audit results

Two parallel agents covered the full processor surface (~47 classes).
Pattern adherence is mixed: every processor overrides `ProcessInternal`
(no `ProcessAsync` legacy holdouts), but ten distinct call sites across
nine processors skip the `ShouldSpawn` guard.

### EventCompetition family

| File:line | DocumentType | Issue | Severity |
|---|---|---|---|
| `Espn/Common/EventCompetitionCompetitorRosterDocumentProcessor.cs:181` | `EventCompetitionAthleteStatistics` | Child spawn missing `ShouldSpawn` guard | **Critical** — per-roster-entry loop (50–100 athletes per game) |
| `Espn/Football/EventCompetitionDriveDocumentProcessor.cs:199` | `EventCompetitionPlay` | Raw `_publishEndpoint.Publish(new DocumentRequested(...))` — bypasses both helpers (no dedup, no filter propagation, no guard) | **Critical / structural** |
| `Espn/Football/EventCompetitionSituationDocumentProcessor.cs:70` | `EventCompetitionPlay` | Wrong helper — uses `PublishChildDocumentRequest` then throws for retry. Should be `PublishDependencyRequest` so retries are deduped | Low (one play per retry today; switching to dependency makes it zero after first retry) |
| `Espn/Baseball/BaseballEventCompetitionSituationDocumentProcessor.cs:66` | `EventCompetitionPlay` | Sibling duplicate of the football site — same wrong-helper issue | Low |
| `Espn/Common/EventCompetitionPowerIndexDocumentProcessor.cs:94` | `TeamSeason` | Wrong helper — same shape as Situation processors | Low |

### Non-EventCompetition

| File:line | DocumentType | Severity | Note |
|---|---|---|---|
| `Espn/Common/EventDocumentProcessorBase.cs:253` | `EventCompetition` | Medium | Root of the contest cascade. `EventCompetition` is in `ContestRefreshDocumentTypes`, so PR #296 isn't broken in practice — but the pattern violation matters for future filter sets |
| `Espn/Common/SeasonTypeDocumentProcessor.cs:103` | `GroupSeason` | High | Fires after the new-vs-update branches, so unconditional |
| `Espn/Common/SeasonTypeDocumentProcessor.cs:110` | `SeasonTypeWeek` | High | Same |
| `Espn/Common/SeasonDocumentProcessor.cs:107` | `SeasonType` | Medium | New-entity path; `isNew` short-circuit applies but the explicit guard is missing |
| `Espn/Common/SeasonDocumentProcessor.cs:136` | `SeasonFuture` | Medium | Same |
| `Espn/Common/GroupSeasonDocumentProcessor.cs:159` | `GroupSeason` | Medium | Update path |
| `Espn/Common/SeasonPollDocumentProcessor.cs:98` | `SeasonTypeWeekRankings` | Medium | Per-rank loop |
| `Espn/Common/SeasonTypeWeekRankingsDocumentProcessor.cs:147` | `TeamSeason` (dependency) | Low | Dependency-fallback loop on missing `FranchiseSeason` |

Exemplar processors (all spawn sites guarded):
`TeamSeasonDocumentProcessor`, `CoachDocumentProcessor`,
`FootballAthleteDocumentProcessor` (update path),
`EventCompetitionDocumentProcessorBase` (9 sites),
`EventCompetitionCompetitorDocumentProcessor` (5 sites),
`EventCompetitionLeadersDocumentProcessor` (2 sites).

## What this audit does *not* explain

None of the audit findings are a smoking gun for the MLB fan-out
observed overnight. Walking each candidate against the actual cascade:

- **Roster `:181` (AthleteStatistics spawn)** — the parent
  `EventCompetitionCompetitorDocumentProcessor:201` already guards
  Roster on `isNew || ShouldSpawn(EventCompetitionCompetitorRoster, ...)`,
  and `EventCompetitionCompetitorRoster` is *not* in
  `ContestRefreshDocumentTypes`. On a Refresh Contest path against an
  already-hydrated contest (`isNew=false`), Roster is suppressed at
  the parent — the Roster processor never runs and its unguarded
  internal spawn is moot. On a non-Refresh path with no filter,
  `ShouldSpawn` returns true regardless of the guard, so adding it
  changes no live behavior.
- **Drive `:199`** — football-only. Cannot apply to MLB.
- **Situation, PowerIndex** — single-doc dependency requests. Not
  fan-outs. Wrong-helper bugs that cause retry-time republishes, but
  not bulk amplification.
- **Season-tier sites** — fire once per season, not per game.

The audit findings are still real pattern violations worth closing —
the test seam alone is worth the work, since it stops new violations
from landing. But the audit does not, by itself, identify what
amplified MLB sourcing overnight.

Plausible alternative explanations to investigate in Seq before
implementing fixes:

1. **MLB pods aren't actually paused.** Config drift, KEDA not
   honoring `paused: "true"`, or replicas reverted via a different
   manifest.
2. **A trigger path that doesn't carry the filter.** Something other
   than `ContestUpdateProcessor` (e.g. a saga step, a scheduled job,
   a re-finalize endpoint) emitted seed `DocumentRequested` events
   for MLB without setting `IncludeLinkedDocumentTypes`, so the full
   cascade spawned normally. That's the expected behavior for an
   un-narrowed sourcing pass — the question is *who triggered it*.
3. **A bug elsewhere in the propagation chain** that drops
   `IncludeLinkedDocumentTypes` between Producer and Provider on the
   round trip. Shouldn't be possible per PR #296 and the propagation
   tests, but worth verifying with a real trace.
4. **`isNew=true` cascades** — if competitions were being hydrated
   for the first time (not re-finalized), the `isNew` short-circuit
   would bypass the Refresh Contest filter. Documented behavior, but
   surprising at scale.

Concretely: pull the MLB-Producer Seq logs from last night, group by
`CorrelationId`, and look at the seed `DocumentRequested` for each
cascade — what `DocumentType` it was, whether `IncludeLinkedDocumentTypes`
was populated, and where it came from (`CausationId` /
`SourceContext`). That tells us which trigger fired and whether the
filter was set.

## Fix per site

### 1. `EventCompetitionCompetitorRosterDocumentProcessor.cs:181`

Current (`:175–188`):

```csharp
if (entry.Statistics?.Ref is not null)
{
    _logger.LogDebug("Publishing child request for athlete statistics. ...", ...);

    await PublishChildDocumentRequest<string?>(
        command,
        entry.Statistics,
        null,
        DocumentType.EventCompetitionAthleteStatistics);

    publishedStatsCount++;
}
```

Wrap with the guard (note: this site is inside a per-entry loop creating
new roster entries, so `isNew` is effectively always true here — the
guard is still worth adding for filter alignment):

```csharp
if (entry.Statistics?.Ref is not null
    && ShouldSpawn(DocumentType.EventCompetitionAthleteStatistics, command))
{
    ...
}
```

Drop `isNew ||` here because the site is *only* reached for newly-added
roster entries — the loop is constructing them. `ShouldSpawn` alone is
the right guard; if the cascade filter excludes athlete stats, we
honor it even on a brand-new roster entry.

### 2. `EventCompetitionDriveDocumentProcessor.cs:199` (structural)

Current (`:184–219`):

```csharp
foreach (var play in externalDto.Plays.Items)
{
    var playIdentity = _externalRefIdentityGenerator.Generate(play.Ref);

    var playEntity = await _dataContext.CompetitionPlays
        .FirstOrDefaultAsync(x => x.Id == playIdentity.CanonicalId);

    if (playEntity != null)
    {
        playEntity.DriveId = drive.Id;
        linkedCount++;
    }
    else
    {
        await _publishEndpoint.Publish(new DocumentRequested(
            Id: playIdentity.UrlHash,
            ParentId: competitionId.ToString(),
            Uri: new Uri(playIdentity.CleanUrl),
            ...
            PropertyBag: new Dictionary<string, string>()
            {
                { "CompetitionDriveId", drive.Id.ToString()}
            }
        ));
        requestedCount++;
    }

    await _dataContext.SaveChangesAsync();
}
```

This site has two problems: (a) no `ShouldSpawn` guard, and (b) raw
`_publishEndpoint.Publish` instead of `PublishChildDocumentRequest`,
which means the `IncludeLinkedDocumentTypes` propagation helper is
skipped entirely — even if the filter were checked at this site, the
emitted `DocumentRequested` wouldn't carry the parent's filter forward.

The complication is the `PropertyBag` — `PublishChildDocumentRequest`
doesn't expose a `PropertyBag` parameter today. Two options:

- **Option A** — extend `PublishChildDocumentRequest` (and
  `PublishDocumentRequestInternal`) with an optional
  `IDictionary<string, string>? propertyBag` parameter. One-line plumbing
  change in `DocumentProcessorBase`; one-line call-site change in the
  Drive processor. Preserves the helper-routing invariant.
- **Option B** — keep the raw publish but explicitly add `ShouldSpawn`
  guard and `IncludeLinkedDocumentTypes: command.IncludeLinkedDocumentTypes?.ToList()`.
  Smaller diff, but leaves a one-off path that bypasses the helper —
  future processors might copy the pattern.

Option A is the right answer; it keeps the helper as the single funnel
and means we don't have to remember to hand-propagate the filter at any
new raw-publish site.

### 3 & 4. Situation processors (Football and Baseball) — wrong helper

Both look identical:

```csharp
if (lastPlay == null)
{
    await PublishChildDocumentRequest(  // ← wrong helper
        command,
        dto.LastPlay,
        competitionIdValue,
        DocumentType.EventCompetitionPlay);

    throw new ExternalDocumentNotSourcedException(...);
}
```

This is semantically a *dependency* — the processor cannot finish
without `lastPlay`, so it publishes the request and throws for retry.
But it's calling `PublishChildDocumentRequest`, which has no
`RequestedDependencies` tracking. Every retry attempt re-publishes the
same single Play request.

Switch to `PublishDependencyRequest`:

```csharp
if (lastPlay == null)
{
    await PublishDependencyRequest(
        command,
        dto.LastPlay,
        competitionIdValue,
        DocumentType.EventCompetitionPlay);

    throw new ExternalDocumentNotSourcedException(
        $"Last Play {dto.LastPlay.Ref} not found. Requesting. Will retry.");
}
```

This gets us three things for free:
1. **Retry dedup** — first attempt publishes; subsequent retries see
   the entry in `RequestedDependencies` and silently skip the
   republish.
2. **Correct semantics** — Play here is a hard precondition, not an
   optional fan-out. `PublishDependencyRequest` is the contract for
   "must have this to proceed."
3. **Filter propagation** — both helpers route through
   `PublishDocumentRequestInternal`, so `IncludeLinkedDocumentTypes`
   still flows.

No `ShouldSpawn` guard is needed — dependency requests are
intentionally ungated.

### 5. `EventCompetitionPowerIndexDocumentProcessor.cs:94` — wrong helper

Same shape as the Situation processors. Switch
`PublishChildDocumentRequest` → `PublishDependencyRequest`:

```csharp
if (franchiseSeasonId is null)
{
    if (dto.Team?.Ref is null)
    {
        _logger.LogWarning("Team reference is null for power index. Skipping.");
        return;
    }

    await PublishDependencyRequest(
        command,
        dto.Team,
        parentId: string.Empty,
        DocumentType.TeamSeason);

    await _dataContext.SaveChangesAsync();

    throw new ExternalDocumentNotSourcedException("FranchiseSeason not found. Sourcing requested. Will retry.");
}
```

### 6. `EventDocumentProcessorBase.cs:253`

Current (`:248–258`):

```csharp
foreach (var competition in externalDto.Competitions)
{
    _logger.LogDebug("Publishing DocumentRequested for EventCompetition. ...");

    await PublishChildDocumentRequest(
        command,
        competition,
        contest.Id,
        DocumentType.EventCompetition);
}
```

Add the standard guard:

```csharp
foreach (var competition in externalDto.Competitions)
{
    if (isNew || ShouldSpawn(DocumentType.EventCompetition, command))
    {
        await PublishChildDocumentRequest(...);
    }
}
```

`isNew` flows in as a parameter on `ProcessCompetitions` — the caller
will need to pass it. (`EventCompetition` is in
`ContestRefreshDocumentTypes`, so this guard is preventative, not
load-shedding — it future-proofs against new filter callers.)

### 7–8. `SeasonTypeDocumentProcessor.cs:103, 110`

Both calls live after the new/update branches and fire on every
processing call. Wrap each:

```csharp
if (isNew || ShouldSpawn(DocumentType.GroupSeason, command))
{
    await PublishChildDocumentRequest(command, dto.Groups, seasonPhase.Id, DocumentType.GroupSeason);
}

if (isNew || ShouldSpawn(DocumentType.SeasonTypeWeek, command))
{
    await PublishChildDocumentRequest(command, dto.Weeks, seasonPhase.Id, DocumentType.SeasonTypeWeek);
}
```

### 9–10. `SeasonDocumentProcessor.cs:107, 136`

Inside the new-season-creation branch. Add the standard guard so a
narrow filter is still honored even on initial sourcing:

```csharp
if (dto.Types?.Ref is not null && (isNew || ShouldSpawn(DocumentType.SeasonType, command)))
{
    await PublishChildDocumentRequest(...);
    publishEvents = true;
}

if (dto.Futures?.Ref is not null && (isNew || ShouldSpawn(DocumentType.SeasonFuture, command)))
{
    await PublishChildDocumentRequest(...);
    publishEvents = true;
}
```

### 11. `GroupSeasonDocumentProcessor.cs:159`

```csharp
if (dto.Children?.Ref is not null
    && (isNew || ShouldSpawn(DocumentType.GroupSeason, command)))
{
    await PublishChildDocumentRequest(...);
}
```

### 12. `SeasonPollDocumentProcessor.cs:98`

```csharp
foreach (var rank in dto.Rankings)
{
    if (isNew || ShouldSpawn(DocumentType.SeasonTypeWeekRankings, command))
    {
        await PublishChildDocumentRequest(command, rank, dtoIdentity.CanonicalId, DocumentType.SeasonTypeWeekRankings);
    }
}
```

(Or hoist the guard outside the loop for readability since the filter
decision is loop-invariant.)

### 13. `SeasonTypeWeekRankingsDocumentProcessor.cs:147`

This is a `PublishDependencyRequest` (not `PublishChildDocumentRequest`).
Dependency requests are intended to fail-safe on missing parents — the
processor immediately throws after the request, expecting Hangfire
retry once the dependency lands. Guarding here is a judgment call:

- If the filter excludes `TeamSeason`, sourcing a missing
  `FranchiseSeason` still requires the underlying `TeamSeason` doc.
  Suppressing the dependency request would silently break the rankings
  flow.
- The conservative answer: leave `PublishDependencyRequest` ungated
  (dependencies are a contract distinct from spawning) and document
  that distinction.

Recommend leaving this site as-is, but capture the policy in the
helper's xmldoc: dependencies bypass `ShouldSpawn` because they
represent "this work cannot proceed without X," not "spawn X for the
sake of completeness."

## Test seam — pattern enforcement

To prevent regression, add a unit test under
`test/unit/SportsData.Producer.Tests.Unit/Application/Documents/Processors/`
that uses Roslyn syntax analysis (or simpler reflection + source
inspection) to enforce the contract:

- Find every `.cs` file under
  `src/SportsData.Producer/Application/Documents/Processors/Providers/`.
- For each file, locate every invocation of `PublishChildDocumentRequest`.
- Walk the parent expression chain to find an enclosing `if` whose
  condition contains `ShouldSpawn(`.
- Fail the test if any invocation lacks an enclosing `ShouldSpawn` guard,
  except for an explicit allowlist of intentional exceptions.

Roslyn is overkill for an opening pass — a string-based check
(load file → regex for `PublishChildDocumentRequest` → confirm a
preceding line within N lines contains `ShouldSpawn(` or `isNew`) catches
every site we care about today and is trivially understandable. We can
upgrade to Roslyn later if false positives appear.

The same test should flag any new `_publishEndpoint.Publish(new DocumentRequested(...))`
direct calls, since those bypass the helper-routed propagation and the
guard.

## Implementation order

1. **Helper extension** — add optional `propertyBag` parameter to
   `PublishChildDocumentRequest` and `PublishDocumentRequestInternal`.
   Required for the Drive fix.
2. **Wrong-helper fixes** — Situation (football + baseball) and
   PowerIndex switch from `PublishChildDocumentRequest` to
   `PublishDependencyRequest`. Restores retry dedup at these sites.
3. **Live fan-out fixes** — Roster (add `ShouldSpawn` guard) and Drive
   (move to helper + add guard). These are the cascade leaks.
4. **EventDocumentProcessorBase** — add the guard at the contest →
   competitions hop.
5. **Season-tier fixes** — `SeasonType`, `Season`, `GroupSeason`,
   `SeasonPoll`. Lower live impact (one-time-per-season cascades) but
   the pattern needs to be uniform.
6. **Test seam** — string-based pattern-enforcement test, run as part
   of the standard test suite. Also flags any new raw
   `_publishEndpoint.Publish(new DocumentRequested(...))` calls.
7. **xmldoc** — beef up the helper xmldocs with the
   dependency-vs-child distinction, the dedup mechanism, and when to
   use which. Rationale lives in code, not just here.

Recommend a single PR per group (1+2+3 together since they share the
helper change and are the live-impact fixes; 4 standalone since it
touches a base class; 5 standalone for review surface area; 6+7
standalone as guardrails).

## Related

- PR #296 (`docs/refresh-contest-cascade-narrowing.md`) — the
  filter-propagation work this audit closes the holes in.
- PR #294 (`memory/project_in_season_completed_cache.md`) — orthogonal
  cache-bypass concern.
