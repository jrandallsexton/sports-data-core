# Quick Reference: xUnit Parallel Testing

## TL;DR - Run This Now

```powershell
# From repository root
.\scripts\Setup-XunitParallelization.ps1

# Measure improvement
Measure-Command { dotnet test --no-build }
```

**Expected:** 50-60% faster test execution

---

## Current Status ?

Your tests ARE running in parallel (xUnit default), but you have no explicit configuration.

---

## What We're Adding

**File:** `xunit.runner.json` in each test project

```json
{
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 0  // 0 = use all CPU cores
}
```

---

## Tuning Guide

If tests become flaky after enabling max parallelization:

### Option 1: Reduce Thread Count
```json
{
  "maxParallelThreads": 4  // Limit to 4 concurrent threads
}
```

### Option 2: Disable for Specific Tests
```csharp
[Collection("Sequential")]
public class ProblematicTests { }

[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollection { }
```

### Option 3: Fix Shared State
```csharp
// BAD - shared between tests
private static readonly SemaphoreSlim _lock = new(1, 1);

// GOOD - instance per test
private readonly SemaphoreSlim _lock = new(1, 1);
```

---

## Performance Expectations

| Environment | Before | After | Improvement |
|-------------|--------|-------|-------------|
| Local (8 cores) | ~45s | ~15-20s | **60%** |
| CI/CD (4 cores) | ~90s | ~30-40s | **55%** |

---

## Troubleshooting

### Tests fail only when run in parallel?
- You have shared state between tests
- Look for: static fields, shared database collections, shared files

### Tests are slower with parallelization?
- Your machine doesn't have many cores
- Tests are I/O bound (waiting on network/disk)
- Try `maxParallelThreads: 4` instead of `0`

### Some tests timeout?
- Increase test timeouts
- Tests are competing for resources (CPU/memory)

---

## Files Created

? `docs/testing/XUNIT_PARALLEL_CONFIGURATION.md` - Full analysis  
? `scripts/Setup-XunitParallelization.ps1` - Setup automation  
? This file - Quick reference

---

## One-Liner Setup

```powershell
.\scripts\Setup-XunitParallelization.ps1 && dotnet test
```

Done. ??
