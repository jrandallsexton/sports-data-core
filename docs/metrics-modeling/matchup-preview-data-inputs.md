# Matchup Preview — Data Inputs (Capture + Historical Enrichment Design)

Status: analysis + design, 2026-08-07. Captures exactly what the preview
model received during the 2025-season alpha (and receives today), then
proposes the historical-data enrichment for early-season quality. The
prompt-capture / experiment tooling (§3.6) is IMPLEMENTED (PR #601);
only the historical enrichment (§3–3.5) remains unapproved.

Relevant code: `MatchupPreviewProcessor` (SportsData.Api),
`MatchupForPreviewDto` (SportsData.Core), `MatchupPreviewPromptProvider`,
Producer's `GetMatchupForPreview` / `GetFranchiseSeasonPreviewStats` /
`GetFranchiseSeasonMetricsByFranchiseSeasonId` /
`GetFranchiseSeasonCompetitionResults`.

---

## 1. What the model receives today

The processor serializes the ENTIRE enriched `MatchupForPreviewDto` as raw
JSON and appends it to the prompt. Field census:

**Game context:** Sport, SeasonYear, WeekNumber, ContestId, HeadLine,
StartDateUtc, Status/StatusDescription, Venue/VenueCity/VenueState.

**Per team (Away/Home):** FranchiseSeasonId, name, slug, rank (poll),
ConferenceSlug, Wins/Losses, ConferenceWins/Losses, plus three enrichment
blocks fetched by the processor:

- **Stats** (`FranchiseSeasonModelStatsDto`): PointsPerGame, YardsPerGame,
  Passing/RushingYardsPerGame, ThirdDownConvPct, RedZoneScoringPct,
  TurnoverDifferential, PenaltiesPerGame, PenaltyYardsPerGame,
  AvgYardsPerPlay, Sacks, Interceptions, FumblesLost, Takeaways.
- **Metrics** (`FranchiseSeasonMetricsDto`): the advanced block — Ypp,
  SuccessRate, ExplosiveRate, PointsPerDrive, ThirdFourthRate,
  RzTdRate/RzScoreRate, TimePossRatio, the Opp* mirrors of all of those,
  NetPunt, FgPctShrunk, FieldPosDiff, TurnoverMarginPerDrive,
  PenaltyYardsPerPlay, PtsScored/Allowed Min/Max/Avg, GamesPlayed.
  **Both-or-nothing rule:** if either team's metrics are missing, both are
  nulled (asymmetric analytics would bias the model toward the covered
  team).
- **CompetitionResults** (`FranchiseSeasonCompetitionResultDto[]`): the
  team's games this season — date, opponents (short/slug/rank), spread,
  O/U, final scores, winner, spread-winner, O/U result.

**Odds:** Spread (text), Away/HomeSpread, OverUnder, Over/UnderOdds — from
the preferred odds provider (DraftKings).

**Feedback loop:** if a prior preview for this contest was rejected, the
rejection note is appended as "Additional feedback from the editor."

**Prompt selection** (`MatchupPreviewPromptProvider`, Azure Blob container
`prompts`): `hasStats` = BOTH teams have `RushingYardsPerGame` →
`prediction-insights-with-stats-schedule.txt`; else
`prediction-insights-v1.txt`. `PromptVersion` (blob name) is persisted on
every generated preview — good provenance, keep it.

**Model:** whatever `IProvideAiCommunication` is bound to (DeepSeek at
present); model name persisted per preview. `UsedMetrics` flag records
whether the metrics block survived the both-or-nothing rule.

### Prompts — reviewed 2026-08-07 (NOT in source control, by design)

The prompt texts are proprietary ("secret sauce") and live in the Azure
Blob `prompts` container ONLY — this repo is public, so they are never
committed. `docs/metrics-modeling/prompts/` is gitignored; local exports
there are working copies for analysis. Blob inventory: 
`prediction-insights-v1.txt`, `prediction-insights-with-stats.txt`
(legacy variant, not referenced by the provider),
`prediction-insights-with-stats-schedule.txt`, `game-recap-v1/v2.txt`.
PromptVersion (blob name) persisted per preview remains the provenance
mechanism; prompt edits happen against blob storage directly.

**Findings from reading the active prompt
(`with-stats-schedule`, 24-rule contract):**

1. **It opens "You are a college football analyst"** — and since the
   multi-sport unlock (#595) this same prompt generates NFL previews.
   Sport-aware prompt voice (or neutral wording) is now a live gap, and
   folds naturally into the with-history prompt authoring below.
2. **The advanced Metrics block is un-briefed.** The processor ships
   AwayMetrics/HomeMetrics, but the prompt documents only
   AwayStats/HomeStats and CompetitionResults — the model receives ~30
   advanced fields with no instruction. Either the with-history prompt
   teaches them (preferred) or the payload drops them; today they are
   tokens spent on unexplained data.
3. Rules 21–24 already counteract spread-parroting and rank-overweight —
   the prompt engineering anticipated the early-season signal problem;
   the historical blocks give those rules material to work with.

---

## 2. The early-season problem (current behavior)

Every enrichment block is keyed by the CURRENT season's FranchiseSeasonId:

- Weeks 1–2: Stats are null → `hasStats` false → the no-stats prompt.
  Metrics: GamesPlayed 0 or tiny samples (or absent → nulled).
  CompetitionResults: empty or one game.
- Net: an early-season preview reasons from **records (0-0), poll ranks,
  venue, and the betting line** — the line being the only real signal.
  The model is effectively paraphrasing the spread.
- The problem decays but doesn't vanish: through roughly week 4, current-
  season samples are noise-dominated (opponent quality unadjusted).

---

## 3. Historical enrichment design (build-upon proposal)

Principle (user decision 2026-08-07): historical data flows to the model
ALL season, not just early — the model weighs recency; we don't gate it.

### 3a. Prior-season block (per team)

Add `AwayPriorSeason` / `HomePriorSeason` to the preview payload:

- Prior season's final record, conference record, final poll rank (if
  any), and the full `FranchiseSeasonMetricsDto` for that season.
- Resolution: current FranchiseSeasonId → Franchise → FranchiseSeason for
  SeasonYear-1. New Producer query/endpoint (by franchiseId + year, or a
  purpose-built "preview history" endpoint that returns the whole block).
- Same both-or-nothing symmetry rule as current metrics.

### 3b. Head-to-head history

Last N (propose 5) meetings between the two franchises: date, scores,
winner, and the spread result where available. Canonical query by
franchise pair across seasons. High narrative value for the LLM ("X has
won 4 straight in this series").

### 3c. Recency bridge (late prior season form)

The last ~5 games of the prior season per team (same
`FranchiseSeasonCompetitionResultDto` shape) — "how did they finish?"
matters more than season aggregates for early-current-season reads.

### 3d. Roster-churn guardrail (prompt, not data)

NCAA transfer portal / NFL free agency mean prior-season data describes a
partially different team. The with-history prompt must instruct the model
to discount historical signal accordingly (more heavily for NCAA). This
is prompt text, not code.

### 3e. Payload hygiene (recommended, separable)

Today the raw wire DTO is serialized — GUIDs, slugs, $ref-ish fields the
model can't use burn tokens and add noise. Propose a purpose-built prompt
payload (projection of the DTO) so the prompt contract stops being
coupled to wire-DTO churn. Doing this BEFORE adding historical blocks
keeps the enlarged payload inside sane token budgets.

### Prompt evolution

New blob `prediction-insights-with-history-v1.txt` (naming keeps the
PromptVersion provenance meaningful). Selection becomes: history present →
with-history variant (which itself branches on hasStats internally or via
two variants — decide during prompt authoring).

---

## 3.5 Proposed model payload structure (draft, 2026-08-07)

Findings from prototyping the history queries
(`sql/pgsql/_debug_contest.sql`, `_debug_preview_history.sql`, CAR/ARI
test pair):

- **Franchise-level join is the cross-season identity path.**
  FranchiseSeasonIds change every year; Contest → FranchiseSeason →
  Franchise resolves head-to-head across 2001–2025 cleanly.
- **Playoff meetings surface in head-to-head** (e.g. the 2015 NFC
  Championship, 49-15). Keep them — highest narrative value — but label
  them: carry SeasonPhase name and EventNote per game.
- **Spread/O-U context decays historically.** SpreadWinner is null and
  OverUnder = 0 for roughly pre-2012 rows (and scattered later ones).
  Historical ATS fields are OPTIONAL — omit when absent, never zero-fill.
- **`Contest.OverUnder` is the RESULT enum (0 none / 1 Over / 2 Under),
  not the line.** The line is not persisted on historical contests, so
  historical O/U context is result-only.
- **GUID trap:** historical Contest rows carry per-season
  FranchiseSeasonIds. If serialized, the model can echo one into
  `predictedStraightUpWinner` (the output contract demands a
  FranchiseSeasonId). Rule: **exactly two GUIDs in the entire payload** —
  the live Away/Home FranchiseSeasonIds. Every historical block is
  names/labels only.

Draft shape (also implements the 3e hygiene projection — this IS the
purpose-built prompt payload, not the wire DTO):

```jsonc
{
  "Sport": "FootballNfl",              // string enum; lets the prompt voice adapt per sport
  "SeasonYear": 2026, "WeekNumber": 1,
  "StartDateUtc": "...", "Venue": "...", "VenueCity": "...", "VenueState": "...",

  "Away": "Carolina Panthers", "AwaySlug": "carolina-panthers",
  "AwayFranchiseSeasonId": "<guid>",   // one of the only two GUIDs
  "AwayRank": null,
  "AwayRecord": { "Wins": 0, "Losses": 0, "ConfWins": 0, "ConfLosses": 0, "ConferenceSlug": "nfc-south" },
  "AwayStats":   { /* FranchiseSeasonModelStatsDto — null weeks 1-2 */ },
  "AwayMetrics": { /* FranchiseSeasonMetricsDto — both-or-nothing */ },
  "AwaySeasonResults": [ /* current season, GameResult shape below */ ],
  "AwayPriorSeason": {
    "SeasonYear": 2025,
    "Record": { "Wins": 0, "Losses": 0, "ConfWins": 0, "ConfLosses": 0 },
    "FinalRank": null,
    "Stats": { }, "Metrics": { },      // same shapes as current-season blocks
    "LastFiveGames": [ /* GameResult */ ]   // the 3c recency bridge
  },
  // Home* mirrors all of the above

  "HeadToHead": {
    "GamesIncluded": 5,
    "Games": [ /* GameResult, newest first */ ]
  },

  "Odds": { "Spread": "ARI -3", "AwaySpread": 3.0, "HomeSpread": -3.0,
            "OverUnder": 44.5, "OverOdds": -110, "UnderOdds": -110 }
}
```

**One unified `GameResult` shape** for all three lists (season results,
prior-season last-five, head-to-head):

```jsonc
{
  "Date": "2025-09-14", "SeasonYear": 2025,
  "Phase": "Regular Season",           // SeasonPhase.Name; "Postseason" flags playoffs
  "Home": "Arizona Cardinals", "Away": "Carolina Panthers",
  "HomeScore": 27, "AwayScore": 22,
  "Winner": "Arizona Cardinals",
  "SpreadWinner": "Carolina Panthers", // omitted when unknown
  "OverUnderResult": "Over",           // omitted when no line
  "OpponentRank": null,                // season/recency lists only
  "Note": "NFC Championship"           // EventNote; omitted when null
}
```

Serialization rules: omit-null (no zero-fill, no `"SpreadWinner": null`
noise), ISO dates, string enums over ints — every value should be usable
by the model verbatim. Structural choices worth noting:

- **Constant shape year-round** (user directive: history flows all
  season). Week 1 and week 14 payloads differ only in which blocks are
  populated — one prompt contract, no seasonal branching.
- **PriorSeason.LastFiveGames replaces a free-floating "recent form"
  list** — a cross-season "last 5" would overlap AwaySeasonResults
  mid-season; scoping the bridge to the prior season keeps the two lists
  disjoint by construction.
- **No computed series summary** ("Arizona leads 3-2") — the model
  derives it from 5 rows; computing it invites drift between summary and
  rows.
- Rough token cost: GameResult ≈ 40 tokens compact → H2H (5) + 2×5 prior
  + 2×~17 season ≈ 2k tokens of game rows; the hygiene projection claws
  back much of that from dropped GUIDs/wire noise.

---

## 3.6 Prompt capture + persistence plan (2026-08-07, pre-implementation)

Goal (user): run locally, trigger generation for one game from admin,
and see EXACTLY what would be sent to the model — without burning
tokens. Then persist the data payload on every real generation (the
thing we should have been saving all along — today only `PromptVersion`
and `Model` survive; the serialized matchup JSON is gone the moment the
call returns).

### Where things stand in code

- Single-game admin trigger EXISTS:
  `POST /admin/matchup/preview/{contestId}/reset?sport=...` → enqueues
  `GenerateMatchupPreviewsCommand` via Hangfire.
- The processor composes `fullPrompt = promptText + "\n\n" + json(dto)
  [+ editorNote]` inline at `MatchupPreviewProcessor.cs:109` — assembly
  and LLM call are one code path, which is what makes capture easy to
  add faithfully.

### Storage decision: API's own Postgres, NOT Provider's IDocumentStore

Considered: exposing Provider's Mongo `IDocumentStore` to API.
Rejected, for the same reason API never touches Producer's database:
the document store is Provider's PRIVATE persistence for third-party
source documents; crossing that boundary couples API deploys/config to
Provider infra and adds a Mongo connection + failure mode to API pods.
Also mechanical: `IDocumentStore` is defined inside SportsData.Provider
(`MongoDocumentService.cs`), not Core — using it would mean moving the
interface or referencing Provider outright.

The scale doesn't warrant a document store anyway: payloads are
~10–30 KB of JSON, a few thousand per season (~60–100 MB/season).
A `jsonb` column in API's Postgres is transactional with the preview
row, joinable against `MatchupPreview`/contest for backtesting, and
queryable ("captures where AwayMetrics was null").

### Design

**0. The overwrite hazard (drove the final shape).** The picks-page
read is newest-non-rejected `MatchupPreview` per contest — so a testing
run that wrote a preview row for a contest which already has one from a
prior season would SHADOW the real preview instantly. Protection:
experiment runs never write `MatchupPreview` at all; their results live
on the capture row. (Real generation independently keeps its
completed-contest skip.) This also removed any need to un-hide the
admin approve/reject controls for completed games on the picks page —
the Preview Lab replaces that path entirely.

**1. Command mode** — `GenerateMatchupPreviewsCommand.Mode`:
`Generate` (0, default — old serialized payloads keep working, same
pattern as the Sport default), `Capture` (prompt persisted, no model
call), `Experiment` (model called; raw response + model name +
parse/validation problems recorded on the capture row; no
MatchupPreview, no PreviewGenerated event). Capture and Experiment
allow completed contests; Generate does not.

**2. Processor refactor** — extract payload assembly (client fetches →
both-or-nothing → hasStats → prompt selection → serialize → editor
note) into one method returning the assembled prompt parts. Both paths
(capture and real) use it — the capture is byte-identical to what
generation would send, by construction. In capture mode: persist the
capture row, SKIP the LLM call, the `MatchupPreview` row, and the
`PreviewGenerated` event.

**3. New entity `MatchupPreviewPrompt`** (API Postgres):

| Field | Notes |
|---|---|
| Id, ContestId, Sport | |
| MatchupPreviewId (nullable FK) | null for dry-run captures; set when a real generation wrote a preview |
| PromptVersion | blob name, as today |
| PromptText | instruction text EXACTLY as sent — stored per capture because a blob can be edited in place, which would make version-based reconstruction lie (CR finding on #601) |
| PayloadJson (jsonb) | the serialized matchup DTO — the data part |
| EditorNote (nullable) | rejection-feedback text if it was appended |
| CharCount, EstTokens | chars/4 estimate — pre-flight budget visibility |
| Mode | Generate / Capture / Experiment |
| Model, RawResponse, ResponseValidationErrors | model-call runs; RawResponse is deliberately `text` NOT `jsonb` — malformed responses are exactly the failures an experiment must record |
| CreatedUtc, CorrelationId | |

Originally the plan stored only PromptVersion + payload and
reconstructed via the blob — rejected during review: `ReloadPromptAsync`
can replace a blob in place, so version-based reconstruction could show
text that differs from what the model actually received. Each capture
stores its instruction text; the DB is private, and ~10 KB per row at a
few thousand rows/season is noise. Full prompt = PromptText + "\n\n" +
PayloadJson + EditorNote, all from the row.

**4. Admin capture endpoint** — new
`POST /admin/matchup/preview/{contestId}/capture?sport=...`, ASYNC via
Hangfire, matching the established admin pattern (decision 2026-08-07:
toast on submit → SignalR notification on completion, exactly like
regeneration). Capture completion publishes a lightweight event through
the same notification path. Retrieval is a companion
`GET /admin/matchup/preview/{contestId}/captures` (list, newest first)
returning payload + metadata, with the full prompt reconstructed
(blob text + payload + editor note) so the admin sees exactly what the
model would receive. The existing `/reset` endpoint stays untouched.

**5. Always-on persistence** — the real generation path writes the same
`MatchupPreviewPrompt` row (linked to the preview) in the same
SaveChanges. From then on every preview carries its exact inputs —
which is also the corpus for the 3.5 projection work: capture today's
raw-DTO payloads, build the projection, diff.

### Decisions (made 2026-08-07)

1. **Capture mode ALLOWS completed contests.** Rationale goes beyond
   local testing: completed games + captured payloads + actual outcomes
   (already on Contest) form an EVAL HARNESS — replay a stored payload
   against model X / prompt-variant Y and score the prediction against
   reality. Expectation: different data shapes and instructions will
   perform differently per model; this table is what makes that
   measurable. (Real generation keeps its completed-contest skip.)
2. **Async capture** via Hangfire + SignalR completion notification —
   consistent with the existing admin regeneration UX (toast → SignalR).
   Retrieval via the GET captures endpoint.
3. **Keep forever.** The captures are the backtest corpus, not debug
   debris.

Eval-harness note (future, separate effort): a backtest run =
`MatchupPreviewPrompt.PayloadJson` × {model, prompt blob} →
prediction vs `Contest` final score / spread result. The capture table
deliberately stores everything that run needs; the harness itself is
not part of this build.

### Admin UI — Preview Lab (built with the above, 2026-08-07)

`/admin/preview-lab` (AdminRoute-gated): league selector + ContestId
input (grab the GUID from the picks page), actions **Capture prompt**
and **Run experiment**, list of captures per contest with mode badge,
prompt version, model, est. tokens, expandable full-prompt / payload /
raw-response blocks (copy buttons), and validation problems surfaced.
SignalR `PreviewPromptCaptured` completion auto-refreshes the list.
Endpoints: `POST .../capture`, `POST .../experiment`,
`GET .../captures` on AdminController. The picks-page Matchup Preview
logic is untouched by design.

---

## 4. Data availability (verified against the corpus work, 2026-08)

- Play-by-play-derived metrics corpus: FBS NCAA 94–99.9% for 2008–2025;
  NFL near-complete 2006–2025. Prior-season metrics for 2026 previews
  (i.e. 2025 data) are in the strongest part of the corpus.
- **Verify before build:** whether `FranchiseSeasonMetric` season
  aggregates were actually GENERATED for prior seasons (the corpus work
  confirmed CompetitionMetric coverage; the season-aggregate rows for
  historical seasons need a count check per sport/season). If absent, a
  one-time backfill job precedes 3a.
- Head-to-head (3b) and prior-season results (3c) need only Contest
  rows — coverage is effectively complete for both sports.

---

## 5. Open decisions

1. N for head-to-head (proposed 5) and recency bridge (proposed 5 games).
2. One combined Producer "preview history" endpoint vs. composing existing
   per-piece endpoints (proposed: one endpoint — one HTTP hop, one place
   to evolve).
3. Payload-hygiene projection (3e) first, or accept token growth and do it
   later (proposed: first).
4. Prompt authoring workflow for with-history-v1. Prompts stay blob-only
   (secret sauce, public repo — decided 2026-08-07); author against local
   gitignored working copies, upload to blob, PromptVersion tracks it.
5. Whether the DTO gains blocks (wire change through Producer client) or
   the API composes history separately — follows from decision 2.

## Sequencing

1. ~~Prompt capture~~ — done 2026-08-07 (local working copies only;
   blob remains source of truth).
2. Verify prior-season FranchiseSeasonMetric row coverage — SQL ready in
   `sql/pgsql/_debug_preview_history.sql` (query 3), run per sport DB.
3. Decisions 1–5 above.
4. Design doc v2 with endpoint/DTO shapes → authorization → build.
