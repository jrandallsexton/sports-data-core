# Franchise season sourcing from the team page (admin)

**Status:** built 2026-09-27. Admin-only. Sibling of
[franchise-season-enrichment-admin.md](franchise-season-enrichment-admin.md),
on the same Admin tab and the same route shape.

## What it does

The **Admin** tab on a team page (`/app/sport/{sport}/{league}/team/{slug}/{season}`)
has a **Source {season} season** action, above Enrich. It re-requests that one
franchise season from ESPN: a single `DocumentRequested` for the team's ESPN
TeamSeason document, with `IncludeLinkedDocumentTypes` left null so the full
child cascade runs (events/schedule, record, rank, statistics, roster, leaders,
coaches, and so on). `TeamSeasonDocumentProcessor` handles it as an update, and
on an update it spawns each child type that `ShouldSpawn` allows; null allows
all of them.

It exists for seasons that were incompletely sourced. The immediate case:
South Alabama 2026 showed 5 games on its schedule.

It is the single-team version of the bulk
`POST api/franchise-seasons/seasonYear/{seasonYear}/source`
(`RequestFranchiseSeasonSourcingCommandHandler`) and publishes the same message
shape (id = URL hash of the clean ESPN URL, `ParentId` = franchise season id,
`CausationId.Producer.FranchiseSeasonService`), with explicit Direct delivery
because the handler never saves.

### Why the full cascade, not just Event

The filter propagates to children. Narrowing it to `[Event]` would stop each
event from spawning its own children (its competition and everything under
it), so new games would arrive incomplete.

### Cost: every press is a full live crawl for the current season

For the current season (season year >= `CommonConfig:CurrentSeason`) the
Provider bypasses its Mongo cache, so the request fetches ESPN live rather
than replaying the stale cached copy. That is what makes the repair work.

The same season test also turns off the Provider's republish suppression
(`ResourceIndexItemProcessor`: suppression requires `!IsCurrentSeason`), so
nothing deduplicates a current-season re-source. Every press re-fetches the
team's whole tree from ESPN (schedule, every game under it, roster) at the
Provider's request pacing. Only historical seasons get the suppression, and
only for unchanged content inside the cooldown.

## Operating it

1. Press **Source {season} season**. The 202 response carries the correlation
   id; search Seq for it to follow the cascade.
2. Documents arrive asynchronously. Give it a few minutes and reload the page.
3. Once the games are in, run **Enrich {season} season** so the record,
   statistics and metrics are recomputed from the new games. Sourcing does not
   trigger enrichment.

Don't press it repeatedly. For the current season each press is another full
live crawl of the team's tree (see Cost above); nothing absorbs the repeat.
If the first run hasn't landed, follow the correlation id in Seq before
pressing again.

## Route and gating

```
POST /api/{sport}/{league}/franchises/{slug}/seasons/{seasonYear}/source
```

`SportsData.Api` → `FranchisesController.SourceFranchiseSeason`, gated by
`[AdminApiToken]` (Admin role claim, or `X-Admin-Token` for Bruno). A
reflection test pins the attribute and route template.

The handler (`Application/Franchises/Seasons/Commands/SourceFranchiseSeason`)
uses the same two-step slug/season lookup as the enrich handler (copied, not
shared), then calls `IProvideFranchises.RequestSingleFranchiseSeasonSourcing`.
404 when either lookup misses; a Producer failure passes through with its
status.

Producer side: `POST api/franchise-seasons/id/{franchiseSeasonId}/source` →
`RequestSingleFranchiseSeasonSourcingCommandHandler`. It is registered for
every sport mode; it depends only on `TeamSportDataContext`.

## Failure semantics

- Unknown franchise season: 404.
- No usable ESPN ref on the franchise season: 400 with a message saying so.
  The bulk handler skips such seasons; here sourcing is the action's only
  job, so it reports the failure.
- Broker publish fails: 500 with a fixed message that includes the
  correlation id. The exception text goes to the log, not the response.

## Verification

- Producer unit tests: one TeamSeason request with the full cascade, Direct,
  under the caller's correlation id; the current sport carried through
  (baseball); no-ref and unparseable-ref failures; 404; validation; publish
  failure.
- API unit tests: resolution, both 404s, pass-through failures (including the
  Producer's no-ref 400), bad sport/league, validation; authorization pin.
- Web (Vitest): the source section posts for the routed team and season,
  shows the correlation id, surfaces server validation text, and runs
  independently of Enrich.
- Bruno: `bruno/api/franchise-season-source.yml`.
