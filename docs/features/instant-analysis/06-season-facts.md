# 06. Season facts

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** not started

**Depends on:** [01 Data spike](01-data-spike.md), [04 Classifier](04-classifier.md)

**Feature doc:** ["As of this play"](instant-analysis.md#as-of-this-play-how-a-season-count-is-computed) · [Stage 3: dig](instant-analysis.md#stage-3-dig-the-data) · [Out of reach for now](instant-analysis.md#out-of-reach-for-now)

## Goal

Season-to-date facts that are correct at the instant of the play.

## Scope

- Season count as of this play = season-to-date from games **before** the current competition + qualifying plays in the current competition up to and including this play.
- The season-to-date source chosen in task 01 (`AthleteSeasonStatistic` or Σ `AthleteCompetitionStatistic`), never double-counting part of the current game.
- Season milestones crossed on this play (e.g. 1,000 rushing yards), thresholds in code.
- Provenance for each number in the `Facts` jsonb.

**Out of scope:**

- Conference and national analysis (task 14).
- Career facts (task 16).

## Deliverables

- The as-of query (or queries) per stat family.
- Unit tests, including the example interception.

## Acceptance criteria

- [ ] The example interception yields "second of the season".
- [ ] A run that crosses a milestone threshold yields the milestone fact; the next run doesn't repeat "crossed" but does carry the new total.
- [ ] Dig time stays within the timing budget in the feature doc.

## Notes

- Can run in parallel with task 05.
