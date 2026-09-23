# Franchise season enrichment from the team page (admin)

**Status:** built 2026-09-23. Admin-only. First write action on the public
franchises API route, deliberately not on the admin controller.

## What it does

The **Admin** tab on a team page (`/app/sport/{sport}/{league}/team/{slug}/{season}`,
visible only to admins, after Logos) has one action: **Enrich {season} season**.
It makes that one franchise season current on the Producer, the same three legs
as the weekly `FranchiseSeasonEnrichmentJob`, scoped to one team:

1. **Record enrichment**: W/L re-derived from finalized contests (Hangfire job,
   `IEnrichFranchiseSeasons`).
2. **Season statistics refresh**: one `DocumentRequested` for the team's ESPN
   TeamSeason document, scoped to spawn only the `TeamSeasonStatistics` child,
   published with explicit Direct delivery (the Producer's ambient EF outbox
   would otherwise capture and discard it, since the handler never saves).
   Skipped with a warning when the franchise season has no ESPN ref.
3. **Metrics**: football only (`ICalculateFranchiseSeasonMetricsCommandHandler`
   is registered inside the football guard); a Hangfire job.

All three share one correlation id, returned to the UI and shown as the Seq
handle. The request is accepted (202) as soon as the legs are enqueued;
nothing waits for them to finish.

## Route and gating

```
POST /api/{sport}/{league}/franchises/{slug}/seasons/{seasonYear}/enrich
```

`SportsData.Api` → `FranchisesController.EnrichFranchiseSeason`, gated by
`[AdminApiToken]`: the Admin role claim from Firebase auth (what the web app
sends) or the `X-Admin-Token` header (Bruno/ops). Same attribute the admin
controller uses; only the placement is new. Reads on the controller stay
public; a unit test pins the attribute to this one action.

The handler (`Application/Franchises/Seasons/Commands/EnrichFranchiseSeason`)
resolves the slug to a franchise and the season year to a FranchiseSeason with
the same two-step Producer lookup as `GetFranchiseSeasonByIdQueryHandler`, then
calls the new `IProvideFranchises.EnrichFranchiseSeason(franchiseSeasonId)`.
404 when either lookup misses; a Producer failure passes through with its
status.

Producer side: `POST api/franchise-seasons/id/{franchiseSeasonId}/enrich`
(GUID-based command route, per convention) →
`EnqueueSingleFranchiseSeasonEnrichmentCommandHandler`.

## Why not the admin controller

The operator is moving admin actions onto the resources they act on. This one
lives on the franchises controller next to the reads it repairs, under the
slug-based route shape every other franchises action uses, and the UI reaches
it through a `FranchiseAdmin` API module rather than the admin API module.

## Failure semantics

Legs run in order on the Producer. If a later leg throws (for example the
broker is down for the statistics publish), the request reports a failure even
though the record enrichment was already enqueued. That is deliberate: this is
a synchronous admin action, every leg is idempotent, and the button can simply
be pressed again. The weekly job makes the opposite choice (swallow and log)
because a thrown exception there would trigger a Hangfire retry storm.

## Verification

- API unit tests: handler (resolution, both 404s, pass-through failure, bad
  sport/league) and a reflection test pinning `[AdminApiToken]` and the route
  template on the action.
- Producer unit tests: all three legs under one correlation id, statistics
  skipped without an ESPN ref, metrics skipped for baseball, 404 for unknown
  id, validation on empty id, failure reported when the publish throws.
- Web (Vitest): the tab posts for the routed team and season, shows the
  correlation id, disables during flight, surfaces server validation text and
  network errors.
- Bruno: `bruno/api/franchise-season-enrich.yml`.
