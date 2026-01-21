# Document Inclusion Service - Centralizing Size Limit Logic

## Overview

The `DocumentInclusionService` provides centralized logic for determining whether JSON documents should be included inline in event payloads or sent as references only. This prevents exceeding Azure Service Bus message size limits.

## Problem Statement

### Before: Duplicated Logic

The logic for checking document size limits and deciding whether to include JSON was duplicated across multiple processors:

1. **ResourceIndexItemProcessor** - When creating/updating documents from ESPN API
2. **PublishDocumentEventsProcessor** - When bulk publishing existing documents

Both implementations had the same hardcoded constants and logic:

```csharp
// Duplicated in ResourceIndexItemProcessor.HandleNewDocumentAsync()
const int MAX_INLINE_JSON_BYTES = 204_800; // 200 KB in bytes

var jsonSizeInBytes = json.GetSizeInBytes();
var jsonDoc = jsonSizeInBytes <= MAX_INLINE_JSON_BYTES ? json : null;

if (jsonDoc == null)
{
    _logger.LogInformation(
        "Document JSON size ({SizeKB} KB) exceeds {MaxKB} KB limit, sending reference only",
        jsonSizeInBytes / 1024.0,
        MAX_INLINE_JSON_BYTES / 1024);
}
```

**Also duplicated in**:
- `ResourceIndexItemProcessor.HandleUpdatedDocumentAsync()`
- Future: `PublishDocumentEventsProcessor` (after this fix)

### Issues with Duplication

1. **Maintenance Burden**: Changes to size limits require updating multiple locations
2. **Inconsistency Risk**: Easy to miss one location, causing different behaviors
3. **Testing Complexity**: Same logic must be tested in multiple contexts
4. **Configuration Inflexibility**: Hardcoded constants prevent runtime configuration
5. **No Single Source of Truth**: Unclear where the authoritative size limit is defined

## Solution: Centralized Service

### New Service: `IDocumentInclusionService`

```csharp
public interface IDocumentInclusionService
{
    /// <summary>
    /// Determines if the JSON document should be included in the event payload
    /// or if only a reference should be sent (requiring the consumer to fetch it).
    /// </summary>
    string? GetIncludableJson(string json);

    /// <summary>
    /// Checks if the JSON document size exceeds the maximum inline size limit.
    /// </summary>
    bool ExceedsSizeLimit(string json);

    /// <summary>
    /// Gets the size of the JSON document in bytes.
    /// </summary>
    int GetDocumentSize(string json);

    /// <summary>
    /// Gets the maximum allowed inline JSON size in bytes.
    /// </summary>
    int MaxInlineJsonBytes { get; }
}
```

### Implementation

```csharp
public class DocumentInclusionService : IDocumentInclusionService
{
    private readonly ILogger<DocumentInclusionService> _logger;

    // Azure Service Bus limits:
    // - Standard tier: 256 KB max message size
    // - Premium tier: 1 MB max message size
    // Using conservative 200 KB limit (204,800 bytes) to allow for overhead
    // and other event properties (correlationId, causationId, metadata, etc.)
    private const int MAX_INLINE_JSON_BYTES = 204_800; // 200 KB

    public int MaxInlineJsonBytes => MAX_INLINE_JSON_BYTES;

    public string? GetIncludableJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var jsonSizeInBytes = json.GetSizeInBytes();

        if (jsonSizeInBytes <= MAX_INLINE_JSON_BYTES)
            return json;

        _logger.LogInformation(
            "Document JSON size ({SizeKB:F2} KB) exceeds {MaxKB} KB limit. Sending reference only.",
            jsonSizeInBytes / 1024.0,
            MAX_INLINE_JSON_BYTES / 1024);

        return null;
    }

    public bool ExceedsSizeLimit(string json)
    {
        if (string.IsNullOrEmpty(json))
            return false;

        return json.GetSizeInBytes() > MAX_INLINE_JSON_BYTES;
    }

    public int GetDocumentSize(string json)
    {
        if (string.IsNullOrEmpty(json))
            return 0;

        return json.GetSizeInBytes();
    }
}
```

## Usage

### 1. ResourceIndexItemProcessor

**Before**:
```csharp
private async Task HandleNewDocumentAsync(...)
{
    // ... document creation ...

    const int MAX_INLINE_JSON_BYTES = 204_800;
    var jsonSizeInBytes = json.GetSizeInBytes();
    var jsonDoc = jsonSizeInBytes <= MAX_INLINE_JSON_BYTES ? json : null;
    
    if (jsonDoc == null)
    {
        _logger.LogInformation(
            "Document JSON size ({SizeKB} KB) exceeds {MaxKB} KB limit",
            jsonSizeInBytes / 1024.0,
            MAX_INLINE_JSON_BYTES / 1024);
    }

    var evt = new DocumentCreated(..., jsonDoc, ...);
    await _publisher.Publish(evt);
}
```

**After**:
```csharp
private readonly IDocumentInclusionService _documentInclusionService;

private async Task HandleNewDocumentAsync(...)
{
    // ... document creation ...

    // Use the DocumentInclusionService to determine if JSON should be included
    var jsonDoc = _documentInclusionService.GetIncludableJson(json);

    var evt = new DocumentCreated(..., jsonDoc, ...);
    await _publisher.Publish(evt);
}
```

### 2. PublishDocumentEventsProcessor

**Before**:
```csharp
var events = batch.Select(doc =>
    new DocumentCreated(
        doc.Id.ToString(),
        null,
        typeAndName.Type.Name,
        doc.Uri,
        doc.Uri,
        doc.Data,  // ? Always includes JSON, regardless of size!
        doc.SourceUrlHash,
        // ... other parameters
    )).ToList();
```

**After**:
```csharp
private readonly IDocumentInclusionService _documentInclusionService;

var events = batch.Select(doc =>
    new DocumentCreated(
        doc.Id.ToString(),
        null,
        typeAndName.Type.Name,
        doc.Uri,
        doc.Uri,
        _documentInclusionService.GetIncludableJson(doc.Data), // ? Respects size limits
        doc.SourceUrlHash,
        // ... other parameters
    )).ToList();
```

## Benefits

### 1. **Single Source of Truth**
- Size limit defined in ONE place
- Consistent behavior across all processors
- Clear ownership of the logic

### 2. **Easier Maintenance**
- Change size limit once, affects all callers
- Update logging format in one location
- Add instrumentation/metrics centrally

### 3. **Testability**
- Service can be unit tested independently
- Easy to mock in processor tests
- Clear contract via interface

### 4. **Future Configurability**
- Easy to move limit to Azure App Config
- Can make limit configurable per document type
- Can add telemetry/monitoring hooks

### 5. **Better Observability**
- Centralized logging shows all oversized documents
- Can add metrics (% of docs included vs. referenced)
- Easy to track size distribution

## Design Decisions

### Why a Service Instead of Extension Method?

**Considered**:
```csharp
public static string? GetIncludableJson(this string json, ILogger logger) { ... }
```

**Rejected because**:
- Requires passing logger everywhere (awkward)
- Can't easily swap implementations (no DI)
- Can't add state (future: metrics, counters)
- Extension methods make testing harder

### Why Not Static?

**Considered**:
```csharp
public static class DocumentInclusionHelper
{
    public static string? GetIncludableJson(string json) { ... }
}
```

**Rejected because**:
- No dependency injection
- Can't mock in tests
- Can't inject logger
- Can't make configurable without global state

### Why Interface + Implementation?

? **Chosen approach**:
- Standard DI pattern
- Easy to mock for testing
- Can swap implementations (e.g., config-driven vs. hardcoded)
- Clear contract
- Future: Can add decorators (metrics, caching, etc.)

## Azure Service Bus Size Limits

### Context

Azure Service Bus has different message size limits based on tier:

| Tier | Max Message Size | Our Limit | Headroom |
|------|-----------------|-----------|----------|
| **Standard** | 256 KB | 200 KB | 56 KB (22%) |
| **Premium** | 1 MB | 200 KB | 824 KB (82%) |

### Why 200 KB?

We chose **200 KB** (204,800 bytes) as the limit because:

1. **Works on Standard Tier**: Fits within 256 KB limit
2. **Overhead Buffer**: Leaves room for:
   - Event metadata (correlationId, causationId, etc.)
   - Message envelope
   - Routing headers
   - Azure Service Bus metadata
3. **Conservative Safety Margin**: 22% headroom prevents edge cases

### What Happens When Limit is Exceeded?

**Without this service** (old code):
```csharp
var evt = new DocumentCreated(..., doc.Data, ...); // Includes 500 KB JSON
await _bus.Publish(evt); // ? Throws MessageSizeExceededException!
```

**With this service**:
```csharp
var jsonDoc = _documentInclusionService.GetIncludableJson(doc.Data); // Returns null
var evt = new DocumentCreated(..., null, ...); // Reference only
await _bus.Publish(evt); // ? Succeeds (small message)

// Producer fetches document via Provider API:
var json = await _providerClient.GetDocumentAsync(doc.SourceUrlHash);
```

## Future Enhancements

### 1. Configurable Limit

Move limit to Azure App Config:

```csharp
public class DocumentInclusionService : IDocumentInclusionService
{
    private readonly IConfiguration _config;
    private readonly int _maxInlineJsonBytes;

    public DocumentInclusionService(IConfiguration config)
    {
        _config = config;
        _maxInlineJsonBytes = _config.GetValue<int>(
            "SportsData.Provider:DocumentInclusion:MaxInlineBytes", 
            204_800);
    }

    public int MaxInlineJsonBytes => _maxInlineJsonBytes;
}
```

### 2. Document Type-Specific Limits

Different limits for different document types:

```csharp
public interface IDocumentInclusionService
{
    string? GetIncludableJson(string json, DocumentType documentType);
}

// Configuration:
{
  "DocumentInclusion": {
    "DefaultMaxBytes": 204800,
    "PerType": {
      "Event": 100000,          // Events are large, smaller limit
      "TeamSeasonRank": 512000   // Rankings are small, larger limit OK
    }
  }
}
```

### 3. Metrics & Telemetry

Track inclusion rates:

```csharp
public string? GetIncludableJson(string json)
{
    var size = json.GetSizeInBytes();
    var included = size <= MaxInlineJsonBytes;

    _metrics.RecordDocumentSize(size, included);

    if (included)
    {
        _metrics.IncrementCounter("documents.included");
        return json;
    }

    _metrics.IncrementCounter("documents.referenced");
    _logger.LogInformation(...);
    return null;
}
```

Dashboard metrics:
- **Inclusion Rate**: % of documents included vs. referenced
- **Size Distribution**: Histogram of document sizes
- **Oversized Documents**: Count and types of large documents
- **Fetch Rate**: How often Producer must call Provider API

### 4. Compression Option

For documents just over the limit, try compression:

```csharp
public string? GetIncludableJson(string json)
{
    var size = json.GetSizeInBytes();

    if (size <= MaxInlineJsonBytes)
        return json;

    // Try gzip compression
    var compressed = GzipCompress(json);
    if (compressed.Length <= MaxInlineJsonBytes)
    {
        _logger.LogInformation("Document compressed from {Original}KB to {Compressed}KB",
            size / 1024.0, compressed.Length / 1024.0);
        return Convert.ToBase64String(compressed); // Return compressed
    }

    _logger.LogInformation("Document too large even compressed, sending reference");
    return null;
}
```

## Testing

### Unit Tests

```csharp
public class DocumentInclusionServiceTests
{
    [Fact]
    public void GetIncludableJson_SmallDocument_ReturnsJson()
    {
        var service = new DocumentInclusionService(_logger);
        var smallJson = new string('x', 1000); // 1 KB

        var result = service.GetIncludableJson(smallJson);

        Assert.Equal(smallJson, result);
    }

    [Fact]
    public void GetIncludableJson_LargeDocument_ReturnsNull()
    {
        var service = new DocumentInclusionService(_logger);
        var largeJson = new string('x', 300_000); // 300 KB

        var result = service.GetIncludableJson(largeJson);

        Assert.Null(result);
    }

    [Fact]
    public void GetIncludableJson_ExactLimit_ReturnsJson()
    {
        var service = new DocumentInclusionService(_logger);
        var json = new string('x', service.MaxInlineJsonBytes);

        var result = service.GetIncludableJson(json);

        Assert.Equal(json, result);
    }

    [Fact]
    public void ExceedsSizeLimit_SmallDocument_ReturnsFalse()
    {
        var service = new DocumentInclusionService(_logger);
        var smallJson = new string('x', 1000);

        Assert.False(service.ExceedsSizeLimit(smallJson));
    }

    [Fact]
    public void ExceedsSizeLimit_LargeDocument_ReturnsTrue()
    {
        var service = new DocumentInclusionService(_logger);
        var largeJson = new string('x', 300_000);

        Assert.True(service.ExceedsSizeLimit(largeJson));
    }

    [Fact]
    public void GetDocumentSize_ReturnsCorrectSize()
    {
        var service = new DocumentInclusionService(_logger);
        var json = new string('x', 1000);

        var size = service.GetDocumentSize(json);

        Assert.Equal(1000, size);
    }
}
```

### Integration Tests

```csharp
[Fact]
public async Task PublishDocumentEventsProcessor_LargeDocuments_SendsReferences()
{
    // Arrange
    var largeDoc = new DocumentBase
    {
        Id = "test-id",
        Data = new string('x', 300_000) // 300 KB
    };
    
    await _documentStore.InsertOneAsync("test-collection", largeDoc);

    // Act
    await _processor.Process(new PublishDocumentEventsCommand { ... });

    // Assert
    _eventBusMock.Verify(x => x.Publish(
        It.Is<DocumentCreated>(e => e.Document == null), // ? No JSON included
        It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task ResourceIndexItemProcessor_SmallDocument_IncludesJson()
{
    // Arrange
    var smallJson = "{\"id\": 123}";
    _espnApiMock.Setup(x => x.GetResource(It.IsAny<Uri>(), false))
        .ReturnsAsync(smallJson);

    // Act
    await _processor.Process(new ProcessResourceIndexItemCommand { ... });

    // Assert
    _eventBusMock.Verify(x => x.Publish(
        It.Is<DocumentCreated>(e => e.Document == smallJson), // ? JSON included
        It.IsAny<CancellationToken>()), Times.Once);
}
```

## Migration Checklist

- [x] Create `IDocumentInclusionService` interface
- [x] Implement `DocumentInclusionService`
- [x] Register service in DI container (`ServiceRegistration.cs`)
- [x] Update `ResourceIndexItemProcessor.HandleNewDocumentAsync()`
- [x] Update `ResourceIndexItemProcessor.HandleUpdatedDocumentAsync()`
- [x] Update `PublishDocumentEventsProcessor`
- [x] Verify build succeeds
- [ ] Add unit tests for `DocumentInclusionService`
- [ ] Add integration tests
- [ ] Deploy to dev environment
- [ ] Monitor inclusion vs. reference rates
- [ ] Deploy to production

## Related Documentation

- [BATCHED_DOCUMENT_PROCESSING.md](./BATCHED_DOCUMENT_PROCESSING.md) - Batched processing to prevent OOM
- [INCLUDE_LINKED_DOCUMENT_TYPES.md](./INCLUDE_LINKED_DOCUMENT_TYPES.md) - Selective child document spawning

## Conclusion

The `DocumentInclusionService` centralizes the critical logic for respecting Azure Service Bus size limits, providing:

- ? **Consistency** - Same logic everywhere
- ? **Maintainability** - Single source of truth
- ? **Testability** - Easy to unit test and mock
- ? **Observability** - Centralized logging and metrics
- ? **Configurability** - Future-ready for runtime config

This refactoring eliminates code duplication, reduces maintenance burden, and sets the foundation for future enhancements like per-document-type limits, compression, and telemetry.

---

**Created**: January 21, 2026  
**PR**: TBD  
**Deployed**: TBD
