# IncludeLinkedDocumentTypes - Selective Document Spawning

## Overview

`IncludeLinkedDocumentTypes` is an optional inclusion filter that controls which linked/child documents are spawned when processing a parent document. It enables **selective re-processing** of documents without triggering the full cascade of dependent document requests.

## Why It Was Added

### The Problem: All-or-Nothing Document Processing

When a document processor handles a parent document (e.g., `TeamSeason`), it typically spawns requests for **all** linked child documents:

```csharp
// TeamSeasonDocumentProcessor spawns ~13 different document types
await PublishChildDocumentRequest(..., DocumentType.AthleteSeason, ...);      // 50-100+ athletes
await PublishChildDocumentRequest(..., DocumentType.TeamSeasonStatistics, ...);
await PublishChildDocumentRequest(..., DocumentType.TeamSeasonRank, ...);
await PublishChildDocumentRequest(..., DocumentType.TeamSeasonLeaders, ...);
await PublishChildDocumentRequest(..., DocumentType.Event, ...);              // 12+ games
// ... 8 more document types
```

**The Issue**: If you wanted to refresh **only** team statistics (e.g., after a game completed), you had to either:
1. Re-process the entire `TeamSeason` document ? spawns 150+ jobs for athletes, events, leaders, etc.
2. Manually construct specific `DocumentRequested` events ? bypasses normal orchestration flow

Neither option was ideal for targeted updates.

### The Use Case: Selective Refresh

Common scenarios that needed selective processing:

1. **Refresh Team Statistics Only** - After a game, update stats without re-processing entire roster
2. **Update Team Rankings** - Poll rankings without spawning athlete/event requests
3. **Refresh Game Schedule** - Update event list without re-processing team rosters
4. **Update Team Leaders** - Refresh leader stats without full roster re-processing

### The Solution: Inclusion Filter

The `IncludeLinkedDocumentTypes` property provides a **whitelist** of document types to spawn, allowing fine-grained control over which linked documents are processed.

## How It Works

### Configuration Flow

```mermaid
graph LR
    A[API Request] -->|POST /resourceindex/{id}/process| B[ResourceIndexController]
    B -->|with IncludeLinkedDocumentTypes| C[DocumentJobDefinition]
    C --> D[ResourceIndexJob]
    D --> E[DocumentCreated Event]
    E -->|includes filter| F[ProcessDocumentCommand]
    F --> G[TeamSeasonDocumentProcessor]
    G -->|ShouldSpawn check| H[Selective Child Spawning]
```

### Behavior

**When `null` or empty (default)**:
- All linked documents are spawned
- Standard full-cascade processing
- Backward compatible with existing behavior

**When provided with specific types**:
- Only document types in the list are spawned
- Other linked documents are **skipped**
- Logged for visibility: `"Skipping spawn of {DocumentType} due to inclusion filter"`

## Implementation

### API Entry Point

#### Endpoint: `POST /api/resourceindex/{indexId}/process`

```json
{
  "includeLinkedDocumentTypes": [
    "TeamSeasonStatistics",
    "TeamSeasonRank"
  ]
}
```

This request will:
1. Re-process the `TeamSeason` document from the ResourceIndex
2. **Only** spawn child requests for `TeamSeasonStatistics` and `TeamSeasonRank`
3. Skip all other linked documents (athletes, events, leaders, etc.)

### Request Model

**`ProcessResourceIndexRequest`**:
```csharp
public record ProcessResourceIndexRequest
{
    /// <summary>
    /// Inclusion-only semantics: if this is provided and non-empty,
    /// downstream processors should only spawn linked documents that are in this list.
    /// If null or empty, all linked documents are processed (default behavior).
    /// </summary>
    public IReadOnlyCollection<DocumentType>? IncludeLinkedDocumentTypes { get; init; }
}
```

### Controller Logic

```csharp
[HttpPost("{indexId}/process")]
public async Task<IActionResult> ProcessResourceIndex(
    [FromRoute] Guid indexId,
    [FromBody] ProcessResourceIndexRequest? request = null)
{
    var resourceIndex = await _dataContext.ResourceIndexJobs
        .Where(x => x.Id == indexId)
        .FirstOrDefaultAsync();

    if (resourceIndex == null)
        return NotFound();

    var jobDef = new DocumentJobDefinition(resourceIndex)
    {
        IncludeLinkedDocumentTypes = request?.IncludeLinkedDocumentTypes
    };

    if (request?.IncludeLinkedDocumentTypes?.Count > 0)
    {
        _logger.LogInformation(
            "Processing ResourceIndex {Id} with inclusion filter: {DocumentTypes}",
            indexId,
            string.Join(", ", request.IncludeLinkedDocumentTypes));
    }

    _backgroundJobProvider.Enqueue<ResourceIndexJob>(x => x.ExecuteAsync(jobDef));

    return Accepted();
}
```

### Command Propagation

The filter propagates through the processing pipeline:

1. **`DocumentJobDefinition`** ? Carries filter from API to job
2. **`ProcessResourceIndexItemCommand`** ? Passes filter to resource processor
3. **`DocumentCreated` Event** ? Includes filter in event payload
4. **`ProcessDocumentCommand`** ? Provides filter to document processor

```csharp
public class ProcessDocumentCommand
{
    /// <summary>
    /// Optional inclusion-only list of linked document types.
    /// If provided and non-empty, downstream processors should only spawn linked documents
    /// of types in this collection. If null or empty, all linked documents are processed.
    /// </summary>
    public IReadOnlyCollection<DocumentType>? IncludeLinkedDocumentTypes { get; init; }
}
```

### Processor Implementation

#### Standard Pattern: `ShouldSpawn` Helper Method

```csharp
/// <summary>
/// Determines if a linked document of the specified type should be spawned,
/// based on the inclusion filter in the command.
/// </summary>
/// <param name="documentType">The type of linked document to check</param>
/// <param name="command">The processing command containing the optional inclusion filter</param>
/// <returns>True if the document should be spawned; false otherwise</returns>
private bool ShouldSpawn(DocumentType documentType, ProcessDocumentCommand command)
{
    // If no inclusion filter is specified, spawn all documents (default behavior)
    if (command.IncludeLinkedDocumentTypes == null || command.IncludeLinkedDocumentTypes.Count == 0)
    {
        return true;
    }

    // If inclusion filter is specified, only spawn if the type is in the list
    var shouldSpawn = command.IncludeLinkedDocumentTypes.Contains(documentType);

    if (!shouldSpawn)
    {
        _logger.LogInformation(
            "Skipping spawn of {DocumentType} due to inclusion filter. Allowed types: {AllowedTypes}",
            documentType,
            string.Join(", ", command.IncludeLinkedDocumentTypes));
    }

    return shouldSpawn;
}
```

#### Usage in Update Methods

```csharp
private async Task ProcessUpdateEntity(
    FranchiseSeason existing,
    EspnTeamSeasonDto dto,
    ProcessDocumentCommand command)
{
    // Always process logos (non-filtered)
    await ProcessLogos(existing.Id, dto, command);

    // Conditionally spawn based on inclusion filter
    if (ShouldSpawn(DocumentType.AthleteSeason, command))
    {
        await PublishChildDocumentRequest(
            command,
            dto.Athletes,
            existing.Id,
            DocumentType.AthleteSeason,
            CausationId.Producer.TeamSeasonDocumentProcessor);
    }

    if (ShouldSpawn(DocumentType.Event, command))
    {
        await PublishChildDocumentRequest(
            command,
            dto.Events,
            existing.Id,
            DocumentType.Event,
            CausationId.Producer.TeamSeasonDocumentProcessor);
    }

    if (ShouldSpawn(DocumentType.TeamSeasonStatistics, command))
    {
        await PublishChildDocumentRequest(
            command,
            dto.Statistics,
            existing.Id,
            DocumentType.TeamSeasonStatistics,
            CausationId.Producer.TeamSeasonDocumentProcessor);
    }

    // ... other conditional spawns
}
```

### Important Implementation Notes

#### 1. New Entity Processing vs. Updates

**New Entities**: Typically **ignore** the filter and spawn all dependencies
```csharp
private async Task ProcessNewEntity(...)
{
    // Create the entity
    await ProcessDependencies(...);
    
    // NEW entities spawn ALL linked documents regardless of filter
    // (need complete data for initial creation)
    await ProcessDependents(canonicalEntity, dto, command);
}
```

**Update Entities**: **Honor** the filter for selective refresh
```csharp
private async Task ProcessUpdateEntity(...)
{
    // UPDATES respect the inclusion filter
    if (ShouldSpawn(DocumentType.AthleteSeason, command))
        await PublishChildDocumentRequest(...);
}
```

#### 2. Always-Process Items

Some items should **always** be processed regardless of filter:
- **Logos/Images**: Visual assets that don't trigger cascades
- **Core entity updates**: Changes to the entity itself (name, abbreviation, etc.)
- **Direct dependencies**: Required foreign keys (Venue, Group, etc.)

```csharp
// Always process logos - no filter check
await ProcessLogos(existing.Id, dto, command);

// Always update core entity fields
if (existing.Abbreviation != dto.Abbreviation)
{
    existing.Abbreviation = dto.Abbreviation;
    await _dataContext.SaveChangesAsync();
}
```

## Processors Using This Pattern

### Current Implementations

1. **TeamSeasonDocumentProcessor**
   - Filters: `AthleteSeason`, `Event`, `TeamSeasonLeaders`, `TeamSeasonRank`, `TeamSeasonStatistics`
   - Use case: Refresh specific aspects of team data without full roster/schedule re-processing

2. **FranchiseDocumentProcessor** (future)
   - Potential filters: `FranchiseSeason`, `FranchiseAward`, `FranchiseCoach`
   - Use case: Update franchise metadata without re-processing all seasons

3. **EventDocumentProcessor** (future)
   - Potential filters: `EventCompetition`, `EventMedia`, `EventTickets`
   - Use case: Refresh event details without re-processing full competition data

## Use Cases

### 1. Refresh Team Statistics After Game

**Scenario**: A game just finished. You want to update team season statistics without re-processing the entire roster.

**Request**:
```bash
POST /api/resourceindex/{teamSeasonResourceIndexId}/process
{
  "includeLinkedDocumentTypes": ["TeamSeasonStatistics"]
}
```

**Result**:
- ? Re-fetches `TeamSeason` document from ESPN
- ? Spawns `TeamSeasonStatistics` child request
- ? Skips `AthleteSeason` (50-100 athletes)
- ? Skips `Event` (12 games)
- ? Skips `TeamSeasonLeaders`, `TeamSeasonRank`, etc.

**Jobs Created**: ~2 (vs. ~170 without filter)

### 2. Update Team Rankings Only

**Scenario**: AP/Coaches poll released. You want to refresh rankings without touching anything else.

**Request**:
```bash
POST /api/resourceindex/{teamSeasonResourceIndexId}/process
{
  "includeLinkedDocumentTypes": ["TeamSeasonRank"]
}
```

**Result**:
- ? Updates `TeamSeasonRank` from ESPN rankings endpoint
- ? Skips all other linked documents

**Jobs Created**: ~2

### 3. Refresh Game Schedule

**Scenario**: ESPN updated game times. You want to refresh the event list without re-processing team rosters.

**Request**:
```bash
POST /api/resourceindex/{teamSeasonResourceIndexId}/process
{
  "includeLinkedDocumentTypes": ["Event"]
}
```

**Result**:
- ? Re-fetches team's schedule from ESPN
- ? Spawns `Event` child requests for updated games
- ? Skips athlete rosters, statistics, leaders, etc.

**Jobs Created**: ~15 (vs. ~170 without filter)

### 4. Multiple Selective Updates

**Scenario**: After a game, refresh both statistics and rankings.

**Request**:
```bash
POST /api/resourceindex/{teamSeasonResourceIndexId}/process
{
  "includeLinkedDocumentTypes": [
    "TeamSeasonStatistics",
    "TeamSeasonRank",
    "TeamSeasonLeaders"
  ]
}
```

**Result**:
- ? Updates statistics, rankings, and leaders
- ? Skips roster, events, coaches, awards, etc.

**Jobs Created**: ~4

## Architectural Benefits

### 1. **Reduced Job Explosion**
- Targeted updates prevent unnecessary job creation
- Example: 2 jobs instead of 170 for team stats refresh

### 2. **Faster Processing**
- Only relevant data is fetched and processed
- Reduces API calls to external providers (ESPN)

### 3. **Cost Efficiency**
- Fewer HTTP requests to ESPN
- Reduced database writes
- Lower Hangfire job queue pressure

### 4. **Operational Flexibility**
- Fine-grained control over data refresh
- Enables tactical updates without full re-processing
- Supports targeted troubleshooting ("just refresh this one aspect")

### 5. **Backward Compatibility**
- Default behavior (null/empty) maintains existing full-cascade processing
- No breaking changes to existing workflows

## Comparison: IncludeLinkedDocumentTypes vs. EnableDependencyRequests

Both configs control spawning behavior, but serve **different purposes**:

| Aspect | IncludeLinkedDocumentTypes | EnableDependencyRequests |
|--------|---------------------------|-------------------------|
| **Purpose** | Selective child spawning | Prevent cyclical dependencies |
| **Scope** | Child/linked documents | Missing dependencies |
| **Use Case** | Targeted updates/refresh | Dependency resolution safety |
| **Default** | `null` (all) | `false` (safe mode) |
| **When Added** | Mid-season for refresh needs | After 3.5M job explosion incident |
| **Granularity** | Per-request (API controlled) | Global config (Azure AppConfig) |
| **Controls** | **Which** children to spawn | **Whether** to request dependencies |
| **User-Facing** | ? Yes (API parameter) | ? No (internal safety) |

### Combined Example

```csharp
// EnableDependencyRequests (global config) = false
// IncludeLinkedDocumentTypes (per-request) = [TeamSeasonStatistics]

// Scenario: Processing TeamSeason document
// 1. Check for missing Franchise dependency
if (franchise is null)
{
    // EnableDependencyRequests=false ? Don't publish DocumentRequested, just retry
    throw new ExternalDocumentNotSourcedException("Franchise missing");
}

// 2. Check if we should spawn TeamSeasonStatistics child
if (ShouldSpawn(DocumentType.TeamSeasonStatistics, command))
{
    // IncludeLinkedDocumentTypes contains it ? Spawn child request
    await PublishChildDocumentRequest(..., DocumentType.TeamSeasonStatistics, ...);
}

// 3. Check if we should spawn AthleteSeason children
if (ShouldSpawn(DocumentType.AthleteSeason, command))
{
    // IncludeLinkedDocumentTypes does NOT contain it ? Skip
    _logger.LogInformation("Skipping spawn of AthleteSeason due to inclusion filter");
}
```

## Testing

### Unit Test: ShouldSpawn Logic

```csharp
[Fact]
public void ShouldSpawn_WhenFilterIsNull_ReturnsTrue()
{
    var command = new ProcessDocumentCommand(..., includeLinkedDocumentTypes: null);
    var result = ShouldSpawn(DocumentType.AthleteSeason, command);
    Assert.True(result);
}

[Fact]
public void ShouldSpawn_WhenFilterIsEmpty_ReturnsTrue()
{
    var command = new ProcessDocumentCommand(..., includeLinkedDocumentTypes: Array.Empty<DocumentType>());
    var result = ShouldSpawn(DocumentType.AthleteSeason, command);
    Assert.True(result);
}

[Fact]
public void ShouldSpawn_WhenTypeInFilter_ReturnsTrue()
{
    var command = new ProcessDocumentCommand(..., 
        includeLinkedDocumentTypes: new[] { DocumentType.TeamSeasonStatistics });
    var result = ShouldSpawn(DocumentType.TeamSeasonStatistics, command);
    Assert.True(result);
}

[Fact]
public void ShouldSpawn_WhenTypeNotInFilter_ReturnsFalse()
{
    var command = new ProcessDocumentCommand(..., 
        includeLinkedDocumentTypes: new[] { DocumentType.TeamSeasonStatistics });
    var result = ShouldSpawn(DocumentType.AthleteSeason, command);
    Assert.False(result);
}
```

### Integration Test: Selective Spawning

```csharp
[Fact]
public async Task ProcessUpdateEntity_WithInclusionFilter_OnlySpawnsFilteredTypes()
{
    // Arrange
    var filter = new[] { DocumentType.TeamSeasonStatistics };
    var command = CreateCommand(includeLinkedDocumentTypes: filter);

    // Act
    await processor.ProcessUpdateEntity(existingTeam, dto, command);

    // Assert
    eventBus.Verify(x => x.Publish(
        It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.TeamSeasonStatistics),
        It.IsAny<CancellationToken>()), Times.Once);

    eventBus.Verify(x => x.Publish(
        It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.AthleteSeason),
        It.IsAny<CancellationToken>()), Times.Never);
}
```

## Monitoring & Observability

### Log Output Examples

#### With Filter Applied
```
[Info] Processing ResourceIndex {Id} with inclusion filter: TeamSeasonStatistics, TeamSeasonRank
[Info] Skipping spawn of AthleteSeason due to inclusion filter. Allowed types: TeamSeasonStatistics, TeamSeasonRank
[Info] Skipping spawn of Event due to inclusion filter. Allowed types: TeamSeasonStatistics, TeamSeasonRank
[Info] Publishing child document request. DocumentType=TeamSeasonStatistics
[Info] Publishing child document request. DocumentType=TeamSeasonRank
```

#### Without Filter (Default)
```
[Info] Publishing child document request. DocumentType=AthleteSeason
[Info] Publishing child document request. DocumentType=Event
[Info] Publishing child document request. DocumentType=TeamSeasonStatistics
[Info] Publishing child document request. DocumentType=TeamSeasonRank
[Info] Publishing child document request. DocumentType=TeamSeasonLeaders
... (all linked documents)
```

### Metrics to Track

1. **Job Count Reduction**: Compare jobs created with/without filter
2. **Processing Time**: Measure end-to-end time for filtered vs. full refresh
3. **API Call Volume**: Track ESPN API calls saved via selective processing
4. **Filter Usage**: Monitor which DocumentTypes are commonly filtered

## Future Enhancements

### 1. Exclusion Filter (Blacklist)

Instead of inclusion-only, support exclusion:

```json
{
  "excludeLinkedDocumentTypes": ["AthleteSeason", "Event"]
}
```

**Use case**: "Refresh everything EXCEPT athletes and events"

### 2. Nested Filter Control

Allow filters to cascade to child processors:

```json
{
  "includeLinkedDocumentTypes": {
    "TeamSeason": ["TeamSeasonStatistics"],
    "Event": ["EventCompetition"]
  }
}
```

**Use case**: Multi-level selective processing

### 3. Preset Filter Profiles

Define common filter combinations:

```json
{
  "filterProfile": "StatsAndRankingsOnly"
  // Internally maps to: ["TeamSeasonStatistics", "TeamSeasonRank", "TeamSeasonLeaders"]
}
```

**Use case**: Simplified API for common scenarios

## Conclusion

`IncludeLinkedDocumentTypes` solves the "all-or-nothing" problem in document processing by providing **fine-grained control** over which linked documents are spawned. This enables:

- **Targeted data refresh** without full cascade
- **Reduced job volume** for tactical updates
- **Operational flexibility** for troubleshooting and maintenance
- **Cost efficiency** through fewer external API calls

**Key Principle**: When updating existing entities, respect the filter. When creating new entities, spawn all dependencies for completeness.

**Relationship to EnableDependencyRequests**: While `EnableDependencyRequests` prevents **reactive dependency requests** (job explosion safety), `IncludeLinkedDocumentTypes` controls **intentional child spawning** (operational efficiency). They work together to provide both safety and flexibility in document processing.
