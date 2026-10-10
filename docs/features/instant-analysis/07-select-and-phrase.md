# 07. Select and phrase

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** not started

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

- [ ] Given several facts for one play, all are kept and ordered by priority.
- [ ] A fact with a stale source is dropped and its reason is available to the logger.
- [ ] Rendering is deterministic: the same facts always give the same text.
