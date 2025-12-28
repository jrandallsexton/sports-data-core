# Safe Logging Update - Remove {@Command} from All Processors

## Context

**CodeRabbit Feedback:** Logging `{@Command}` exposes the entire JSON document (potentially 100KB+) in logs, which:
- ? Is verbose and clutters logs
- ? Could leak sensitive data
- ? Makes Seq queries slow with huge payloads

**Solution:** Extract just the **ESPN `$ref` URI** for debugging (can hit Postman to inspect) without logging full JSON.

---

## Implementation

### Added to ProcessDocumentCommand:

```csharp
/// <summary>
/// Extracts the ESPN $ref URI from the JSON document for logging purposes.
/// Returns null if $ref cannot be found or parsed.
/// </summary>
public string? GetDocumentRef()
{
    try
    {
        using var jsonDoc = JsonDocument.Parse(Document);
        if (jsonDoc.RootElement.TryGetProperty("$ref", out var refElement))
        {
            return refElement.GetString();
        }
    }
    catch
    {
        // Silently ignore parsing errors - this is best-effort logging
    }
    
    return null;
}
```

---

## Required Changes for All 15 Processors

**Pattern:**

### Before:
```csharp
_logger.LogInformation("ProcessorName started. {@Command}", command);
```

### After:
```csharp
_logger.LogInformation("ProcessorName started. Ref={Ref}, UrlHash={UrlHash}", 
    command.GetDocumentRef(),
    command.UrlHash);
```

---

## Processors to Update

- [x] ? EventDocumentProcessor
- [x] ? EventCompetitionDocumentProcessor
- [x] ? EventCompetitionCompetitorDocumentProcessor
- [ ] EventCompetitionCompetitorScoreDocumentProcessor
- [ ] EventCompetitionCompetitorLineScoreDocumentProcessor
- [ ] EventCompetitionOddsDocumentProcessor
- [ ] EventCompetitionStatusDocumentProcessor
- [ ] EventCompetitionSituationDocumentProcessor
- [ ] EventCompetitionBroadcastDocumentProcessor
- [ ] EventCompetitionPlayDocumentProcessor
- [ ] EventCompetitionLeadersDocumentProcessor
- [ ] EventCompetitionPredictionDocumentProcessor
- [ ] EventCompetitionProbabilityDocumentProcessor
- [ ] EventCompetitionPowerIndexDocumentProcessor
- [ ] EventCompetitionDriveDocumentProcessor

---

## Benefits

### Before Logs (Verbose):
```
[INFO] EventCompetitionPlayDocumentProcessor started. 
{
  "SourceDataProvider": "Espn",
  "Sport": "FootballNcaa",
  "DocumentType": "EventCompetitionPlay",
  "Document": "{\"$ref\":\"http://...\",\"id\":\"401628334123\",\"sequenceNumber\":1,...<100KB of JSON>...}",
  "CorrelationId": "abc123-...",
  "Season": 2024,
  "ParentId": "def456-...",
  "SourceUri": "http://...",
  "UrlHash": "abc123def",
  "AttemptCount": 0
}
```

### After Logs (Clean):
```
[INFO] EventCompetitionPlayDocumentProcessor started. 
Ref=http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/plays/401628334123,
UrlHash=abc123def
```

**User can copy Ref ? paste into Postman ? inspect ESPN data directly!** ??

---

## Scope Context Still Available

All critical fields remain in `BeginScope`:
```csharp
using (_logger.BeginScope(new Dictionary<string, object> {
    ["CorrelationId"] = command.CorrelationId,
    ["DocumentType"] = command.DocumentType,
    ["Season"] = command.Season ?? 0,
    ["CompetitionId"] = command.ParentId ?? "Unknown"
}))
```

So every log in the processor automatically has:
- ? CorrelationId
- ? DocumentType
- ? Season
- ? ParentId

Plus the start message adds:
- ? Ref (ESPN URI for Postman)
- ? UrlHash (unique document identifier)

---

## Impact

**Seq Log Size Reduction:** ~90% smaller logs (100KB ? 1KB per start message)  
**Debugging Capability:** **IMPROVED** - Can hit ESPN API directly with Ref  
**Security:** No sensitive data exposure  
**Performance:** Seq queries faster with smaller payloads
