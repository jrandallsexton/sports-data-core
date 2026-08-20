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

## Model-payload injection (v1.1)

`MatchupForPreviewDto.SpreadContext` carries the same block into the
preview/insight prompt payload (`MatchupPreviewProcessor` copies it from
the history fetch; the payload serializer's omit-null and GUID-hygiene
rules apply — the block is names-and-numbers only, zero GUIDs). Because
the facts are as-of-capped in Producer, capture/experiment runs on
completed games stay leak-free.

**Operator note:** the payload provides the facts; the PROMPT (blob-
stored, never in this repo) decides what the model does with them.
To activate the narrative angle, update the preview prompt in the
Prompt Lab to instruct the model to cite `SpreadContext` facts verbatim
when discussing the line — the model reads facts, never computes them.

## Future

- League-wide base rate ("35+ favorites cover X% overall") as the
  anchor each team's number stands against.
- More family members: cover streaks vs. opponent class, totals-based
  facts ("teams hitting this O/U").

See memory `project_spread_contextualized_history` for the full product
rationale and origin (2026-08-19, immediately post-#658).
