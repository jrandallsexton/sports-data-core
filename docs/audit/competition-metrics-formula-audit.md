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
  2024 = 1.43, **2025 = 5.93** (physically impossible; ~2.2–2.6 real).
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

**Fix**: the baseline must be the score BEFORE the drive's first play —
i.e. the score at the last play of the team's game-position predecessor
(0 only at true game start). Compute per-drive points game-ordered, not
within-drive-ordered.

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

**Fix**: include `PassInterceptionReturn` (and interception-TD),
`FumbleLost` (when the possessing offense is the team) as snaps with
their actual yardage outcomes (typically ≤ 0 for the offense).

### H2. Red-zone trip state survives possession changes it shouldn't

Both RZ calculators end a trip only when the OTHER offense takes a
standing scrimmage snap. A trip left open at a quarter/half boundary,
or ended by a defensive score/kick, stays open until the opponent
snaps — and a later score in a NEW drive can be credited to the stale
trip (e.g. trip at end of Q2, team receives the second-half kickoff
and scores: the first-half trip is marked scored).

**Fix**: also close the trip when THIS offense starts a new drive
(DriveId change) or on period boundaries.

### H3. PenaltyYardsPerPlay attributes by possession, not by offender

Penalty plays are attributed to `StartFranchiseSeasonId` — the
possessing offense — so a defensive-offside against the OPPONENT during
your drive counts as YOUR penalty yards, and your own defensive
penalties are never counted. `Math.Abs` also erases direction.

**Fix**: attribute by the penalized team if the data supports it; if
not, rename/redefine the metric honestly ("penalty yards observed
during own possessions") or drop it from the feature set.

## MEDIUM

- **M1. TurnoverMarginPerDrive**: relies on play-type attribution
  (`StartFranchiseSeasonId` on interception/fumble plays) that should
  be verified empirically against box scores; likely misses
  opponent-fumble-recovery play types. Denominator (own offensive
  drives) is unusual but internally consistent.
- **M2. TimePossRatio**: `(4 − period) * 900` goes negative in
  overtime (period 5+), corrupting OT games' possession seconds; the
  final play of each drive contributes zero elapsed time.
- **M3. FgPctShrunk**: no shrinkage despite the name (a raw filtered
  percentage), and zero attempts returns 0 — scoring a team with no
  short-FG attempts as a 0% kicker. Should be null (SafeAvg-style) or
  a shrunk prior.
- **M4. NetPunt**: hardcoded `0m` TODO — a dead constant feature.
  Harmless to models (zero variance) but misleading in payloads.

## Aggregation layer (`CalculateFranchiseSeasonMetricsCommandHandler`)

- Plain unweighted mean of per-game ratios (average-of-ratios). This is
  a documented, defensible choice — but note small-denominator games
  (few snaps/drives) get equal weight with full games.
- `SafeAvg` returns 0 (not null) when every game is null — known quirk,
  formula-parity-locked into the as-of SQL; revisit deliberately.
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

## Recommended sequence

1. Fix C1 + C2 (and decide H1–H3) in
   `CalculateCompetitionMetricsCommandHandler`; add
   `MetricFormulaVersion` stamping; unit-test each formula against a
   hand-computed fixture game (real play-by-play JSON, known answers).
2. Recompute ALL CompetitionMetric + FranchiseSeasonMetric under the
   fixed code (the audit-job idiom is the backfill mechanism) — one
   vintage everywhere.
3. Re-run the MetricBot acceptance sweep — pure model and market-prior
   both re-graded on honest features. Only then resume model iteration
   (v1.2: consistent as-of aggregate features on both sides).
