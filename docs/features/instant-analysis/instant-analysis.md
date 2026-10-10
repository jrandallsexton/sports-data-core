# Instant Analysis

**Status:** planning (2026-10-10). Nothing is built yet.

**Scope of v1:** NCAAFB live games. NFL rides along later; MLB is a separate phase.

**Naming:** in code the unit is a **`PlayAnalysis`** (entity, table, events,
SignalR message). "Tidbit" appears only in prose, as the everyday word for one
fact a commentator would say.

**SQL:** any SQL written for this feature (spikes, prototypes, audit scripts) lives
in `docs/features/instant-analysis/sql/`, not in the repo's `sql/pgsql/` folder.

## Tasks

One document per task, in dependency order. Tasks 05/06 and 11/12/13 can run in parallel.

| # | Task | Phase | Depends on |
|---|---|---|---|
| 01 | [Data spike](01-data-spike.md) | 0 | — |
| 02 | [Enablement and schema](02-enablement-and-schema.md) | 1 (proof of concept) | — |
| 03 | [Trigger](03-trigger.md) | 1 (proof of concept) | 02 |
| 04 | [Classifier](04-classifier.md) | 1 (proof of concept) | 01, 03 |
| 05 | [Game facts](05-game-facts.md) | 1 (proof of concept) | 04 |
| 06 | [Season facts](06-season-facts.md) | 1 (proof of concept) | 01, 04 |
| 07 | [Select and phrase](07-select-and-phrase.md) | 1 (proof of concept) | 05, 06 |
| 08 | [Persist and audit](08-persist-and-audit.md) | 1 (proof of concept) | 02, 07 |
| 09 | [Admin replay lab](09-replay-lab.md) | 1 (proof of concept) | 08 |
| 10 | [Delivery](10-delivery.md) | 1 (live) | 08 |
| 11 | [Web display](11-web-display.md) | 1 (live) | 10 |
| 12 | [Mobile display](12-mobile-display.md) | 1 (live) | 10 |
| 13 | [Cold-load](13-cold-load.md) | 1 (live) | 08 |
| 14 | [Enrichment: conference, national and team-rank facts](14-enrichment.md) | 2 | Phase 1 done |
| 15 | [Move to the database, first weak-pattern filters](15-move-to-database-and-filters.md) | 2 | 09, live data |
| 16 | [Depth](16-depth.md) | 3 | Phase 2 done |

## Goal

Broadcasters sprinkle in "tidbits" the moment a play happens:

> *Cooley intercepts the ball! That's his second interception of the season.*
> *That's the third takeaway for Oklahoma's defense today.*
> *Henderson's 42-yard run is the longest of the game, and puts him over 600 rushing yards on the season.*

Instant analysis produces these tidbits from our own data and pushes them to the
MatchupCard over SignalR, alongside the current play and score we already push.

It is a pipeline, not one query:

```
play arrives → classify → dig → select → phrase → persist → deliver → display
```

## Principles

These come from features already in the codebase, and they apply here unchanged.

1. **Every number comes from a query; nothing invents one.** Spread Context ("The
   Line", `docs/features/matchup-spread-context.md`) and StatBot
   (`docs/features/statbot-advisor.md`) are built this way. A tidbit is a typed
   fact plus a template, never free-form prose.
2. **Deterministic first.** The phrasing step is a template catalog, like SmackBot's
   (`docs/features/smackbot-voice.md`). An LLM rewrite can come later, but only
   as an optional asynchronous step, never on the live per-play path.
   `docs/metrics-modeling/ai-provider-routing-per-sport.md` puts local-model
   latency at 30-90 seconds, which is "not for any realtime path".
3. **Correct as of this play.** A tidbit describes the moment it is attached to,
   even when our stored statistics haven't caught up yet (see Timing).
4. **Say nothing rather than something wrong.** A missing fact is better than a
   stale or wrong one. Ordinary facts are not wrong: firsts and plain counts are
   exactly what commentators say.
5. **Ship dark.** A config kill switch, plus an admin lab that replays real games,
   before anything is user-facing.

## Worked example: the interception in `_debug_instant_analysis.sql`

| Step | What happens |
|---|---|
| Play arrives | `CompetitionPlay` with `TypeId = 26` (Pass Interception Return) in competition `6f3ebe93…` |
| Who | `CompetitionPlayParticipant` rows: `passer`, `passDefender`, `returner`, `tackler`. ESPN has **no "interceptor" role**: the intercepting defender appears as `passDefender` and again as `returner` (confirmed in the Producer test fixture `EspnFootballNcaaEventCompetitionPlays.json`). |
| Classify | Kind = **Takeaway / Interception**. Actor = the `passDefender` (Tamarcus Cooley, S). Victim = the `passer`. Team = the defense. |
| Dig | Cooley's interceptions this season, including this one: **2**. The passer's interceptions thrown this season. The defense's takeaways in this game. Does Cooley lead his conference or the nation in interceptions? |
| Select | "2nd INT of the season" is exactly what a commentator says, so it flows. If other facts also apply ("leads the SEC", "3rd takeaway today"), they flow too; priority only orders them for display. |
| Phrase | `Cooley's second interception of the season.` |
| Deliver | A `PlayAnalysisGenerated` event for the play goes to the API, then out over SignalR. The card shows it under the last-play line while that play is still the latest. |

## How the pipeline works today (the parts we hook into)

Verified against source on 2026-10-10.

- **Play ingestion is in Producer.**
  - `FootballEventCompetitionPlayDocumentProcessor` builds `CompetitionPlay` and its `CompetitionPlayParticipant` rows.
  - For a **new** play in an **in-progress** game, it publishes `FootballPlayCompleted` (`src/SportsData.Core/Eventing/Events/Contests/Football/FootballPlayCompleted.cs`) through the EF outbox, in the same transaction as the play row.
  - Football participants are strict: the play is held back until every participant's `AthleteSeason` is sourced. So when the event fires, all participant rows exist.
- **`FootballPlayCompleted` carries no play type and no participants.** It has the play id, description, period, clock, scores, possession, scoring flags and down/distance. Anything that needs the play type or who was involved must load the play by `PlayId`, which only Producer can do directly.
- **Delivery:** the API's `FootballPlayCompletedConsumer` sends the raw event with `Clients.All.SendAsync("FootballPlayCompleted", …)` on `NotificationHub` (`/hubs/notifications`). There are **no per-contest groups**; clients filter by `contestId`. The web `ContestUpdatesContext` and mobile `contestUpdatesStore` keep a per-contest record (`lastPlayId`, `lastPlayDescription`, …) that MatchupCard reads.
- **Corrections are not re-emitted.** Re-processing an existing play (an ESPN edit) updates the row but publishes nothing, and edits to older plays mostly aren't picked up at all (`docs/features/in-season-cache-bypass-fix.md`).
- **New consumers in the API need RabbitMQ shovels.** Producer publishes to per-sport brokers, so every new API consumer needs a shovel per Producer broker in sports-data-config (see the comment at `src/SportsData.Api/Program.cs` near the consumer registrations).

## Stage 1: trigger

**Decided (2026-10-10): a Producer-internal consumer of `FootballPlayCompleted`,
with the analysis work on the priority queue.**

| Option | For | Against |
|---|---|---|
| **A. Producer consumer of `FootballPlayCompleted` (decided)** | Has the play row, participants and stats tables locally; runs after the play commits; no new shovels for the trigger; the play processor stays unchanged. | One more consumer on the live path. It must be fast, and a failure must never affect the play pipeline (it's a separate consumer, so it can't). |
| B. Inline in the play processor | No extra hop. | Couples analysis to ingestion; slows or fails the play write; violates the thin-processor pattern. |
| C. API consumer | Already receives the event. | No access to Producer's database; every dig becomes an HTTP call to Producer. |

The consumer first checks that analysis is enabled for this game: the kill
switch, plus `CompetitionStream.LiveAnalysisEnabled` (see "Ship dark"). It stays a
thin shim (the ingest-consumer convention) and enqueues the analysis job on
**`HangfireQueues.Live` (`"00-live"`)**, the priority queue competition streams
already use. That queue sorts ahead of `default` and `daemon` (Hangfire's
PostgreSQL storage dequeues by queue name; see `HangfireQueues` in
`src/SportsData.Core/Processing/BackgroundJobProvider.cs` and
`docs/features/athlete-cascade-scoping.md`), so live analysis never waits behind
bulk backfill. The job loads the play and its participants by `PlayId`, runs the
pipeline, and publishes **`PlayAnalysisGenerated`** if it found anything to say.

Only new plays in in-progress games fire the trigger. That's what we want: no
tidbits for replays of completed games outside the lab, and none for pre-game
noise.

## Stage 2: classify (what happened, who did it)

Classification turns a play into a **kind**, an **actor** (and sometimes a
victim), and a set of fact families worth investigating. It works from the play
type id (`src/SportsData.Core/Common/PlayType.cs`), the participant roles,
`StatYardage`, the scoring flags and the down/distance.

Classify on the enum ids, not the `lkPlayType` lookup table, which is missing
several newer types (`Interception` 63, `InterceptionReturnTouchdown` 36,
`FumbleLost` 29, `Safety` 20, and others). ESPN's play-type text isn't stored.

| Kind | Play types (ids) | Actor (role) | Fact families |
|---|---|---|---|
| Interception | 26, 63, 36 (pick-six) | `passDefender` (also `returner`); victim `passer` | season INTs, conference/national rank, team takeaways today, passer's INTs thrown |
| Fumble lost | 29, 39 (return TD) | recovering defender (verify role), victim the fumbler | team takeaways today, season forced fumbles |
| Sack | 7 | sacker (verify role) | season sacks, rank, team sacks today |
| Passing TD | 67 | `passer`, `receiver` | season passing TDs, receiving TDs, milestones |
| Rushing TD | 68 | `rusher` | season rushing TDs |
| Explosive run | 5 with `StatYardage` ≥ threshold | `rusher` | longest run of the game/season, season rushing yards, milestone crossed |
| Explosive pass | 24 with `StatYardage` ≥ threshold | `passer`, `receiver` | longest play of the game, season yards, milestones |
| Field goal | 59 (made), 60 (missed) | `kicker` | long of the season, season FG made/attempted |
| Return TD | 32, 37, 36, 39 | `returner` | rare-event facts |
| Safety | 20 | (verify roles) | rare-event facts |

The thresholds (e.g. explosive run ≥ 20 yards) and the role mapping live in one
small classifier with unit tests, not scattered through queries. **Decided:** in
code for the proof of concept, moving to the database later (Phase 2) so they can
be tuned without a deploy.

**Phase 0 task:** confirm the participant roles for fumbles, sacks and safeties
against live data. Only the interception role mapping is verified so far.

## Stage 3: dig (the data)

What we have, how fresh it is, and what it can answer:

| Source | Grain | Freshness during a game | Answers |
|---|---|---|---|
| `CompetitionPlay` + `CompetitionPlayParticipant` | per play | **live** (the trigger) | everything "in this game": his 2nd INT today, team's 3rd takeaway, longest run of the game. Participants are only stored from 2026-07-22 onward, so season counts built from plays are complete only for 2026 games. |
| `AthleteCompetitionStatistic` (+ Category/Stat) | athlete × game, cumulative | near-live, but **lags the play**: fetched after the play is saved, debounced 3 minutes per athlete (scoring plays bypass the debounce) | season-to-date totals when summed over the season's **completed** games; 2025 is missing |
| `AthleteSeasonStatistic` | athlete × season | **refreshed in-season** (see below); exactly how fresh relative to a live play is a Phase 0 measurement. Duplicated per re-source, newest wins (`docs/features/player-pickem/athlete-season-stats-audit-2026-08.md`). | current-season totals (the debug SQL's interception example reads the current count here) and previous seasons |
| `SeasonTypeLeader` | national top ~200, 13 categories | weekly (operator-registered resource index) | national rank, and conference rank by filtering on `FranchiseSeason.GroupSeasonMap` |
| `CompetitionLeader` | per-game leaders | streamed every 60s | "leads all players today" |
| `FranchiseSeasonStatistic` (with `Rank`) | team × season | weekly enrichment | "Oklahoma's defense ranks 3rd nationally in takeaways" |
| `AthleteCareerStatistic` | athlete career totals | rare (only when the Athlete document is processed); NCAA coverage is thin (processor registered 2026-08-30) | career totals, later. **No career highs or longs are stored anywhere.** |
| `FranchiseSeason.GroupSeasonMap` / `GroupSeason` | conference membership | static per season | scoping "leads the SEC" |

**How season stats stay current.** Several paths re-source the team-season
document during the season, and reprocessing it can spawn each rostered
`AthleteSeason` (whose statistics follow) and the team-season leaders (which
spawn season stats for each leader):

- `EventCompetitionLeadersDocumentProcessor`, which the live streamer polls every 60 seconds.
- `EventDocumentProcessorBase`, via the daily contest-update and weekly contest-enrichment jobs.
- `SeasonTypeWeekRankingsDocumentProcessor` (weekly rankings).
- `FranchiseSeasonEnrichmentJob` (weekly).
- The franchise-season sourcing commands.

Whether each request lets the athlete cascade run (`ShouldSpawn`) determines how
current the season stats are at any moment. Phase 0 measures this against live
games.

### "As of this play": how a season count is computed

Season stats can't be assumed to include the play that just happened, or even
the rest of today's game. The count is anchored on data whose coverage is known,
plus the current game counted from plays:

```
seasonCount = season-to-date total from games BEFORE the current competition
            + count of qualifying plays in the current competition by this athlete,
              up to and including this play's sequence number
```

The first term can come from either source:

- **`AthleteSeasonStatistic`**, if Phase 0 shows it reliably reflects completed
  games and never already includes part of the current game (which would
  double-count).
- **Σ `AthleteCompetitionStatistic`** over the season's earlier competitions,
  which is game-scoped by construction, so it can't overlap the current game.

The second term is counted from the play rows that just triggered us. Together
they're correct the instant the play happens, independent of stats-fetch lag and
the 3-minute debounce. Comparing the two sources in Phase 0 also checks each
against the other.

### "Leads the SEC" (Phase 2)

**Decided (2026-10-10): conference- and national-level analysis is Phase 2.** Get
the facts flowing first, then enrich them. Two options when we get there:

- **A. `SeasonTypeLeader` + `GroupSeasonMap`.** Cheap and already sourced, but
  up to a week stale, and it doesn't include today's play. Fine for "entered the
  game leading the SEC".
- **B. Our own season-to-date aggregation across conference athletes.** Uses the
  same "as of this play" sum for every athlete in the conference. Correct live,
  but a heavier query. A per-conference, per-category cache refreshed after each
  final, plus the live delta, keeps it cheap.

Recommendation: start with A, phrased honestly ("entered today leading the SEC
with 3"), and move to B once the cache exists.

### Out of reach for now

- **Career highs and longs** ("longest run of his career"): no data. It could be
  derived from plays (max `StatYardage` per athlete via `AthleteSeason.AthleteId`),
  but only for plays since 2026-07-22. Phase 3.
- **Milestones based on career totals**: career data is thin and stale for NCAA.
  Season milestones (crossing 1,000 rushing yards) are fine, using the season sum.

## Stage 4: select

**v1 lets facts flow.** We don't yet know which facts are weak, and guessing up
front would discard the very data that tells us. So every correct fact is
generated, persisted and delivered, and filters come later, once patterns emerge.

- **Every correct fact flows.** Firsts ("his first interception of the season"),
  plain counts ("his second"), in-game counts, leads and milestones are all the
  kind of thing commentators say. The only facts dropped in v1 are wrong or stale
  ones (the freshness guard below).
- **Repeats are wanted.** Restating a fact every time it applies is the point:
  "his second interception today", "Henderson now has 142 rushing yards" after
  each carry. Nothing suppresses a fact for having been said earlier in the game.
- **Priority orders, never filters.** Each fact type has a priority. It orders a
  play's facts for display (the card may show only the first one or two and let
  the rest wait or rotate), but every fact is still persisted and sent.
- **One set of tidbits per play.** Messages are delivered at least once, so the
  same play can be analysed twice; tidbits are keyed by `PlayId` and written
  once. This prevents duplicates for one play, never a fact's recurrence.
- **Freshness guard.** Drop a fact if a required source is missing or too stale.
  This is about correctness, not interest.

### Learning what's weak

Because every fact is persisted (`PlayAnalysis`), patterns can be studied directly
rather than guessed:

- **Lab ratings:** 0-4 star ratings in the replay lab, by fact type and situation.
- **Logs:** generated and dropped facts per game from the Seq events (see "Logging and audit").
- **Volume:** how often each fact type fires per game, and whether some flood the
  card (e.g. a running count after every carry for a workhorse back).
- **Shape:** which facts are near-duplicates of each other on the same play.

Filters are added from that evidence (by fact type, value range, situation or
frequency). Like the fact-type priorities, they start in code and move to the
database in Phase 2, so they can be tuned without a deploy.

## Stage 5: phrase

Each fact type has one or more templates with typed tokens, rendered
deterministically, for example `{actor.shortName}'s {ordinal:count} interception of the season.`

**Decided (2026-10-10):** the templates live in code while the proof of concept
comes together, then move to a database catalog (Phase 2), as SmackBot's phrases
did, so the lab can edit them without a deploy.

## Stage 6: persist

A new Producer entity, **`PlayAnalysis`** (one row per tidbit):

| Column | Purpose |
|---|---|
| `Id`, `ContestId`, `CompetitionId` | identity and joins |
| `PlayId` | **the play that triggered the fact. Required foreign key to `CompetitionPlay`** (same Producer database), so the link can't dangle. Indexed on (`CompetitionId`, `PlayId`) for post-game audits. |
| `AnalysisRunId` | null for live analysis. Set for a lab replay run, which reuses the same plays, so replay rows never mix with live ones and the one-set-per-play rule applies per run. |
| `Ordinal` | the fact's position in its play's set. Unique on (`PlayId`, `AnalysisRunId`, `Ordinal`) with NULLS NOT DISTINCT, so a retried or redelivered job can't write a second set. |
| `FactType` | e.g. `SeasonCount`, `GameTakeaways`, `ConferenceLeader`, `GameLongest`, `MilestoneCrossed` |
| `Facts` (jsonb) | the numbers that produced the text (actor ids, value, rank, scope) **and their provenance**: which source answered each term (e.g. `AthleteSeasonStatistic` doc id or the summed `AthleteCompetitionStatistic` competition ids), the play sequence counted up to, and the "as of" cut-off |
| `Text` | the rendered sentence |
| `Priority` | the fact type's priority, which orders the play's facts for display |
| `TemplateVersion`, `CreatedUtc` | provenance |

Persisting gives us three things:
- **An audit trail:** why did we say that, and on which play? Every fact is
  linked to its triggering play, so a post-game audit can walk the game play by
  play:

  ```sql
  SELECT p."SequenceNumber", p."TypeId", p."Text" AS play, t."FactType", t."Text" AS tidbit, t."Facts"
  FROM "CompetitionPlay" p
  LEFT JOIN "PlayAnalysis" t ON t."PlayId" = p."Id" AND t."AnalysisRunId" IS NULL
  WHERE p."CompetitionId" = @competitionId
  ORDER BY p."SequenceNumber"::bigint, t."Priority";
  ```

  Plays with no facts show up too, which is how gaps get noticed.
- **Cold-load:** a card loaded mid-game can show the latest tidbit. That's the same LATERAL-join pattern `GetMatchupsByContestIds.sql` already uses for `LastPlayDescription`.
- **Lab rating data**, as SmackBot's ratings are.

### Logging and audit

Every analysed play is logged, not only the facts that survive, so the decisions
can be audited and the "learning what's weak" analysis has the full picture.
Structured Seq events (the observability convention: Seq for logs), carrying the
play's `CorrelationId` from `FootballPlayCompleted`:

| Event | Logged fields |
|---|---|
| Play analysed | ContestId, CompetitionId, PlayId, sequence, play type id, classified **kind**, actor/victim AthleteSeasonIds and roles, whether analysis was enabled (kill switch, `LiveAnalysisEnabled`) |
| Fact generated | **PlayId**, fact type, value, scope (game/season/conference/national), priority, rendered text, and the provenance (sources and "as of" cut-off, as in `Facts`) |
| Fact dropped | **PlayId**, fact type, **why** (missing or stale source, failed freshness guard, a later weak-pattern filter by name), and the source state that caused it |
| Timing | per-stage durations (load, classify, dig per fact family, phrase, persist) and the total |

- **Persisted rows** are the durable record of what was said, and why.
- **Logs** add what was considered and rejected, plus timings, which never reach the table.
- **Dropped facts and filter decisions** are logged at Information level, so the weak-pattern analysis can count them in Seq without a separate table.
- **Lab replays** log the same events, tagged as replay, so lab and live data can be compared.

## Stage 7: deliver

- **Event:** `PlayAnalysisGenerated { ContestId, CompetitionId, PlayId, Items[] }`, one item per `PlayAnalysis` row: `{ Id, FactType, Text }`.
- **Transport:** Producer publishes it; the API consumes it and sends `Clients.All.SendAsync("PlayAnalysis", …)`. This matches every other live event today.
- **New consumer, new shovels:** the event exchange must be pre-declared, and there's a shovel per Producer broker in sports-data-config. See the memory notes on shovels and exchange pre-declaration.
- **No per-contest groups yet.** Broadcasting to all is fine at current scale, and `docs/ui/signalr-surface.md` already lists contest groups as a known gap. Adding groups is a separate change, not part of this feature.

## Stage 8: display

- **Web** (`ContestUpdatesContext`) and **mobile** (`contestUpdatesStore`) store `lastPlayAnalysis` per contest, **keyed by `playId`**.
- **MatchupCard** shows the tidbit under the last-play line only while `lastPlayAnalysis.playId === lastPlayId`. The tidbit event can arrive after the play event (it's a later hop), and once the next play lands the old tidbit no longer applies.
- **Styling:** a distinct style, so a tidbit reads as commentary, not as part of the play description.
- **Optional:** cold-load the latest tidbit from the matchup REST payload.
- **On for everyone (decided 2026-10-10)** once live. There's no per-user opt-in; the kill switch and the per-game `LiveAnalysisEnabled` flag control rollout.

## Timing

```
ESPN play visible ──(10s poll)──► Provider ──► Producer play processor
  ──(outbox commit)──► FootballPlayCompleted ──► API ──► SignalR (play shown)
                    └─► Producer tidbit consumer ──(dig ≤ ~200 ms target)──►
                        PlayAnalysisGenerated ──► API ──► SignalR (tidbit attaches by playId)
```

The tidbit trails the play by one consumer hop plus the queries. The queries are
set-based and indexed: play rows by competition, stats by athlete-season. Keep
the dig budget small; a fact that can't be computed quickly is dropped, not waited for.

## Corrections

ESPN sometimes reverses a play: an interception overturned on review, a fumble
ruled down. Edits to an existing play aren't re-emitted today, so a tidbit for a
reversed play could stand.

- **v1:** accept the risk, but skip tidbits for plays that carry a penalty or a
  review marker in the text, if one can be identified.
- **Later:** when play-correction handling exists, retract a tidbit with a
  `PlayAnalysisRetracted` event keyed by `PlayId`.

## Ship dark and the lab

- **Kill switch:** `SportsData.Producer:InstantAnalysis:Enabled`, off unless set to `true`. Producer pods are single-sport, so setting it in a sport label (e.g. `Prod.FootballNcaa`) keys it per sport. It's read once at startup (App Configuration isn't refreshed at runtime), so flipping it needs a Producer restart; the per-game flag below takes effect immediately. It gates the consumer.
- **Per-game opt-in:** a new `LiveAnalysisEnabled` column on `CompetitionStream`
  (`src/SportsData.Producer/Infrastructure/Data/CompetitionStream.cs`, one row per
  streamed competition), **default `false`**. While we're tuning, analysis runs only
  for the handful of games an operator picks, not every game we stream, so the noise
  stays contained.
  - **Rule:** the consumer runs only when the kill switch is on **and** the play's competition has a stream row with `LiveAnalysisEnabled = true`.
  - **How it's set:** an operator sets it per game (SQL at first, then an admin endpoint or toggle on the existing stream tooling). It needs a Producer migration.
  - **Later:** once tidbits are trusted, the default can flip, or the flag can be set automatically when streams are scheduled.
  - **Replays:** lab replays bypass the flag. They run the pipeline directly for a completed game, which may have no stream row.
- **Admin lab, following the SmackBot Lab pattern:**
  - Replay a completed game through the existing contest replay service (`FootballContestReplayService` re-publishes every stored play with a 1-second delay).
  - Show the tidbits each play would produce, including the candidates that selection rejected and why.
  - Rate them 0-4 to tune thresholds and templates.
- **Replay doesn't reproduce past "as of" data.** It runs against today's data, so a season count during a replay of a game played weeks ago includes later games. The lab must compute "as of" the replayed game: season sums restricted to competitions that started before it. That restriction is needed anyway for correctness.

## Phases

| Phase | Scope |
|---|---|
| **0. Spike** | Verify participant roles for fumbles, sacks and safeties on live data. Measure how current `AthleteSeasonStatistic` is during and after games, and `AthleteCompetitionStatistic` coverage for 2026 FBS. Prototype the "as of this play" season-count query on the example interception, from both sources. |
| **1. Facts flowing** | NCAAFB only. **Proof of concept, dark:** kinds interception, sack, passing/rushing TD, explosive run/pass; fact types season count (as of this play), in-game counts, game longest, season milestones crossed. Producer consumer → analysis job on `HangfireQueues.Live`, `PlayAnalysis` table, logging, templates/thresholds/priorities in code, kill switch, `CompetitionStream.LiveAnalysisEnabled`, admin replay lab. **Then live, on for everyone:** `PlayAnalysisGenerated` → API → SignalR, web and mobile MatchupCard display, cold-load, enabled game by game via the per-game flag. |
| **2. Enrichment** | Conference and national-level analysis ("leads the SEC": option A, then B) and team-rank facts (`FranchiseSeasonStatistic.Rank`). Templates, thresholds and fact-type priorities move to the database. First filters for weak fact patterns, learned from lab ratings and logged volume. |
| **3. Depth** | Career facts and highs (derived from plays), NFL, MLB (its participants are lenient and its event already carries batter/pitcher), corrections and retraction, optional async LLM rephrasing, per-contest SignalR groups. |

## Decisions (2026-10-10)

1. **Trigger:** lives in Producer. A consumer of `FootballPlayCompleted` enqueues
   the analysis on the priority queue competition streams already use
   (`HangfireQueues.Live`, `"00-live"`).
2. **Templates:** in code while the proof of concept comes together; they move to
   the database (Phase 2).
3. **Conference and national-level analysis:** Phase 2. Get the facts flowing,
   then enrich them.
4. **Display:** on for everyone once live.
5. **Explosive-play thresholds and fact-type priorities:** in code for now; they
   move to the database (Phase 2).

## Known data gaps (from the 2026-10-10 survey)

- Participant rows exist only for plays ingested since 2026-07-22. Historical replays would fill older games.
- No "interceptor" participant role. Other roles still need verifying.
- `AthleteSeasonStatistic` is refreshed in-season through team-season re-sourcing, but how current it is during a live game isn't established yet. It also has duplicate documents (newest wins).
- 2025 per-game athlete stats (`AthleteCompetitionStatistic`) are missing.
- `AthleteCareerStatistic` is thin for NCAA. No career highs or single-play bests are stored.
- No conference leaderboards are sourced; `SeasonTypeLeader` is national, weekly, top ~200.
- `lkPlayType` is missing newer play types; ESPN's play-type text isn't stored.
- `FootballPlayCompleted` has no play type or participants (fine for a Producer consumer, which loads them).
