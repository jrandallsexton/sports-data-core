# 13. Cold-load

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (live) · **Status:** done 2026-10-11 (uncommitted); local reload check pending, see Outcome

**Depends on:** [08 Persist and audit](08-persist-and-audit.md)

**Feature doc:** [Stage 6: persist](instant-analysis.md#stage-6-persist) · [Stage 8: display](instant-analysis.md#stage-8-display)

## Goal

A card loaded mid-game shows the latest play's facts without waiting for the next play.

## Scope

- Add the latest play's `PlayAnalysis` items to the matchup payload, using the same LATERAL-join pattern `GetMatchupsByContestIds.sql` uses for `LastPlayDescription`.
- Carry them through the API's matchup DTO to web and mobile, merged like the other live fields.

**Out of scope:**

- History of earlier plays' facts.

## Deliverables

- SQL and DTO changes.
- Client merge changes (web and mobile).

## Acceptance criteria

- [ ] Reloading a card mid-game shows the current play's facts (unit-tested on both clients; local check pending).
- [x] No extra query per card (one batched query per request, live games only).

## Notes

- Can run in parallel with tasks 11 and 12.

## Outcome (2026-10-11)

- **Producer:** `GetMatchupsByContestIdsQueryHandler` stitches the live facts for each live game's `LastPlayId`
  (`GetPlayAnalysesAsync`): one EF query per request (`AsNoTracking`, projected), live rows only
  (`AnalysisRunId IS NULL`), ordered by `Ordinal`, and only for the contests the snap-state stitch already treats as
  live. A stitch rather than the LATERAL join the scope suggested: the handler's other live data (down, distance,
  possession) is stitched the same way, it skips finished weeks entirely, and it avoids grouping on JSON in the shared
  SQL. Using the SQL's `LastPlayId` keeps the facts on the same play as the last-play line beside them.
- **Core / API:** `PlayAnalysisItemDto { Id, FactType, Text }` (the SignalR item shape), `LeagueMatchupDto.PlayAnalysis`,
  `MatchupForPickDto.PlayAnalysis`, mapped in `MatchupForPickDtoMapper` (so the admin Football page's matchup gets it
  too). The API's league-week cache doesn't cache while any game is live, so facts can't go stale there.
- **Clients:** `playAnalysisForCard(live, matchup)` on web and mobile: live facts for the current play first, else the
  payload's facts while the payload's last play is still the card's last play. Used by `PicksPage`, the admin Football
  page and mobile `MatchupCard`.
- **Tests:** Producer stitch (display order; lab runs and other plays ignored; empty input), web and mobile helper
  tests, mobile card cold-load. Producer 894, API 1078, Core 278, web 160, mobile 380; tsc and ESLint clean.

### Local reload check (pending)

Replay the example game with a few seconds between plays, then reload `/app/admin/football` (or the picks page) during
a play that has facts: they should show immediately, before the next play.
