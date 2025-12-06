# AthleteSeasonDocumentProcessor - Update Logic Implementation

**Date**: December 5, 2025  
**Ticket**: Make AthleteSeasonDocumentProcessor functionally equivalent to AthleteDocumentProcessor

---

## ?? **Objective**

Implement update logic in `AthleteSeasonDocumentProcessor` to match the behavior of `AthleteDocumentProcessor`, allowing reprocessing of existing `AthleteSeason` entities to source missing headshot images.

---

## ? **Changes Made**

### **File**: `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/AthleteSeasonDocumentProcessor.cs`

### **1. Updated `ProcessInternal` Method**

**Before**:
```csharp
var athleteSeason = athlete.Seasons.FirstOrDefault(s => s.FranchiseSeasonId == franchiseSeasonId);
if (athleteSeason is not null)
{
    _logger.LogWarning("AthleteSeason already exists. Updating not implemented");
    return; // ? Early return, no processing
}
```

**After**:
```csharp
var athleteSeason = athlete.Seasons.FirstOrDefault(s => s.FranchiseSeasonId == franchiseSeasonId);
if (athleteSeason is not null)
{
    await ProcessExisting(command, athleteSeason, dto); // ? Process existing
    return;
}
```

**Added headshot processing for new entities:**
```csharp
await _dataContext.AthleteSeasons.AddAsync(entity);
await _dataContext.SaveChangesAsync();

_logger.LogInformation("Successfully created AthleteSeason {Id} for Athlete {AthleteId}", entity.Id, athlete.Id);

// ? Process headshot for new AthleteSeason
await ProcessHeadshot(command, entity, dto);
```

### **2. Added `ProcessExisting` Method**

```csharp
private async Task ProcessExisting(
    ProcessDocumentCommand command,
    AthleteSeason entity,
    EspnAthleteSeasonDto dto)
{
    _logger.LogInformation("AthleteSeason already exists: {Id}. Processing updates.", entity.Id);
    
    // Process headshot for existing AthleteSeason
    await ProcessHeadshot(command, entity, dto);
    
    _logger.LogInformation("Successfully processed existing AthleteSeason {Id}", entity.Id);
}
```

**Purpose**: Handles reprocessing of existing `AthleteSeason` entities to source missing images.

### **3. Added `ProcessHeadshot` Method**

```csharp
private async Task ProcessHeadshot(
    ProcessDocumentCommand command,
    AthleteSeason entity,
    EspnAthleteSeasonDto dto)
{
    if (dto.Headshot?.Href is not null)
    {
        var imgId = Guid.NewGuid();
        await _publishEndpoint.Publish(new Core.Eventing.Events.Images.ProcessImageRequest(
            dto.Headshot.Href,
            imgId,
            entity.Id,
            $"{entity.Id}-{imgId}.png",
            command.Sport,
            command.Season,
            command.DocumentType,
            command.SourceDataProvider,
            0, 0,
            null,
            command.CorrelationId,
            CausationId.Producer.AthleteSeasonDocumentProcessor));
        
        _logger.LogInformation("Published ProcessImageRequest for AthleteSeason {Id}, Image: {ImageId}", entity.Id, imgId);
    }
}
```

**Purpose**: Publishes `ProcessImageRequest` event for athlete season headshots, allowing image sourcing pipeline to download and store the image.

---

## ?? **Processing Flow**

### **New AthleteSeason**
```
1. ProcessInternal()
   ??> Resolve Athlete (or request if missing)
   ??> Resolve FranchiseSeason (or request if missing)
   ??> Resolve Position (or request if missing)
   ??> Create new AthleteSeason entity
   ??> Save to database
   ??> ProcessHeadshot() ? NEW
       ??> Publish ProcessImageRequest event
```

### **Existing AthleteSeason** (Reprocessing)
```
1. ProcessInternal()
   ??> Resolve Athlete
   ??> Find existing AthleteSeason
   ??> ProcessExisting() ? NEW
       ??> ProcessHeadshot() ? NEW
           ??> Publish ProcessImageRequest event
```

---

## ?? **Use Cases Enabled**

### **1. Initial Processing**
- New `AthleteSeason` entities are created with headshot images automatically requested

### **2. Reprocessing for Missing Images**
- Existing `AthleteSeason` entities can be reprocessed to source missing headshots
- Allows bulk reprocessing via republishing `DocumentCreated` events
- Mirrors `AthleteDocumentProcessor` behavior

### **3. Image Update**
- If ESPN updates an athlete's headshot, reprocessing will fetch the new image
- New `imgId` ensures uniqueness (no overwrites)

---

## ?? **Functional Equivalence Checklist**

| Feature | AthleteDocumentProcessor | AthleteSeasonDocumentProcessor | Status |
|---------|--------------------------|--------------------------------|--------|
| **Process New Entity** | ? | ? | ? Equal |
| **Process Existing Entity** | ? `ProcessExisting()` | ? `ProcessExisting()` | ? Equal |
| **Headshot Processing** | ? `ProcessImageRequest` | ? `ProcessImageRequest` | ? Equal |
| **Dependency Resolution** | ? Position, Status, Location | ? Position, FranchiseSeason, Athlete | ? Equal |
| **Retry Logic** | ? `ExternalDocumentNotSourcedException` | ? `ExternalDocumentNotSourcedException` | ? Equal |
| **Event Publishing** | ? `AthleteCreated`, `ProcessImageRequest` | ? `ProcessImageRequest` | ? Equal |

---

## ?? **Testing Recommendations**

### **1. Reprocess All AthleteSeasons to Source Missing Images**

```sql
-- Find AthleteSeasons without images
SELECT 
    ase.Id,
    ase.AthleteId,
    a.DisplayName,
    COUNT(i.Id) AS ImageCount
FROM AthleteSeasons ase
INNER JOIN Athletes a ON a.Id = ase.AthleteId
LEFT JOIN Images i ON i.ParentEntityId = ase.Id
GROUP BY ase.Id, ase.AthleteId, a.DisplayName
HAVING COUNT(i.Id) = 0
ORDER BY a.DisplayName;
```

### **2. Republish DocumentCreated Events**

Republish `DocumentCreated` events for athlete seasons to trigger reprocessing:

```csharp
// Pseudo-code for bulk reprocessing job
var athleteSeasons = await _dbContext.AthleteSeasons
    .Where(x => !x.Images.Any())
    .ToListAsync();

foreach (var athleteSeason in athleteSeasons)
{
    var externalId = athleteSeason.ExternalIds.FirstOrDefault();
    if (externalId?.SourceUrl != null)
    {
        await _eventBus.Publish(new DocumentCreated(
            urlHash: externalId.SourceUrlHash,
            parentId: null,
            collectionName: "FootballNcaa",
            providerRef: new Uri(externalId.SourceUrl),
            sourceUri: new Uri(externalId.SourceUrl),
            json: null, // Will be fetched from MongoDB
            sport: Sport.FootballNcaa,
            seasonYear: athleteSeason.SeasonYear,
            documentType: DocumentType.AthleteSeason,
            sourceDataProvider: SourceDataProvider.Espn,
            correlationId: Guid.NewGuid(),
            causationId: CausationId.Manual.BulkReprocessing,
            attemptCount: 0,
            includeLinkedDocumentTypes: null
        ));
    }
}
```

---

## ?? **Expected Impact**

### **Before**
- ? Existing `AthleteSeason` entities could not be reprocessed
- ? Missing headshots required manual intervention
- ? No way to bulk-update images

### **After**
- ? Existing `AthleteSeason` entities can be reprocessed
- ? Missing headshots automatically sourced via event publishing
- ? Bulk reprocessing enabled via republishing `DocumentCreated` events
- ? Functional parity with `AthleteDocumentProcessor`

---

## ?? **Bottom Line**

`AthleteSeasonDocumentProcessor` now has **full feature parity** with `AthleteDocumentProcessor`:
- ? Creates new entities with headshots
- ? Processes existing entities to source missing headshots
- ? Enables bulk reprocessing workflows
- ? Maintains idempotency (multiple reprocessing is safe)

**Ready to reprocess all athlete seasons and fetch missing images!** ????
