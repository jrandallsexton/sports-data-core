# Document Processing Configuration

## Overview

This document describes the `DocumentProcessingConfig` feature flag system that controls how document processors handle missing dependencies.

## The Problem

Prior to this change, document processors used **reactive dependency requests**: when a processor discovered a missing dependency (e.g., an `AthleteSeason` referencing a `TeamSeason` that doesn't exist), it would publish a `DocumentRequested` event to trigger the sourcing of that dependency.

This approach created several issues:

1. **Cyclical Dependencies**: `AthleteSeasonDocumentProcessor` requests `TeamSeason` documents, but `TeamSeasonDocumentProcessor` spawns `AthleteSeason` documents for roster members. When the Provider seeds bulk `AthleteSeason` requests (from `/athletes` endpoint), many athletes reference teams not yet processed, causing an explosion of cross-requests.

2. **Job Explosion**: In one production incident, this cyclical dependency created 3.5M Hangfire jobs in the Provider service.

3. **Tight Coupling**: Processors became tightly coupled through reactive requests, making the dependency graph unpredictable and hard to reason about.

## The Solution: Feature Flag

The `DocumentProcessingConfig.EnableDependencyRequests` flag provides two operational modes:

### Safe Mode (EnableDependencyRequests = false) - DEFAULT & RECOMMENDED

- **Default behavior** to prevent cyclical dependencies
- When a dependency is missing, immediately throw exception and retry
- Log: `"Dependency not found. Will retry. EnableDependencyRequests=false."`
- Throw `ExternalDocumentNotSourcedException` to trigger Hangfire retry
- Relies on proper source ordering and Hangfire's retry mechanism
- No reactive requests = no cyclical dependencies

### Override Mode (EnableDependencyRequests = true)

- **Legacy/edge case override** for specific scenarios
- When a dependency is missing, publish a `DocumentRequested` event
- Log: `"Dependency not found. Raising DocumentRequested (override mode)."`
- Throw `ExternalDocumentNotSourcedException` to trigger Hangfire retry
- Can be used for specific document types if needed
- Use with caution - can cause job explosions if misconfigured

## Configuration

The configuration is managed through **Azure App Configuration**, not local appsettings.json files.

### Azure App Configuration Key

```
SportsData.Producer:DocumentProcessing:EnableDependencyRequests
```

**Type**: `boolean`  
**Default**: `false` (safe mode - no reactive requests)

### Recommended Settings by Environment

- **Local/Dev**: `false` (safe mode - prevents job explosions)
- **QA**: `false` (safe mode - test proper orchestration)
- **Production**: `false` (safe mode - prevent job explosions)
- **Override**: `true` (only for specific edge cases where reactive requests are needed)

### How to Update

1. Navigate to Azure App Configuration resource
2. Add or update the key: `SportsData.Producer:DocumentProcessing:EnableDependencyRequests`
3. Set value to `false` (recommended) or `true` (override for edge cases)
4. Restart the Producer service to pick up the new value

## Implementation Details

### Updated Processors

The following document processors have been updated with feature flag support:

1. **AthleteSeasonDocumentProcessor**
   - Athlete dependency (line ~95)
   - TeamSeason dependency (line ~290)
   - Position dependency (line ~345)

2. **TeamSeasonDocumentProcessor**
   - Franchise dependency (line ~125)
   - GroupSeason dependency (line ~200)

3. **EventCompetitionLeadersDocumentProcessor**
   - AthleteSeason dependency (line ~195)

### Code Pattern

```csharp
if (dependency is null)
{
    if (!_config.EnableDependencyRequests)
    {
        // Safe mode (default): immediate retry, no reactive request
        _logger.LogWarning(
            "Dependency not found. Will retry. EnableDependencyRequests=false. {Context}",
            context);
        throw new ExternalDocumentNotSourcedException(
            $"Dependency not found. Will retry when available.");
    }
    else
    {
        // Legacy mode: publish DocumentRequested event
        _logger.LogWarning(
            "Dependency not found. Raising DocumentRequested (override mode). {Context}",
            context);
        
        await _publishEndpoint.Publish(new DocumentRequested(...));
        await _dataContext.OutboxPings.AddAsync(new OutboxPing());
        await _dataContext.SaveChangesAsync();
        
        throw new ExternalDocumentNotSourcedException(
            $"Dependency not found for {ref}");
    }
}
```

## Testing Plan

1. **Baseline Test (EnableDependencyRequests = true)**
   - Deploy to dev environment
   - Trigger bulk athlete season sourcing
   - Monitor Hangfire job counts
   - Expected: High job counts due to reactive requests

2. **Feature Flag Test (EnableDependencyRequests = false)**
   - Update config to `false`
   - Restart Producer service
   - Trigger same bulk sourcing
   - Monitor Hangfire job counts
   - Expected: Significantly lower job counts, processors rely on retries

3. **Data Integrity Test**
   - Compare final database state between both modes
   - Verify all dependencies eventually resolve
   - Confirm no data loss

4. **Performance Test**
   - Measure processing time for both modes
   - Compare resource utilization
   - Monitor retry patterns

## Monitoring

Key metrics to track:

- **Hangfire Job Counts**: Should be dramatically lower in safe mode (`EnableDependencyRequests = false`)
- **Retry Rates**: May increase slightly (expected and healthy)
- **Processing Duration**: Should improve with fewer cross-requests
- **DocumentRequested Events**: Should drop to zero in safe mode (`EnableDependencyRequests = false`)

## Migration Strategy

1. **Phase 1 (Current)**: Deploy feature flag infrastructure with safe mode default
   - Default: `EnableDependencyRequests = false` (safe mode - no reactive requests)
   - Behavior: Processors throw exceptions on missing dependencies and rely on Hangfire retries
   - This CHANGES behavior from the previous reactive request pattern to prevent cyclical dependencies
   - Allows rollback via config toggle to `true` if data issues emerge

2. **Phase 2**: Validate in dev/QA
   - Monitor with `EnableDependencyRequests = false` (safe mode)
   - Confirm job counts remain low and data integrity is maintained
   - Test override mode (`true`) only if specific scenarios require reactive requests

3. **Phase 3**: Production deployment
   - Keep `EnableDependencyRequests = false` in prod config (safe mode)
   - Monitor Hangfire job counts, retry rates, and data completeness
   - Feature flag available to toggle to `true` if critical issues arise

4. **Phase 4** (Future): Remove legacy mode
   - After successful production run
   - Remove flag and reactive request code entirely
   - Simplify processors

## Architectural Philosophy

This change represents a shift from **reactive dependency resolution** to **orchestrated dependency resolution**:

- **Before**: Processors discover and request missing dependencies on-demand
- **After**: Trust the orchestration layer (Provider) to sequence documents correctly, and trust Hangfire's retry mechanism to handle timing issues

This is more aligned with event-driven architecture principles: processors should be **stateless, idempotent, and retry-safe**, not responsible for orchestrating document sourcing.

## Related Documentation

- [ROADMAP.md](./ROADMAP.md) - Future architectural improvements
- [SERVICES.md](./SERVICES.md) - Producer service overview
- [Hangfire Documentation](https://docs.hangfire.io/) - Retry and background job patterns

## Questions?

For questions or issues related to this feature flag, contact the sports-data-core team or refer to the Git history for this document.

---
**Last Updated**: 2024 (auto-generated during feature flag implementation)
