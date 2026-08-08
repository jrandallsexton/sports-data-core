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
  FranchiseSeasonId). Rule: **historical blocks contribute ZERO GUIDs**
  (names/labels only — implemented and regression-tested in the
  preview-history PR). Today's raw payload therefore carries exactly
  three GUIDs: ContestId plus the two live Away/Home
  FranchiseSeasonIds. The 3e projection below drops ContestId (the
  model has no use for it), landing on two.

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

## 3.6 Prompt capture + persistence (designed 2026-08-07, implemented in PR #601)

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
(persisted PromptText + PayloadJson + EditorNote — no blob round-trip,
matching BuildCapture) so the admin sees exactly what the
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

### ⚠️ First prod capture finding (2026-08-08): the answer leaks into completed-game payloads

Captured the Hall of Fame Game (completed) via the lab. The payload's
`Away/HomeCompetitionResults` contain **the target game itself** — same
ContestId, final score 33-30, `WinnerFranchiseSeasonId`,
`SpreadWinnerFranchiseSeasonId`, `OverUnderResult` — plus
`Status: "STATUS_FINAL"`. An experiment against any completed game hands
the model the answer; every such result is invalid as an eval signal.
In-season real generation never hits this (completed-contest skip =
generation is always pre-game), so this is an EXPERIMENT-MODE problem.

**FIXED (2026-08-08, follow-up PR):** assembly now applies the as-of
rule to both CompetitionResults lists in ALL modes — no game with
`StartDateUtc >= target.StartDateUtc` reaches the payload (a no-op for
pre-game generation, so captures stay byte-identical to real sends) —
and Capture/Experiment runs on completed contests mask
`Status`/`StatusDescription` to Scheduled, recreating the pre-kickoff
information state. Eval numbers from the lab are trustworthy from this
point. (Same PR: history SQL emits `Franchise.DisplayName` so
historical rows string-match the live team names exactly.)

Other confirmations from the same capture (§3.5 hygiene case, now
quantified): 28 serialized null stat fields across the two teams,
`"Sport": 3` as a bare int the prompt can't interpret (user 2026-08-08:
resolve — string enums in the projection), six-decimal odds
(`34.500000`), and the same result row duplicated verbatim in both
teams' lists. And the no-stats path selected `prediction-insights-v1`,
which never documents CompetitionResults — the model received schedule
data the v1 prompt gives no guidance on.

**POLICY — NFL preseason is system-testing data ONLY** (user decision
2026-08-08). A finalized preseason game DOES flow into current-season
CompetitionResults — but preseason is a proving ground for the roster
bubble (final 53-man cuts land Aug 30); outcomes say nothing about the
September team. Decision: preseason results are **excluded, not
labeled** — they NEVER appear in matchup-preview payloads (season
results, recency bridge, head-to-head — note the Hall of Fame Game
itself is a preseason "meeting" and must not count in CAR/ARI H2H) and
NEVER contribute to anything metrics-based, including the Python
scripts that power the DeetsMeter. Their only value is exercising the
live pipeline before the season.

Scoping note for the build: filter at the preview/metrics CONSUMPTION
layer (SeasonPhase on the game), not blanket at Producer's
competition-results endpoint — user-facing schedule surfaces (team
cards) legitimately show preseason games.

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

1. ~~N for head-to-head and recency bridge~~ — DECIDED 2026-08-08: 5 and
   5 (defaults on `GetContestPreviewHistoryQuery`). **Implemented**: 3b
   (head-to-head) + 3c (recency bridge) shipped as
   `GET /contests/{contestId}/preview-history` with preview-safe
   semantics in the SQL (finalized/non-cancelled, preseason excluded per
   policy, as-of `< target.StartDateUtc` so the target never leaks into
   its own history). New blocks use the names-only GameResult shape —
   the payload's only GUIDs remain ContestId + the two live
   FranchiseSeasonIds (regression-tested). 3a (prior-season
   record/metrics block) still pending.

   **Expanded 2026-08-08 (user PoC):** every historical row also carries
   MARKET CONTEXT — Spread details text, home-relative HomeSpread +
   HomeSpreadOpen (line movement), OverUnder + OverUnderOpen, Over/Under
   odds — via the preferred-provider odds lateral, using the live
   payload's exact vocabulary so one shape reads everywhere. Null for
   pre-odds-era games (~pre-2022): result-only context degrades
   honestly. ProviderName deliberately excluded (provenance, not
   signal); alternate-provider previews are just a new run against that
   provider's data.
2. ~~One endpoint vs compose~~ — DECIDED: one endpoint (implemented as
   above).
3. ~~Payload-hygiene projection (3e)~~ — **IMPLEMENTED 2026-08-08**
   (`SerializePromptPayload` in the processor; the wire DTO is untouched
   — hygiene is applied at serialization only, so no consumer risk):
   omit-null (kills the ~28 null stat fields and null market fields on
   old rows), string enums (`"Sport": "FootballNfl"`, O/U results as
   words), top-level `AwaySpread` dropped (user: spread is ALWAYS
   home-relative; the away value is a derived negation), `ContestId`
   dropped, AND legacy CompetitionResults rows stripped of their five
   per-row GUIDs (contest, both franchise-seasons, winner,
   spread-winner) + per-row `AwaySpread` — winner/cover remain derivable
   from slugs + scores + HomeSpread. Regression test: the entire payload
   now contains EXACTLY the two live FranchiseSeasonIds and no other
   GUID. Remaining prompt-side task: drop `AwaySpread` from the example
   inputs during with-history authoring.
4. Prompt authoring workflow for with-history-v1. Prompts stay blob-only
   (secret sauce, public repo — decided 2026-08-07); author against local
   gitignored working copies, upload to blob, PromptVersion tracks it.
5. ~~DTO gains blocks vs API composes~~ — DECIDED: `MatchupForPreviewDto`
   gained nullable HeadToHead / AwayPriorSeasonGames /
   HomePriorSeasonGames; the API's assembly populates them from the
   preview-history endpoint with graceful degradation (history fetch
   failure logs and proceeds without blocks — never fails generation).

## Sequencing

1. ~~Prompt capture~~ — done 2026-08-07 (local working copies only;
   blob remains source of truth).
2. Verify prior-season FranchiseSeasonMetric row coverage — SQL ready in
   `sql/pgsql/_debug_preview_history.sql` (query 3), run per sport DB.
3. Decisions 1–5 above.
4. Design doc v2 with endpoint/DTO shapes → authorization → build.

---

## 6. Remaining work (consolidated 2026-08-08)

Everything above through §3a/3b/3c/3e is SHIPPED. What's left, in
recommended order (season opens ~Aug 30; goal is a defensible
model/prompt choice before real generations start):

1. **§3a prior-season block — IMPLEMENTED with one deferral**: final
   record + prior-season FranchiseSeasonMetrics now flow (both-or-nothing
   applied in API assembly; records always flow). DEFERRED: prior-season
   final poll rank (NFL has no polls; NCAA final AP rank needs a
   rankings-table join — add during NCAA-season tuning if the model
   seems to need pedigree signal). GATE STILL OPEN: run the coverage
   check (query 3 in `sql/pgsql/_debug_preview_history.sql`) per sport
   DB — if 2025 FranchiseSeasonMetric rows are missing, a one-time
   backfill is needed; until then the prior-season RECORD still flows —
   only the null `Metrics` property inside the block is omitted
   (omit-null) — so the payload degrades honestly.
1a. **Prompt provider refactor — IMPLEMENTED 2026-08-08** (the
   "refactor prompt providers" whiteboard item; revised same day per
   user decisions: prompts are Guid-identified DB ENTITIES, text
   included). New `Prompt` table in API's Postgres (private — the
   secret-sauce policy holds): Id, Name (unique; becomes PromptVersion
   on captures), Type (MatchupPreview / future GameRecap), Sport
   (null = any), WithStats, IsDefault, Description, Text. The DB
   storing text removes Azure blob storage from the preview pipeline
   entirely — no Azurite for local dev (the exact blocker from the
   first E2E attempt), and the coming prompt-management UI is plain
   CRUD. Text formatting in Postgres is a non-issue (proven by
   MatchupPreviewPrompt.PromptText since #601); CRLF normalized to LF
   on create for deterministic diffs.

   `IMatchupPreviewPromptProvider` resolution: (1) explicit `PromptId`
   (Guid) → that row; unknown id or wrong Type fails loudly, never
   falls back; honored in Capture/Experiment ONLY — Generate always
   resolves the default, so an experiment override can never leak into
   production previews. (2) Default for the (Sport, WithStats) slot —
   sport-specific outranks Sport=null; flipping defaults takes effect
   next run, no deploy. No caching (scoped indexed read; the blob-era
   fault-eviction machinery is gone).

   Captures now stamp PromptId (Guid) alongside PromptVersion + text.
   Admin endpoints: POST /admin/prompts (create; IsDefault flips the
   slot), POST /admin/prompts/import-blob (ONE-TIME seeding from the
   legacy container), GET /admin/prompts (+ /{id}). Preview Lab's
   Prompt ID input takes the Guid.

   **DEPLOY GATE: seed before the next preview cycle** — the provider
   is DB-only; with no default rows, generation fails with an explicit
   message. Seed via import-blob: `prediction-insights-v1` (WithStats
   false, IsDefault true) and `prediction-insights-with-stats-schedule`
   (WithStats true, IsDefault true), both Sport=null.
   GameRecapPromptProvider deliberately untouched (still blob) — unify
   when recap work resumes.

   **Follow-up (2026-08-08, deployed + seeded — 3 blobs imported):
   used-prompt immutability.** `MatchupPreview.PromptId` (nullable Guid
   FK → Prompt, Restrict delete; the processor stamps it on every new
   generation). Transition plan: operator SQL backfills historical rows
   from PromptVersion, THEN a later migration makes the column
   non-nullable. Rule (enforced in UpdatePromptCommandHandler + shown in
   the Prompt Manager as USED badges / read-only editor): **a prompt
   whose Id appears in MatchupPreview.PromptId is IMMUTABLE — and
   undeletable via the FK** — its text is provenance for real output;
   iterate via "New version". Experiments (capture rows) do NOT freeze a
   prompt — the lab iterates freely. Prompt Manager also gained explicit
   Open / per-row New version actions.

2. **with-history prompt blob** (user authors; blob-only per policy).
   Must teach: the metrics block (un-briefed since forever), HeadToHead
   + prior-season blocks (incl. "history excludes preseason by
   construction"), roster-churn discounting (NCAA > NFL), sport-aware
   voice (no more "college football analyst" for NFL), updated example
   inputs (no AwaySpread, string Sport, hygiene shape). New blob name =
   provenance; selection logic gains the with-history variant.
3. **Backtest harness MVP**: batch experiment enqueue ("every 2025
   week-N game") + a scoring query joining MatchupPreviewPrompt captures
   to Contest outcomes — SU accuracy, ATS accuracy, O/U accuracy, score
   MAE, grouped by PromptVersion × Model. Mostly SQL over existing
   tables; the lab already persists everything needed.
4. **Model routing for experiments**: optional model override on
   GenerateMatchupPreviewsCommand (capture rows already record Model) so
   the harness compares prompt × model, not just prompts.
   IProvideAiCommunication is single-bound today; see
   ai-provider-routing-per-sport.md for prior art.

Deliberately NOT doing now: Preview Lab UX polish (compare/diff views —
curl-grade is fine for a single operator); NCAA-specific H2H tuning
(sparse cross-season meetings are honest data); the Python
metrics-service question (parked until previews flow — see memory);
per-row odds provider selection (a different book's preview = a new
run against that provider's data).
