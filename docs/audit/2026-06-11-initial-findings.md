# MatchupPreview Audit — Initial Findings

**Started:** 2026-06-11
**Scope:** NCAAFB 2025 season — approved `MatchupPreview` rows joined
against actual `Contest` outcomes via the new public retrospective at
`/results/football/ncaa/2025`.

## Headline numbers

First pass from the rendered audit page:

| Market | Hit rate |
|--------|----------|
| Straight Up (SU) | **71.8%** |
| Against the Spread (ATS) | **46.5%** |

(W-L totals to be backfilled — these are eyeballed from the hero record
on the page; need to confirm against the API response.)

## What the numbers actually mean

These two rates aren't contradictory. They're the **canonical signature
of a naïve favorite-detector colliding with a market designed to
neutralize that exact signal.**

- **71.8% SU is genuinely strong.** ESPN expert panels rarely break
  65%; 70%+ means the model has a high-quality read of which side is
  better in any given matchup.
- **46.5% ATS is worse than coin-flip.** Break-even at -110 juice is
  52.4%. We're ~5.9 points under break-even — at $1 flat per game,
  this loses real money.
- **Read together: the model is picking favorites.** It's identifying
  the same fundamental quality differential the sportsbooks have
  already priced into the line. SU lifts because the better team wins
  ~72% of games. ATS sinks because the spread is the market's estimate
  of HOW MUCH better, and the model isn't reaching past the spread to
  find game-specific edge.

This is the most common naïve-model fingerprint, not a code bug. The
prompts are the lever, not the architecture.

## Audit angles, in decreasing order of expected value

### 1. Favorite rate

What percentage of our picks are the favorite vs the underdog?

- **≥90%** → we've built a high-quality favorite-detector that the
  market already priced. The prompt isn't reaching for edge. The
  prompt-engineering target becomes: push the model to explicitly
  identify game-specific edge — injuries, weather, lookahead spots,
  line moves, road-after-emotional-win spots, conference-revenge
  contexts — rather than restate what the spread already encoded.
- **70–80%** → the model picks dogs sometimes, but those picks aren't
  beating the line either (since the overall ATS is sub-50%). Drill
  into angle #3 below.

**Measure this first.** Everything else is downstream.

### 2. ATS by spread size

Bucket games by spread magnitude and recompute ATS hit rate per
bucket:

- `0.0 – 3.0` (pick'em / 1-score game)
- `3.5 – 7.0` (one-score, light favorite)
- `7.5 – 14.0` (clear favorite)
- `14.5+` (blowout territory)

**Hypothesis:** naïve models are decent on tight spreads and brutal on
large ones. With a 14+ spread, the favorite is "obvious" — so the
model picks it — but the line already accounts for the obvious *plus*
typically prices in some garbage-time backdoor coverage.

**If the bucket breakdown confirms ATS gets monotonically worse as
spread size grows, that's actionable.** A simple intervention: bias
the prompt to *decline* picks where the spread exceeds some threshold.
That mechanically lifts overall ATS without making the model smarter,
just less promiscuous.

### 3. ATS on underdog picks only

When we DO pick a dog, what's the ATS hit rate on just those picks?

- **>52.4%** → the model finds real value on the dog side, rarely but
  measurably. Surface these picks distinctly; they're the closest
  thing to alpha the system currently has.
- **≤50%** → the dog picks are essentially noise. Either fold them
  (always pick favorite) or rethink *why* we pick dogs at all.

### 4. Confidence vs hit rate (calibration)

If the preview includes any confidence signal — StatBot's lean, a
stated probability, a hedging adjective — bin picks by stated
confidence and plot actual hit rate per bin.

- **High-confidence picks beat ATS even when low-confidence ones
  don't** → there's a *usable subset*. Surface those distinctly in
  the UI; treat the rest as commentary, not prediction.
- **Confidence uncorrelated with outcome** → the signal is theater.
  Drop it or rebuild it.

Calibration is the #1 thing that separates "AI demo" from "AI
product." Even if overall ATS stays bad, a well-calibrated
high-confidence subset is shippable; an uncalibrated one isn't.

## Next concrete steps

1. **Pull the data into something queryable.** Either a SQL view that
   joins API's `MatchupPreview` with Producer's `Contest` for NCAAFB
   2025, or a notebook export. Need on every row:
   - `PredictedStraightUpWinner` vs `WinnerFranchiseId`
   - `PredictedSpreadWinner` vs `SpreadWinnerFranchiseId`
   - **Spread line at the time the preview was generated.** Closing
     line is the conservative fallback if line-at-publish isn't
     stored — but we should check whether `MatchupPreview` already
     captured a snapshot.
2. **Compute favorite rate** (angle #1). One number.
3. **Compute ATS-by-spread-bucket** (angle #2). Four numbers.
4. **Decide** whether to chase angles #3 and #4 based on what #1 and
   #2 reveal. If favorite rate is 95%+ and large-spread ATS is in the
   30s, the diagnosis is locked in and the next move is prompt work,
   not more analysis.

## What we're explicitly NOT doing yet

- **Money P/L overlay** on the retrospective page. Deferred per the
  original detour scope. Will land once we have a way to backfill
  line-at-publish (or accept closing line as the conservative
  approximation).
- **Sport expansion.** NCAAFB 2025 only. NFL has its own audit when
  we get there — same methodology, different data slice.
- **Prompt-engineering changes.** Diagnosis first, then targeted
  edits. Don't tune blindly.
- **Marketing this publicly as a feature.** The page is exposed
  without auth (intentional, to keep iteration friction low), but
  until the audit produces a usable subset of picks, it's honest
  observation — not a marketing claim.

## References

- Public retrospective page: `/results/football/ncaa/2025`
- Backend endpoint: `GET /ui/results/sport/football/league/ncaa/2025`
- Handler: `SportsData.Api/Application/UI/Results/Queries/GetSeasonResults/GetSeasonResultsQueryHandler.cs`
- Entity: `SportsData.Api/Infrastructure/Data/Entities/MatchupPreview.cs`
- Canonical match DTO from Producer (carries actual SU/ATS winners):
  `SportsData.Core/Dtos/Canonical/LeagueMatchupDto.cs`
