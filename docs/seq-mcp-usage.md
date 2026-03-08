# Seq MCP Usage — Agent-Based Log Scanning

## Overview

This document describes a repeatable approach for using the Seq MCP server with Claude Code agents to investigate production issues in the SportsData platform. The approach fans out parallel agents, each focused on a specific document type or error category, to trace issues without overwhelming a single context window.

## Prerequisites

- Seq MCP server configured (see [seq-mcp.md](seq-mcp.md))
- Available tools: `SeqSearch`, `SeqWaitForEvents`, `SignalList`

## Approach

### Step 1: Discovery — Identify Active DocumentTypes and Error Patterns

Run broad queries to establish what's happening:

```
SeqSearch: ApplicationName = 'SportsData.Producer' and @Level = 'Error'   (count: 100)
SeqSearch: ApplicationName = 'SportsData.Producer' and @Level = 'Warning' (count: 50)
```

Parse results to extract:
- Distinct `DocumentType` values with counts
- Error/warning message patterns grouped by DocumentType
- This gives you the "map" of issues to investigate

### Step 2: Fan Out — Launch Parallel Agents per Issue Area

For each distinct DocumentType (or error pattern cluster), launch a background agent with:

1. **Context** — the DocumentType, known error counts, and pattern summaries from Step 1
2. **Specific queries** to run — tailored to the error patterns found
3. **A structured output format** — so results can be compared across agents

#### Agent Prompt Template

```
You have access to Seq MCP tools (mcp__seq__SeqSearch, mcp__seq__SeqWaitForEvents, mcp__seq__SignalList).
You MUST use ToolSearch to load them before calling them (e.g. query: "select:mcp__seq__SeqSearch").

Investigate {DocumentType} {level} events in the {ApplicationName} application.
{Brief summary of known patterns and counts from Step 1.}

Your task:
1. Search for {count} recent events matching: {specific filter}
2. Classify errors as transient (retried/recovered) or persistent (dead-lettered/unhandled)
3. Check for escalation (warnings -> errors -> dead letters)
4. Identify any unexpected or unhandled failure modes

Return a structured summary with:
- Error categories and counts
- Transient vs persistent classification
- Any unhandled/unexpected errors
- Recommended actions if any
```

### Step 3: Compile — Synthesize Agent Results

Once all agents return, compile findings into a summary table:

| DocumentType | Category | Count | Transient/Persistent | Severity | Action Needed |
|---|---|---|---|---|---|
| *filled per agent* | | | | | |

### Step 4: Act — Address Issues by Severity

Priority order:
1. **Persistent errors** causing data loss or dead letters
2. **Deserialization failures** indicating schema mismatches
3. **Concurrency conflicts** that exhaust retries without recovery
4. **Transient errors** that self-resolve (monitor only)

## Useful Seq Filters

### By Application

```seq
ApplicationName = 'SportsData.Producer'
ApplicationName = 'SportsData.Provider'
```

### By Level

```seq
@Level = 'Error'
@Level = 'Warning'
@Level in ['Error', 'Fatal']
```

### By DocumentType

```seq
DocumentType = 'EventCompetitionAthleteStatistics'
DocumentType = 'AthleteSeason'
DocumentType = 'EventCompetitionCompetitorRecord'
DocumentType = 'AthleteSeasonNote'
```

### By Error Pattern

```seq
@MessageTemplate like '%MAX_RETRIES%'
@MessageTemplate like '%PROCESSOR_FAILED%'
@Exception like '%DbUpdateConcurrencyException%'
@Exception like '%deserializ%'
```

### Combined Filters

```seq
ApplicationName = 'SportsData.Producer' and DocumentType = 'AthleteSeason' and @Level = 'Error'
ApplicationName = 'SportsData.Producer' and @Exception like '%ConcurrencyException%'
```

### By Time (relative)

Seq MCP searches return most recent events by default. Use `count` parameter to control result volume. For time-bounded queries, combine with timestamp filters if supported.

### Correlation Tracing

To follow a single document through the pipeline:

```seq
CorrelationId = '{id}'
DocumentId = '{hash}'
SourceUrlHash = '{hash}'
```

## DocumentType Reference

Full enum defined in `src/SportsData.Core/Common/DocumentType.cs`:

| Value | DocumentType | Description |
|---|---|---|
| 0 | `Athlete` | Athlete bio/profile |
| 1 | `AthleteImage` | Athlete headshot/image |
| 2 | `AthletePosition` | Athlete position mapping |
| 3 | `AthleteSeason` | Per-athlete per-season record |
| 26 | `AthleteSeasonStatistics` | Per-athlete season aggregate stats |
| 65 | `AthleteSeasonNote` | Athlete season notes/annotations |
| 4 | `Award` | Awards |
| 5 | `Coach` | Coach bio/profile |
| 6 | `CoachSeason` | Coach per-season record |
| 55 | `CoachRecord` | Coach career record |
| 66 | `CoachSeasonRecord` | Coach season win/loss record |
| 7 | `Contest` | Contest/matchup |
| 8 | `Event` | Scheduled event |
| 9 | `EventCompetition` | Competition within an event |
| 10 | `EventCompetitionBroadcast` | Broadcast info |
| 11 | `EventCompetitionCompetitor` | Competitor in a competition |
| 12 | `EventCompetitionCompetitorLineScore` | Line scores |
| 13 | `EventCompetitionCompetitorScore` | Final scores |
| 62 | `EventCompetitionCompetitorRoster` | Game-day roster |
| 64 | `EventCompetitionCompetitorRecord` | Team record at time of competition |
| 60 | `EventCompetitionCompetitorStatistics` | Team-level game stats |
| 59 | `EventCompetitionAthleteStatistics` | Per-athlete game stats |
| 14 | `EventCompetitionDrive` | Drive-level data |
| 15 | `EventCompetitionLeaders` | Stat leaders per competition |
| 16 | `EventCompetitionOdds` | Betting odds |
| 17 | `EventCompetitionPlay` | Play-by-play |
| 18 | `EventCompetitionPowerIndex` | Power index ratings |
| 19 | `EventCompetitionPrediction` | Game predictions |
| 20 | `EventCompetitionProbability` | Win probability |
| 57 | `EventCompetitionSituation` | In-game situation |
| 21 | `EventCompetitionStatus` | Game status |
| 22 | `Franchise` | Franchise/team |
| 23 | `FranchiseLogo` | Franchise logo |
| 39 | `FranchiseSeasonLogo` | Season-specific franchise logo |
| 24 | `GameSummary` | Game summary rollup |
| 25 | `GolfCalendar` | Golf-specific calendar |
| 27 | `GroupLogo` | Conference/group logo |
| 28 | `GroupSeason` | Conference/group season |
| 29 | `GroupSeasonLogo` | Conference/group season logo |
| 30 | `Position` | Position reference data |
| 31 | `Scoreboard` | Scoreboard snapshot |
| 32 | `Season` | Season definition |
| 33 | `SeasonFuture` | Future season data |
| 34 | `SeasonType` | Season type (regular, post, etc.) |
| 35 | `SeasonTypeWeek` | Week within a season type |
| 36 | `SeasonTypeWeekRankings` | Weekly rankings |
| 37 | `Seasons` | Seasons listing |
| 56 | `SeasonRanking` | Season ranking |
| 58 | `SeasonPoll` | Season poll |
| 61 | `SeasonPollWeek` | Weekly poll |
| 38 | `Standings` | Standings |
| 42 | `TeamSeason` | Team per-season record |
| 43 | `TeamSeasonAward` | Team season awards |
| 45 | `TeamSeasonInjuries` | Injury reports |
| 46 | `TeamSeasonLeaders` | Team season stat leaders |
| 47 | `TeamSeasonProjection` | Team season projections |
| 48 | `TeamSeasonRank` | Team season ranking |
| 49 | `TeamSeasonRecord` | Team season win/loss record |
| 50 | `TeamSeasonRecordAts` | Team season record against the spread |
| 51 | `TeamSeasonStatistics` | Team season aggregate stats |
| 52 | `Venue` | Venue info |
| 53 | `VenueImage` | Venue image |
| 54 | `Weeks` | Weeks listing |
| 99 | `OutboxTest` | Test document (base context) |
| 98 | `OutboxTestTeamSport` | Test document (team sport context) |
| 9999 | `Unknown` | Unknown/unrecognized |

## Example Investigation: NCAA Football 2019 Sourcing Run (2026-03-08)

### Discovery Results

Broad scan of `SportsData.Producer` errors (100) and warnings (50) identified 4 DocumentTypes with issues:

| DocumentType | Errors | Warnings |
|---|---|---|
| `EventCompetitionAthleteStatistics` | 83 | 23 |
| `AthleteSeason` | 17 | — |
| `EventCompetitionCompetitorRecord` | — | 25 |
| `AthleteSeasonNote` | — | 2 (actually 20 at higher count) |

### Agent Findings

#### EventCompetitionAthleteStatistics — BUG FOUND

| Category | Count | Severity | Transient? |
|---|---|---|---|
| `InvalidOperationException: Collection was modified` | ~53 | **HIGH** | Partial |
| `DbUpdateConcurrencyException` | ~30 | Low | Yes |

- **Bug:** Collection modified during enumeration at `EventCompetitionAthleteStatisticsDocumentProcessor.cs:line 208`. Fix with `.ToList()` materialization or index-based loop.
- **Concurrency errors** self-resolve: retry mechanism (3 attempts) works, and message bus re-delivers failures that succeed on next attempt.
- Concurrency warnings (23x "attempt 1/3") are healthy retry signals — none reached attempt 3/3.

#### AthleteSeason — DLQ Awaiting Dependency Sourcing

| Category | Count | Severity | Transient? |
|---|---|---|---|
| `HANDLER_MAX_RETRIES` (4 distinct athletes) | 17 | Low | Expected |

- Only **4 distinct athletes** failing, correctly dead-lettered after exhausting 10 Hangfire retries.
- Two root causes: "Athlete not found" (IDs 4581926, -12227) and "Franchise season not found" (team 125290, athletes 4927291, 4930678).
- **This is expected behavior during bulk backfill.** The processor discovers a missing dependency and requests it from Provider, but Provider is backlogged processing the 2019 season crawl. Hangfire exhausts its 10 retries before Provider sources the dependency, so the event lands in the DLQ.
- Once Provider catches up and sources the missing athletes/franchise seasons, a **manual DLQ replay** (via endpoint) will succeed.
- Negative athlete ID `-12227` is likely invalid upstream data — this one may still fail on replay.
- **Action:** Wait for Provider backlog to clear, then replay DLQ. The `-12227` athlete may need manual inspection after replay.

#### EventCompetitionCompetitorRecord — Working as Designed

| Category | Count | Severity | Transient? |
|---|---|---|---|
| Concurrency yield after 3 retries | 9 | None | Yes |
| Concurrency retry (attempt 1-2/3) | 16 | None | Yes |

- Zero errors from Producer. Conflicts caused by parallel record-type fan-out (total, home, road, vsconf) for same competitor.
- "Losing" process yields gracefully; "winning" process persists correct data.
- Only a concern during bulk backfill parallelism; normal operation unaffected.

#### AthleteSeasonNote — Graceful Soft-Skip

| Category | Count | Severity | Transient? |
|---|---|---|---|
| Deserialization returns null Id/Ref | 20 | Low | N/A |

- Processor logs warning and returns early — no error escalation, no dead letters.
- `DOC_CREATED_PROCESSOR_COMPLETED` logged for same documents (treated as successful skip).
- Root cause: ESPN returns empty/different JSON shape for archived 2019 season notes.
- Separate ESPN `ServiceUnavailable`/`Forbidden` errors from Provider are a distinct upstream issue.

### Action Items from This Investigation

1. **[HIGH]** Fix collection-modified bug in `EventCompetitionAthleteStatisticsDocumentProcessor.cs:208`
2. **[LOW]** Add diagnostic logging for `AthleteSeasonNote` deserialization (log raw payload on failure)
3. **[LOW]** Filter negative athlete IDs at Provider ingestion stage
4. **[MONITOR]** `AthleteSeason` DLQ — replay after Provider backlog clears; `-12227` may still fail
5. **[MONITOR]** Concurrency patterns are healthy — no action unless volumes increase during live games

### Important: DLQ Design Notes

The dead-letter queue (DLQ) is **intentional infrastructure**, not a failure state to be purged:

- During bulk backfill, a document processor may discover a missing dependency (e.g., Athlete, FranchiseSeason) and request it from Provider. However, Provider may be backlogged processing thousands of other documents.
- Hangfire retries the job up to 10 times. If the dependency still hasn't been sourced after all retries, the event moves to the DLQ. This is expected.
- Once Provider catches up and sources the dependency, the DLQ event can be **manually replayed via endpoint** and will succeed.
- **Never purge the DLQ** — those events represent work that will succeed once dependencies are available.
- DLQ replay is a manual process; nothing in the system automatically replays DLQ events.

## Notes

- Keep `count` parameter reasonable (50-100) to avoid oversized results
- Agents must load MCP tools via `ToolSearch` before using them
- Results exceeding token limits are saved to temp files — use Python/jq to parse
- The MCP is read-only; no risk of modifying Seq data
