# Competition Metrics Formula Audit

Date: 2026-08-13
Auditor: Claude (session with Randall), triggered by the MetricBot
v1.1.1 acceptance failure — a −5.5pt systematic prediction bias traced
through feature decomposition to the metric formulas themselves.
Scope per Randall: **what the current code computes and what is wrong
with it** — not archaeology about prior vintages.

Audited chain: `FranchiseSeasonController.GenerateFranchiseSeasonMetrics`
→ `EnqueueFranchiseSeasonMetricsGenerationCommandHandler` →
`CalculateFranchiseSeasonMetricsCommandHandler` (season aggregation) and
`CalculateCompetitionMetricsCommandHandler` (per-game formulas, where
the values are born).

## Empirical anchors (prod, 2026-08-13)

- FBS per-game PointsPerDrive by season: 2022 = 1.31, 2023 = 1.81,
  2024 = 1.43, **2025 = 5.93** — more than double the best FBS offense
  in modern history (elite ≈ 3.5; league reality ≈ 2.2–2.6), i.e. far
  outside any plausible range, and 3–4× the platform's own prior
  seasons.
  Live `FranchiseSeasonMetric` 2025 average: 6.16. The 2025 rows came
  from the current code below; the pre-2025 rows from an earlier
  vintage — per scope, only the current behavior is judged.
- FieldPosDiff per-game: home mean ≈ −37, away mean ≈ +37 (FBS-priced
  subset); |FPD| ≈ 17 all-division. A real field-position edge cannot
  be venue-signed.

## CRITICAL

### C1. PointsPerDrive — one-play drives credited with the whole scoreboard

`CalculatePointsPerDrive` computes drive points as
`score(lastPlay) − score(secondToLastPlay)`, and **when a drive has one
play, the baseline is 0** — so a one-play drive is credited with the
team's entire cumulative score at that moment. A kneel-down while
leading 42–7 = +42 points on one drive. One-play drives are common
(long TDs, kneels, end-of-half), and the error grows with the score,
which is why season averages reach ~6.

**Fix**: the baseline is the team's cumulative score immediately
before the drive's first play **in global game order** — the last play
by EITHER team preceding it (0 only at true game start). A same-team
predecessor is not sufficient: a defensive score during the opponent's
possession between two drives must be part of the baseline, or the
next drive absorbs those points. (#624 implements exactly this: the
baseline play is `ordered[driveFirstIndex − 1]` over ALL plays.)

### C2. FieldPosDiff — differences a stadium-oriented coordinate

`CalculateFieldPositionDiff` averages raw `CompetitionDrive.StartYardLine`
for own vs opponent drives and subtracts. ESPN's yard line is a
**fixed-orientation coordinate** (the same physical spot reads 30 for
one team and 70 for the other), so the difference measures which side
of the coordinate system each offense drives toward — producing the
±37 venue-signed artifact. Downstream this silently encoded
home-field itself into per-game training features (~16pts of implied
signal) and then vanished in season aggregates, driving MetricBot
v1.1's −5.5 bias.

**Fix**: normalize each drive start to own-goal-relative field position
(e.g. 100 − yards-to-endzone at the drive's first play, or orient
StartYardLine by offense direction) before differencing.

## HIGH

### H1. Offensive-snap filter excludes disaster plays

`IsOffensiveScrimmageType` includes Rush/Pass/Sack/Safety variants but
**excludes interception plays and lost-fumble plays**. Those are
offensive snaps with catastrophic outcomes; excluding them biases Ypp,
SuccessRate, ExplosiveRate, and ThirdFourthRate upward for
turnover-prone teams — the denominators skip exactly the worst plays.

**Fix (corrected 2026-08-13 — CR caught an error in the original
guidance)**: interception play types keep the intercepted OFFENSE in
`StartFranchiseSeasonId`, but `StatYardage` is the DEFENSIVE RETURN
yardage — adding these types to the snap filter as-is would credit
return yards to the offense. The correct contract:
- Interception and lost-fumble plays join the DENOMINATOR of
  Ypp/SuccessRate/ExplosiveRate/ThirdFourthRate as offensive snaps.
- Their NUMERATOR contribution is fixed at 0 yards / not-a-success /
  not-explosive — never `StatYardage`. (If per-type yardage semantics
  are later verified to carry usable offense yardage for FumbleLost,
  that is a separate, evidence-backed change.)
- Fixtures: an interception play must increase snap count without
  changing total yards; a 40-yard pick-six must not appear as a
  40-yard offensive gain.

**Implemented** (fix/metrics-h1-h2): `IsTurnoverType` added; those
types join `IsOffensiveScrimmageType`; a `Yardage(play)` accessor
returns 0 for turnover types and feeds every numerator (Ypp,
success/explosive checks, first-down-by-yardage). Both fixtures above
are in the test suite.

### H2. Red-zone trip state survives possession changes it shouldn't

Both RZ calculators end a trip only when the OTHER offense takes a
standing scrimmage snap. A trip left open at a quarter/half boundary,
or ended by a defensive score/kick, stays open until the opponent
snaps — and a later score in a NEW drive can be credited to the stale
trip (e.g. trip at end of Q2, team receives the second-half kickoff
and scores: the first-half trip is marked scored).

**Fix — complete termination contract** (a trip, once open, closes on
the FIRST of):
1. The opposing offense takes a standing scrimmage snap (existing rule).
2. THIS offense starts a NEW drive (DriveId change) — covers turnovers,
   defensive scores, and kickoff-separated possessions in one rule,
   since every one of those forces a new drive id.
3. A half boundary (period 2→3) or end of regulation→OT transition —
   possessions do not survive the half. (Q1→Q2 and Q3→Q4 do NOT
   terminate: a drive legitimately spans those.)
4. End of input (existing EOF close).
Scoring credited to a trip counts only while that trip is open.
Duplicate play events (same SequenceNumber) count once; missing events
degrade to rule 1/2 whichever fires first.

**Fixtures required**: trip → opponent snap; trip → turnover → own new
drive later scores (stale-trip regression); trip open across Q1→Q2
(must survive); trip open across the half (must close); trip ended by
defensive TD; trip at EOF; duplicate-sequence play.

**Implemented** (fix/metrics-h1-h2): both rates now delegate to one
shared `CountRedZoneTrips` state machine implementing rules 1–4
verbatim (half bucket: period ≤2 / ≤4 / OT; own-new-DriveId close
evaluated before a fresh trip can start on the same play; adjacent
duplicate SequenceNumber skipped). Trip-ended-by-defensive-TD is
covered by rule 2 (the defensive score forces a new drive id) and by
rule 1 now catching opponent interception snaps via the widened H1
type set. Fixture battery is in the test suite.

### H3. PenaltyYardsPerPlay attributes by possession, not by offender

Penalty plays are attributed to `StartFranchiseSeasonId` — the
possessing offense — so a defensive-offside against the OPPONENT during
your drive counts as YOUR penalty yards, and your own defensive
penalties are never counted. `Math.Abs` also erases direction.

**Contract (data limitation verified 2026-08-13)**: ESPN's play
document carries only the possessing/acting `Team` ref (→
`StartFranchiseSeasonId`); there is NO structured penalized-team field
— the offender exists only in free text. Therefore:
- **DECIDED default (pending Randall veto): REMOVE PenaltyYardsPerPlay
  from FEATURE_COLS and the preview payload.** A metric that cannot
  attribute its subject is noise with a name. Text-parsing the offender
  is rejected (fragile, unverifiable at scale).
- The column may remain persisted as "penalty yards observed during
  own possessions" ONLY if renamed to say so; otherwise stop computing
  it. No `Math.Abs`: if it is ever reintroduced with real attribution,
  yards are signed by beneficiary.
- Fixtures on any reintroduction: offensive penalty on own drive,
  defensive penalty on own drive, declined penalty (no yardage), and
  NO-PLAY interaction.

## MEDIUM

Each medium finding now carries ONE deterministic target:

- **M1. TurnoverMarginPerDrive — target: verified attribution or
  exclusion.** Acceptance: a verification query comparing computed
  turnovers-lost/gained against `CompetitionCompetitorStatistic` box
  totals for ≥100 sampled 2025 games; ≥95% exact match → keep with the
  play-type list extended to whatever the mismatches reveal (e.g.
  opponent-fumble-recovery types); below → remove from FEATURE_COLS
  until fixed. Fixture: a game with one INT each way + one lost fumble,
  hand-counted.
- **M2. TimePossRatio — target: OT contributes zero possession
  seconds.** `secondsRemaining = Math.Max(0, 4 − period) * 900 + clock`
  clamps OT periods to clock-only; since CFB/NFL OT possession time is
  not meaningfully clocked in the data, OT plays contribute 0 elapsed
  and the ratio is a REGULATION possession ratio (documented as such).
  Last-play duration remains excluded (accepted approximation).
  Fixture: an OT game whose ratio equals its regulation-only ratio and
  is in [0,1].
- **M3. FgPctShrunk — target: null when no qualifying attempts.**
  The result is null (SafeAvg-carried, omitted from payloads) when a
  team has zero ≤45yd attempts — never 0%. The shrinkage prior implied
  by the name is DEFERRED to a future formula vintage; until then the
  name stays (renaming is churn) with this doc as the honest record.
  Fixtures: 0 attempts → null; 2/3 made → 0.6667.
- **M4. NetPunt — target: removed from FEATURE_COLS and payloads.**
  A hardcoded 0 is a dead constant to models and a lie in payloads.
  The column may persist as null until punting stats are implemented
  (likely from box-score data per franchise-season-week-metrics.md §5).
  Fixture: payload serialization omits it.

### H4. Lexicographic ordering of an ESPN string SequenceNumber
(found implementing #624)

`CompetitionPlayBase.SequenceNumber` is a STRING, and the handler
orders plays with `OrderBy(p => p.SequenceNumber)` — lexicographic,
where "10" sorts before "9". Every ordered computation (drive
first/last plays, possession-time endpoints, red-zone trip scanning)
is exposed if ESPN's values vary in digit count. #624's new
PointsPerDrive orders numerically-with-fallback; the REST of the
handler still sorts lexicographically.

**Target**: one shared numeric-aware ordering helper used by every
ordered computation in the handler; a verification query measuring how
often numeric and lexicographic order actually disagree in prod data
(if never, the fix is cheap insurance; if often, it re-ranks severity).

## Aggregation layer (`CalculateFranchiseSeasonMetricsCommandHandler`)

- Plain unweighted mean of per-game ratios (average-of-ratios).
  **DECIDED: preserved unchanged in the recompute vintage** —
  changing weighting mid-campaign would confound the formula fixes.
  Small-denominator games keeping equal weight is accepted and
  recorded. Fixture: 2-game aggregate equals the hand mean.
- `SafeAvg` all-null → 0 quirk. **DECIDED: preserved in this vintage**
  (formula-parity is locked into the as-of SQL; changing it means
  changing two codebases in lockstep). Revisit with the weighting
  question in a future vintage, as one deliberate change. Fixture:
  all-null RZ inputs → 0 (current contract), asserted so any future
  change is loud.
- Delete+re-add identity churn on recompute — the same pattern
  #617 removed from the records processor; candidate for the same
  upsert treatment.
- `DateTime.UtcNow` used directly (house rule: `IDateTimeProvider`).
- `InputsHash = null` at both persist sites and **no
  AggregationVersion** — with formulas about to change, every recompute
  MUST stamp a version or cross-vintage drift recurs invisibly
  (see franchise-season-week-metrics.md provenance section).

## Blast radius

`CompetitionMetric` → `FranchiseSeasonMetric` → (a) MetricBot training
AND slates (v1.0/v1.1 both poisoned), (b) the LLM preview payload's
stats + prior-season Metrics blocks (the model has been reading
PPD ≈ 6.16 as fact), (c) any DeetsMeter/metric surface.

## Recompute contract

- **Identity**: natural key `(CompetitionId, FranchiseSeasonId)` for
  CompetitionMetric; `(FranchiseSeasonId, Season)` for
  FranchiseSeasonMetric. Verified 2026-08-13: NO code references
  `CompetitionMetricId` as a foreign key — delete/re-add identity churn
  breaks nothing today, but recompute handlers should still write
  delete+insert in ONE SaveChanges (the current per-game handler uses
  TWO — delete saved before insert — a crash between loses the rows;
  same non-atomicity #617 removed from the records processor).
- **Idempotency**: recompute of an unchanged competition under the same
  formula version is a no-op in effect (identical values, new stamp
  only if absent).
- **Version stamp — ONE field, one name**: `FormulaVersion` (string,
  e.g. "2026.08"), persisted on BOTH CompetitionMetric and
  FranchiseSeasonMetric, bumped when EITHER the per-game formulas or
  the aggregation change (they ship as one vintage; split fields buy
  nothing and invite mismatched interpretation). Readers: the
  verification gates below, the MetricBot harness (reports the vintage
  it trained on), and any future drift check. The earlier
  `AggregationVersion`/`MetricFormulaVersion` naming in
  franchise-season-week-metrics.md is superseded by this single field.
- **InputsHash**: populated at computation time as SHA-256 over the
  ordered source-play identity list (EspnId + final scoreboard pair);
  consumer contract: recompute may SKIP a competition whose stored
  (InputsHash, FormulaVersion) both match — the cheap-idempotency path.
  Until populated, recompute treats every row as stale (correct,
  slower).

## Recommended sequence (with gates)

1. Fix C1 + C2 (#624 — landed with hand-computed SYNTHETIC fixtures;
   a real play-by-play fixture game with independently verified answers
   remains follow-up coverage) and land the H/M decisions above in
   `CalculateCompetitionMetricsCommandHandler`, with the listed
   fixtures. Add `FormulaVersion` + `InputsHash` stamping (migration).
2. **Phase 1 recompute**: CompetitionMetric for every competition with
   plays, oldest season first. GATE before phase 2: row count = 2 ×
   competitions-with-plays; 100% stamped with the new FormulaVersion;
   sanity bounds on FBS season means (PPD in [1.5, 3.5];
   |mean FieldPosDiff| < 3; SuccessRate in [0.30, 0.55]). Any gate
   failure halts the campaign — nothing downstream recomputes.
3. **Phase 2 recompute**: FranchiseSeasonMetric strictly AFTER phase 1
   passes (the season handler reads persisted CompetitionMetric rows —
   ordering is load-bearing, not stylistic). GATE: per-season row
   counts match franchise-seasons-with-metrics; spot parity checks vs
   hand-aggregated samples; 100% version-stamped.
4. Only after both gates: re-run the MetricBot acceptance sweep (pure
   model and market-prior re-graded on honest features), regenerate any
   payload-facing consumers, and resume model iteration (v1.2:
   consistent as-of aggregate features on both sides).
