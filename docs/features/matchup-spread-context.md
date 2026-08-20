# Matchup Spread Context ("The Line")

Spread-conditioned historical facts surfaced proactively in the team
comparison dialog's History tab — answering the questions a user wouldn't
think to ask. For a USC -38.5 line over San José State:

1. When is the last time USC beat **anyone** by 38.5+? (and who was it —
   a bowl team or a 3-9 doormat?)
2. When is the last time anyone beat SJSU by 38.5+?
3. How has USC done ATS as a 35+ favorite? SJSU as a 35+ underdog?

Product positioning: **a dimension, not a toll booth.** The casual picks
USC and moves on at full speed; the data geek gets the bigger picture one
tap away. Facts are **context, never prediction** — every number comes
from a query, none from prose, which keeps the killer detail
hallucination-proof.

## Data flow

Computed in Producer inside the existing preview-history pipeline
(`GetContestPreviewHistoryQueryHandler`) and attached to
`ContestPreviewHistoryDto.SpreadContext`, so it reaches every consumer of
`GET contests/{id}/preview-history`: the API relay
(`/api/{sport}/{league}/contests/{contestId}/history`) feeding web +
mobile, and (future) the preview/insight model payload.

`SpreadContext` is **null when the target contest has no line** from the
preferred/fallback odds providers (EspnBet 58 → DraftKings 100 — same
lateral as the matchup payload, so both surfaces quote the same number),
**and also when the selected line is zero** (a pick'em has no favorite to
condition on).

## Fact family and data tiers

| Fact | Tier | Floor | Source |
|------|------|-------|--------|
| `FavoriteWonByMargin` / `UnderdogLostByMargin` | score margins | franchise's earliest corpus season (returned per-fact as `SearchFloorSeason`) | `GetFranchiseMarginFact.sql` |
| `FavoriteAtsAsBigFavorite` / `UnderdogAtsAsBigUnderdog` | spread values | 2022 (`DataFloorSeason`) | `GetFranchiseAtsBucket.sql` |

Observed coverage (NCAAFB, 2026-08-19): spread **results**
(`SpreadWinnerFranchiseSeasonId`) exist from 2012 (~840 FBS games/yr);
spread **values** (`CompetitionOdds.Spread`, providers 58/100) from 2022.
Margin facts need neither — scores only.

### Margin facts (`PreviewMarginFactDto`)

- Exact-line semantics: "won by ≥ 38.5" uses the real magnitude, no
  bucketing — that IS the user's question.
- The qualifying game carries **opponent quality** one level deep: the
  opponent's record that season and the season before
  (`FranchiseSeasonRecord`, type `total`). Absent stays null — never a
  fabricated 0-0.
- `LastGame == null` means it never happened within the corpus — the
  headline case. The SQL always returns a row so the count (0) and the
  franchise's honest search floor survive.
- `CountLastFiveSeasons` bounds "how unusual is this" to a window.

### ATS bucket facts (`PreviewAtsBucketFactDto`)

- Bucketed on football **key numbers** `[3, 7, 10, 14, 21, 28, 35]`:
  largest key ≤ magnitude. "As a 35+ favorite" reads naturally and
  accrues a sample where "as a 38.5-point favorite" would be n=0.
  Magnitude < 3 ⇒ no ATS facts.
- `Games` counts **decided** ATS results only (`SpreadWinner` null =
  push or unsourced ⇒ excluded).
- Zero games is presented honestly: "no games with a line that large
  since 2022" — never implied as 0-for-0.

### Preview-safe predicates (all queries)

Finalized + non-cancelled only; strictly before the target's start (no
answer leak); preseason (`SeasonPhase.TypeCode 1`) excluded, NULL phase
kept — identical to the head-to-head/prior-season queries.

## Rendering

Web (`TeamComparison.jsx`) and mobile (`StatsComparisonModal.tsx`) render
"The Line — USC -38.5" at the top of the History tab as deterministic
sentences composed client-side from the structured facts:

> **Last time USC Trojans won by 38.5+:** Sep 6, 2025 — beat Georgia
> Southern Eagles 59-20 (they went 3-9; 2-10 the season before). 6 such
> wins in the last 5 seasons.
> **San José State Spartans as a 35+ underdog:** covered 1 of 1 (since 2022).

The entire section is **gated by `shouldShowGambling`** on both platforms
— the framing is spread-derived.

## Future (not in v1)

- Inject the fact chain into the preview/insight model payload
  (`MatchupPreviewProcessor`) so the LLM narrative can cite it —
  "model predicts, LLM explains" with pre-verified facts.
- More family members: cover streaks vs. opponent class, totals-based
  facts ("teams hitting this O/U"), favorite-cover-rate league-wide at
  this bucket as the base rate.

See memory `project_spread_contextualized_history` for the full product
rationale and origin (2026-08-19, immediately post-#658).
