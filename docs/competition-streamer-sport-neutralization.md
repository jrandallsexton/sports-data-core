# CompetitionStreamerBase — sport-neutralization plan

**Status:** drafted 2026-05-14 — pending implementation
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

## Part 1 — Terminology (mechanical, no behavior change)

Pure rename + log-string edits. Line numbers below are as of commit `66e096c4`; verify
before editing.

| What | Where | Suggested rename |
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
| XML doc comment on `KickoffOutcome` (lines 38–42, 43, 45) | `:38-48` | Replace "WaitForKickoffAsync" / "KickoffDetected" prose |

### Also: normalize "Game" → "Competition" in log strings

The base alternates between **Game** (`:165, :192, :196, :347, :353, :430, :434`) and
**Competition** (`:92, :109, :141, :278, :287, :315`). The canonical domain term in this
code is *Competition* (`CompetitionStreamerBase`, `StreamCompetitionCommand`,
`CompetitionStream`, etc.). Replace "Game" with "Competition" in all log strings for
consistency. No code identifiers change.

### Out of scope for this PR

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
yield return (competitionDto.Probabilities?.Ref, DocumentType.EventCompetitionProbability, 15, RequiresParentId: true);
yield return (competitionDto.Drives?.Ref,        DocumentType.EventCompetitionDrive,       15, RequiresParentId: true);
yield return (competitionDto.Details?.Ref,       DocumentType.EventCompetitionPlay,        10, RequiresParentId: true);
yield return (competitionDto.Situation?.Ref,     DocumentType.EventCompetitionSituation,    5, RequiresParentId: true);
yield return (competitionDto.Leaders?.Ref,       DocumentType.EventCompetitionLeaders,     60, RequiresParentId: false);
```

`BaseballCompetitionStreamer.GetPollingTargets`: same shape, no Drives target, identical
flag values for the others.

Then `PublishDocumentRequestAsync` takes the bool through the call chain (or a small
record per target) and drops the `type is …` ladder entirely. The base no longer
references any sport-specific DocumentType.

### Verification before merge

- Audit each existing polling target's `RequiresParentId` value against the consumer side
  (the relevant `DocumentProcessor` for each `DocumentType`). If a processor today reads
  `ParentId` from `DocumentRequested`, the flag must be `true`. If not, `false` is fine
  but worth a code comment.
- Add a unit test per streamer that materializes `GetPollingTargets(dto)` and asserts the
  shape (count, doc types, intervals, flags). This is the first test in the
  `CompetitionStreamer*` family — they currently have zero coverage (see
  `live-event-scheduling-testing.md` if it exists, or the parent investigation).

### Risk

Behavior-equivalent if the flag mapping is correct on day one. The risk is mis-mapping a
flag during the migration — hence the unit test.

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
