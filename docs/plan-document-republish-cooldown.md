# Plan: Time-Based Cooldown for Historical Document Re-Publishing

**Created:** 2026-03-24
**Status:** Draft — awaiting review

---

## 1. Problem Statement

PR #176 added publish suppression for historical documents: if Provider has already published a document and the content hasn't changed, it suppresses the `DocumentCreated` event. This solved the cyclical cascade problem (Event -> Team -> Event -> Team -> ...) that was inflating Hangfire queues by orders of magnitude during historical sourcing.

**The new problem:** suppression is permanent. Once a historical document is published, it will never be re-published (as long as the ESPN content hasn't changed). This blocks a legitimate use case: **re-running historical sourcing** to pick up documents that were missed due to DLQ backlog, processing bugs, or newly added processors.

Current workarounds are all heavy-handed:
1. Set `CurrentSeason = 0` (disables suppression entirely — cycles return)
2. Clear `LastPublishedContentHash` in MongoDB manually (operational burden, no granularity)
3. Hope the content changed (it won't — historical data is static)

---

## 2. Proposed Solution

Add a `LastPublishedUtc` timestamp to each document. Change the suppression check from "hash matches -> suppress always" to "hash matches AND published recently -> suppress". Once a configurable cooldown period expires, the document becomes eligible for re-publishing even if the content is identical.

### Suppression logic change

**Before (current):**
```text
historical + hash matches + no downstream work requested → SUPPRESS (permanent)
```

**After (proposed):**
```text
historical + hash matches + published within cooldown + no downstream work requested → SUPPRESS
historical + hash matches + cooldown expired + no downstream work requested → ALLOW re-publish
```

### Why this works

The cyclical cascade completes in **seconds**. A full historical sourcing run takes **hours**. A cooldown of 24 hours (default) prevents cycles within a run while allowing re-sourcing on subsequent runs. The two timescales are separated by orders of magnitude — there's no realistic scenario where a cascade loop persists for 24 hours.

---

## 3. Pros and Cons

### Pros

1. **Enables historical re-sourcing** without operational workarounds. Producer can request documents again and they'll flow through.
2. **Self-healing.** If documents were missed (DLQ, bugs, new processors), they'll naturally get re-published on the next sourcing run after the cooldown.
3. **Preserves cycle prevention.** Within the cooldown window, suppression works identically to today.
4. **Small, focused change.** One new field, one new config value, one additional condition. No new files needed.
5. **Configurable per environment.** Development can use a short cooldown (1 hour) for fast iteration; production can use 24 hours or longer.
6. **Fail-safe.** If `LastPublishedUtc` is null (existing documents without the field), behavior is "never published" = allow publish. Same graceful degradation as `LastPublishedContentHash`.

### Cons

1. **Not eliminating cycles, just rate-limiting them.** If a sourcing run somehow spans longer than the cooldown, documents from early in the run could become eligible for re-publishing before the run completes. Mitigation: 24-hour default far exceeds any realistic run duration.
2. **Additional MongoDB write per publish.** `LastPublishedUtc` is written alongside `LastPublishedContentHash` in the same `UpdateFieldAsync` call — but the method currently updates one field. Needs to update two fields atomically, or make two calls. Minor overhead either way.
3. **One more condition in an already multi-condition check.** The suppression logic already has 4 conditions; this adds a 5th. Still readable, but the comment block should be clear.

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Cooldown too short, cycles return | Low | Medium | Default 24h; document the minimum safe value |
| Cooldown too long, blocks legitimate re-sourcing | Low | Low | Configurable; can reduce per environment |
| Clock drift across Provider pods | Very Low | None | Using UTC; MongoDB timestamps are server-side |
| Existing documents lack `LastPublishedUtc` | Certain | None | Null = "never published" = allow publish (same pattern as hash) |

---

## 4. Configuration

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `SportsData.Provider:DocumentPublishCooldownMinutes` | int | 1440 (24h) | Minimum minutes between re-publishing unchanged historical documents. Set to 0 to disable cooldown (always re-publish — suppression still active via hash for current-run dedup). |

Placed under `SportsData.Provider` in Azure App Configuration alongside other Provider-specific settings like `HistoricalSourcing` and `EspnApiClientConfig`.

**Setting to 0** disables the cooldown but keeps hash-based suppression for same-content documents within a single request (no behavioral regression from today — documents without `LastPublishedUtc` are treated as "never published").

---

## 5. Implementation

### 5a. Add `LastPublishedUtc` to DocumentBase

**File:** `src/SportsData.Provider/Infrastructure/Data/DocumentBase.cs`

```csharp
/// <summary>
/// UTC timestamp of when DocumentCreated was last published for this document.
/// Used with LastPublishedContentHash to implement time-based cooldown for
/// historical document re-publishing. Null means never published.
/// </summary>
public DateTime? LastPublishedUtc { get; set; }
```

No MongoDB migration needed — new nullable field on existing documents defaults to null.

### 5b. Update `UpdateLastPublishedHashAsync`

**File:** `src/SportsData.Provider/Application/Processors/ResourceIndexItemProcessor.cs`

Rename to `UpdateLastPublishedStateAsync` and update both fields:

```csharp
private async Task UpdateLastPublishedStateAsync(string collectionName, string urlHash, string json)
{
    try
    {
        var contentHash = _jsonHashCalculator.NormalizeAndHash(json);
        await _documentStore.UpdateFieldAsync<DocumentBase>(
            collectionName, urlHash, nameof(DocumentBase.LastPublishedContentHash), contentHash);
        await _documentStore.UpdateFieldAsync<DocumentBase>(
            collectionName, urlHash, nameof(DocumentBase.LastPublishedUtc), DateTime.UtcNow);
    }
    catch (Exception ex)
    {
        // existing warning log
    }
}
```

Alternative: extend `UpdateFieldAsync` to accept multiple field/value pairs in a single MongoDB `UpdateOne` call. Slightly cleaner but changes the interface. Either approach works — two sequential `UpdateFieldAsync` calls are fine since they're on the same document and failure is already handled with a warning log (next request will re-publish).

### 5c. Update suppression logic

**File:** `src/SportsData.Provider/Application/Processors/ResourceIndexItemProcessor.cs` (lines 202-218)

```csharp
// For historical data, check if this exact content was already published
// within the cooldown window. If so, skip — Producer already processed it
// and re-publishing only creates churn. After the cooldown expires,
// allow re-publishing to support historical re-sourcing runs.
if (!IsCurrentSeason(command.SeasonYear) &&
    dbItem.LastPublishedContentHash is not null &&
    !command.NotifyOnCompletion &&
    (command.IncludeLinkedDocumentTypes is null || command.IncludeLinkedDocumentTypes.Count == 0))
{
    var contentHash = _jsonHashCalculator.NormalizeAndHash(dbItem.Data);
    if (contentHash == dbItem.LastPublishedContentHash &&
        IsWithinCooldown(dbItem.LastPublishedUtc))
    {
        _logger.LogInformation(
            "ESPN {CacheResult} for {DocumentType}. Content unchanged and within cooldown, skipping. Url={Url}",
            "HIT-SUPPRESSED", command.DocumentType, dbItem.Uri.OriginalString);
        return;
    }
}
```

New helper:

```csharp
private bool IsWithinCooldown(DateTime? lastPublishedUtc)
{
    if (!lastPublishedUtc.HasValue)
        return false; // never published — allow

    var cooldownStr = _commonConfig["SportsData.Provider:DocumentPublishCooldownMinutes"];
    if (string.IsNullOrWhiteSpace(cooldownStr) || !int.TryParse(cooldownStr, out var cooldownMinutes))
        return true; // misconfigured — default to suppressing (safe, matches current behavior)

    if (cooldownMinutes <= 0)
        return false; // cooldown disabled — always allow re-publish

    return (DateTime.UtcNow - lastPublishedUtc.Value).TotalMinutes < cooldownMinutes;
}
```

### 5d. Update design doc

**File:** `docs/document-publish-suppression.md`

- Add cooldown to the flow diagram (new decision node after hash match)
- Update "Behavior by scenario" table with cooldown-related rows
- Update "Key rules" section

### 5e. Update tests

**File:** `test/unit/SportsData.Provider.Tests.Unit/Application/Processors/ResourceIndexItemProcessorTests.cs`

New test cases:
- `WhenCacheHit_Historical_HashMatches_WithinCooldown_ShouldSuppress`
- `WhenCacheHit_Historical_HashMatches_CooldownExpired_ShouldPublish`
- `WhenCacheHit_Historical_HashMatches_NullLastPublishedUtc_ShouldPublish`
- `WhenCacheHit_Historical_HashMatches_CooldownDisabled_ShouldPublish`
- `WhenCacheHit_Historical_HashMatches_CooldownMisconfigured_ShouldSuppress`

Existing test `WhenCacheHit_Historical_AndHashMatches_ShouldSuppressPublish` needs update: the test `DocumentBase` must now also set `LastPublishedUtc` to a recent time for suppression to trigger.

---

## 6. Files Changed

| File | Change |
|------|--------|
| `src/SportsData.Provider/Infrastructure/Data/DocumentBase.cs` | Add `LastPublishedUtc` property |
| `src/SportsData.Provider/Application/Processors/ResourceIndexItemProcessor.cs` | Add `IsWithinCooldown()`, update suppression check, rename/update hash update method |
| `docs/document-publish-suppression.md` | Update flow diagram and scenario table |
| `test/.../ResourceIndexItemProcessorTests.cs` | 5 new tests, 1 updated test |
| **Total** | **4 files** (3 source + 1 test) |

---

## 7. Deployment

1. Deploy code with cooldown logic + new field
2. Existing documents have `LastPublishedUtc = null` — treated as "never published" — first request publishes and sets the timestamp
3. No MongoDB migration needed
4. Set `SportsData.Provider:DocumentPublishCooldownMinutes` in Azure App Configuration (default 1440 if absent = current suppression behavior with 24h window)

**Backward compatible.** If the config key is missing, `IsWithinCooldown` returns `true` (suppresses), matching today's permanent suppression. The feature only activates when the config is present.

---

## 8. Future Consideration

If two `UpdateFieldAsync` calls per publish becomes a concern (unlikely — it's two lightweight MongoDB `$set` operations on the same document), we could:
- Add an `UpdateFieldsAsync` method that accepts a dictionary of field/value pairs
- Use a single `UpdateOne` with multiple `$set` operators

Not worth doing now. Optimize if profiling shows it matters.
