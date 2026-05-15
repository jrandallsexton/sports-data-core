# CompetitionStreamerBase — sport-neutralization plan

**Status:** Part 1 implemented in PR [#323](https://github.com/jrandallsexton/sports-data-core/pull/323) on 2026-05-15. Part 2 implemented in PR [#324](https://github.com/jrandallsexton/sports-data-core/pull/324) on 2026-05-15.
**Scope:** `src/SportsData.Producer/Application/Competitions/CompetitionStreamerBase.cs`
**Why:** This class is the sport-neutral live-streaming base (typed on `TCompetitionDto`,
subclassed by `FootballCompetitionStreamer` and `BaseballCompetitionStreamer`), but its
public surface and log prose still use football-specific terminology ("kickoff") and one
section of its dispatch logic hardcodes a football-flavored set of `DocumentType` values.
We want the base to read cleanly for any sport we add next (MLB is already a subclass; NBA
/ NHL / PGA are on the roadmap).

## Verification done before drafting

- `KickoffOutcome` and `WaitForKickoffAsync` are referenced **only** inside
  `CompetitionStreamerBase.cs`. No external callers, so renames are internal-only.
- Other repo hits for "Kickoff" (PlayType enum, NFL season-start countdown UI,
  TeamSchedule.css label, mobile timeUtils, EF migrations seeded data) are legitimate
  football domain usage and out of scope.

## Part 1 — Terminology (mechanical, no behavior change) — implemented

Implemented in PR #323. Line numbers below were as of pre-rename commit `66e096c4`.
The renames and log-string edits below are now present in `CompetitionStreamerBase.cs`
(verifiable by greping for `LiveStartOutcome` and `WaitForLiveStartAsync`).

| What | Where (pre-rename) | Renamed to |
|---|---|---|
| `KickoffOutcome` enum | `:43` | `LiveStartOutcome` |
| `KickoffOutcome.KickoffDetected` | `:45` | `LiveStartOutcome.StartDetected` |
| `WaitForKickoffAsync` method | `:166`, `:313` | `WaitForLiveStartAsync` |
| Local `kickoffOutcome` var | `:166` | `startOutcome` |
| Log: "Waiting for kickoff..." | `:165` | "Waiting for competition to start..." |
| Log: "Polling for kickoff every 20 seconds..." | `:315` | "Polling for live start every 20 seconds..." |
| Log: "Waiting for kickoff exceeded max duration" | `:324` | "Waiting for live start exceeded max duration" |
| Log: "Kickoff detected! Game is now in progress." | `:347` | "Live start detected. Competition is now in progress." |
| Log: "Game went final while waiting for kickoff." | `:173` | "Competition went final while waiting for start." |
| Log: "Kickoff was not detected within max stream duration. Aborting." | `:182` | "Live start was not detected within max stream duration. Aborting." |
| Failure-reason string: "Kickoff not detected within max stream duration" | `:185` | "Live start not detected within max stream duration" |
| XML doc comment on `KickoffOutcome` (lines 38–42, 43, 45) | `:38-48` | "WaitForKickoffAsync" / "KickoffDetected" prose replaced |

### Also: normalized "Game" → "Competition" in log strings

The base previously alternated between **Game** (`:165, :192, :196, :347, :353, :430,
:434`) and **Competition** (`:92, :109, :141, :278, :287, :315`). The canonical domain
term in this code is *Competition* (`CompetitionStreamerBase`, `StreamCompetitionCommand`,
`CompetitionStream`, etc.). "Game" was replaced with "Competition" in all log strings for
consistency. No code identifiers changed.

### Out of scope for Part 1

Log fields, EF entity names, sport-specific PlayType values, mobile / web UI strings — all
fine where they are.

## Part 2 — Design smell (football leakage, separate PR)

Lines `:537–543`:

```csharp
var parentId = type is
    DocumentType.EventCompetitionProbability or
    DocumentType.EventCompetitionDrive or          // football-only concept
    DocumentType.EventCompetitionSituation or
    DocumentType.EventCompetitionPlay
    ? command.CompetitionId.ToString()
    : null;
```

The sport-neutral base hardcodes a football-flavored set of doc types when deciding
whether to attach `ParentId` to the outgoing `DocumentRequested` event. `Drive` does not
exist in baseball. This means:

1. The base **knows** about a football-only DocumentType, violating the abstraction.
2. Adding any new child polling target in a future sport (e.g. an MLB-specific
   `EventCompetitionPitching`) silently produces `parentId = null` until somebody
   remembers to edit this list in the base.

### Proposed shape

Extend the polling-target tuple to carry the flag at declaration site:

```csharp
protected abstract IEnumerable<(Uri? RefUri, DocumentType DocumentType, int IntervalSeconds, bool RequiresParentId)>
    GetPollingTargets(TCompetitionDto competitionDto);
```

`FootballCompetitionStreamer.GetPollingTargets`:

```csharp
// Flag values shown here reflect the audited mapping (see Verification section
// below) — NOT the pre-Part-2 hardcoded ladder. Probability is false because
// its processor resolves the parent via the DTO's own Competition ref; the
// other four call TryGetOrDeriveParentId downstream.
yield return (competitionDto.Probabilities?.Ref, DocumentType.EventCompetitionProbability, 15, RequiresParentId: false);
yield return (competitionDto.Drives?.Ref,        DocumentType.EventCompetitionDrive,       15, RequiresParentId: true);
yield return (competitionDto.Details?.Ref,       DocumentType.EventCompetitionPlay,        10, RequiresParentId: true);
yield return (competitionDto.Situation?.Ref,     DocumentType.EventCompetitionSituation,    5, RequiresParentId: true);
yield return (competitionDto.Leaders?.Ref,       DocumentType.EventCompetitionLeaders,     60, RequiresParentId: true);
```

`BaseballCompetitionStreamer.GetPollingTargets`: same shape, no Drives target, identical
flag values for the others.

Then `PublishDocumentRequestAsync` takes the bool through the call chain (or a small
record per target) and drops the `type is …` ladder entirely. The base no longer
references any sport-specific DocumentType.

### Verification done (2026-05-15 audit)

Audited each polling target against its downstream processor (via `TryGetOrDeriveParentId`
in `DocumentProcessorBase`). Result:

| DocumentType | Previous ladder | Audited truth | Flag set to |
|---|---|---|---|
| `EventCompetitionProbability` | `true` | **false** — processor resolves parent via DTO's `Competition` ref; never reads `ParentId` | `false` |
| `EventCompetitionDrive` | `true` | `true` — `EventCompetitionDriveDocumentProcessor.cs:105` | `true` |
| `EventCompetitionPlay` | `true` | `true` — `EventCompetitionPlayDocumentProcessorBase.cs:61` | `true` |
| `EventCompetitionSituation` | `true` | `true` — football and baseball both call `TryGetOrDeriveParentId` | `true` |
| `EventCompetitionLeaders` | (not in ladder = `false`) | **true** — `EventCompetitionLeadersDocumentProcessor.cs:46` | `true` |

**Two latent bugs surfaced.** Probability shipped `ParentId` that nothing read (harmless waste).
Leaders shipped without `ParentId` and downstream code relied on URI-based fallback in
`TryGetOrDeriveParentId` — works today but is fragile against ESPN URL-shape changes. Both
are corrected by Part 2; the PR is therefore **not a pure behavior-preserving refactor** and
the commit message + PR description should call that out for reviewers.

### Tests added

Tests live in `test/unit/SportsData.Producer.Tests.Unit/Application/Competitions/`:

- `FootballCompetitionStreamerTests.cs` — `#region Polling Targets`: 5 facts asserting
  count, `RequiresParentId` per type, intervals, ref-URI passthrough, and null-ref behavior.
- `BaseballCompetitionStreamerTests.cs` (new file) — same 5 facts plus an explicit
  `DoesNotIncludeDrive` test (Drive is football-only).

Test pattern: nested `Testable*Streamer` subclass exposes the `protected GetPollingTargets`
method; AutoMocker constructs it with `ProducerTestBase` deps. No mocking of the method
itself — it's pure logic over a DTO.

## Recommended PR sequence

1. **Terminology PR** — Part 1 only. Mechanical, easy to review, no behavior change.
   Title: `refactor(producer): drop football-specific terms from CompetitionStreamerBase`
2. **Design PR** — Part 2 only. Behavior-equivalent (with the test in place).
   Title: `refactor(producer): move parent-id requirement to polling-target declaration`

Do **not** combine them. The design PR deserves dedicated review attention; bundling it
with the rename will let it slip by.

## Open question

Naming: is `LiveStartOutcome` the right name, or do we prefer `InProgressOutcome` /
`StartDetectedOutcome`? Decide at PR time. The enum value `StartDetected` is the
load-bearing part — the enum type name is a bikeshed.
