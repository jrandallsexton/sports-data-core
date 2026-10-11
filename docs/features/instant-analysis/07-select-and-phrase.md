# 07. Select and phrase

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** done 2026-10-10 (uncommitted)

**Depends on:** [05 Game facts](05-game-facts.md), [06 Season facts](06-season-facts.md)

**Feature doc:** [Stage 4: select](instant-analysis.md#stage-4-select) · [Stage 5: phrase](instant-analysis.md#stage-5-phrase) · [Principles](instant-analysis.md#principles)

## Goal

Turn candidate facts into ordered, rendered sentences: everything correct flows.

## Scope

- Every correct fact flows in v1, including firsts and repeats; nothing is dropped for being ordinary.
- Fact-type priority orders a play's facts for display; it never filters (priorities **in code**, decision 5).
- The freshness guard: drop a fact whose source is missing or stale, with the reason recorded for logging.
- Templates with typed tokens, **in code** (decision 2), versioned (`TemplateVersion`).

**Out of scope:**

- Weak-pattern filters (task 15).
- Database-backed templates (task 15).
- LLM rephrasing (task 16).

## Deliverables

- Selection and rendering components.
- Unit tests for ordering, the freshness guard, and each template.

## Acceptance criteria

- [x] Given several facts for one play, all are kept and ordered by priority.
- [x] A fact with a stale source is dropped and its reason is available to the logger.
- [x] Rendering is deterministic: the same facts always give the same text.

## Outcome (2026-10-10)

`FactSelector.cs` (select and order) and `FactPhraser.cs` (templates), both pure, in
`src/SportsData.Producer/Application/Competitions/InstantAnalysis/`. The analysis job loads names and each team's
previous game, selects, and logs every selected and dropped fact at Information level (persistence is task 08).

- **Every correct fact flows.** The only drops: the freshness guard, and a fact with no name or no template.
  Each drop carries a reason.
- **Priority (lowest first), in code:** `SeasonLongest` 10, `SeasonCount` 20, `GameLongest` 30, `GameCount` 40,
  `TeamGameCount` 50, `SeasonCountAgainst` 60, `GameCountAgainst` 70. Ties order by kind, then text, so the order
  doesn't depend on input order. `Ordinal` is the position in that order.
- **Freshness guard (season facts only):** the season document must be written after the athlete's team's previous
  game this season was finalized; otherwise it can't include that game and the count is short. The team is the
  play's offense or defense by the fact's role (takeaway and sack actors are the defense; scorers, explosive plays
  and victims the offense), not the athlete's own team record. No previous game: fresh. Previous game not
  finalized: dropped. Game facts come from this game's plays and are never stale.
  Measured with [`07a`](sql/07a-freshness-and-names.sql) on each team's latest 2026 game: of 4,444 athletes with a
  season document, **1,080 (24%) would fail the guard** (document older than the previous game's final).
- **Templates** (`TemplateVersion` 1): one per fact type and kind, e.g. "T. Cooley's second interception of the
  season.", "J. Smith has been sacked 3 times this season.", "Alabama's third takeaway of the game.",
  "T. Cooley's 42-yard run is the longest of the game.", "T. Cooley's longest catch of the season (48 yards)."
  Season sacks are a count, not an ordinal ("has 3.5 sacks this season"), since ESPN credits halves. Ordinals are
  words to tenth, then digits ("11th", "22nd"). Names ending in "s" take a bare apostrophe.
- **Names:** `AthleteSeason.ShortName` (populated for all 15,721 athletes in 2026 plays) and
  `FranchiseSeason.DisplayNameShort`.
- Tests: `FactSelectorTests`, `FactPhraserTests`.
