# SeasonYear Corruption Issue - Analysis & Remediation Plan

## Executive Summary

A bug in the application caused **massive data corruption** affecting **12,995+ records** across multiple tables spanning **years 1949-2026**. All affected records have `SeasonYear = 2023` when they should reflect their actual year.

---

## Problem Discovery

### Initial Report
- User identified 2 `GroupSeason` records for NCAA Football
- Both showed `SeasonYear = 2023`
- External IDs revealed one had SourceUrl for 2016, other for 2023

### Actual Scope (Much Worse!)
Investigation revealed **24 NCAA Football GroupSeason records** all incorrectly showing `SeasonYear = 2023`:

**Affected Years:** 1949, 1954, 1968, 1971, 1975, 1977, 1979, 1980, 1981, 1988, 1994, 2001, 2013, 2014, 2015, 2016, 2017, 2018, 2019, 2020, 2021, 2022, 2024, 2026

---

## Root Cause Analysis

### The Bug
When creating `GroupSeason` records, the application:
1. ✅ Correctly set `SeasonId` FK (pointing to correct Season record)
2. ❌ Incorrectly set `SeasonYear` = 2023 (hardcoded or wrong variable)

### Why SeasonYear Exists
`SeasonYear` is denormalized across many tables to reduce join overhead in queries. It's redundant with the Season FK but improves query performance.

### Cascading Impact
Because `SeasonYear` is denormalized across tables, child records inherited the wrong year:
- Child GroupSeason records copied from parent
- FranchiseSeason copied from GroupSeason
- Contest, Rankings, Records copied from FranchiseSeason

---

## Data Impact Assessment

### Records Affected

| Table | Count | Source of Truth |
|-------|------:|----------------|
| **GroupSeason** | 1,266 | Season.Year (via SeasonId FK) |
| **FranchiseSeason** | 1,335 | GroupSeason.SeasonYear |
| **Contest** | 6,161 | FranchiseSeason.SeasonYear |
| **FranchiseSeasonRanking** | 260 | FranchiseSeason.SeasonYear |
| **FranchiseSeasonRecord** | 3,973 | FranchiseSeason.SeasonYear |
| **Total** | **12,995+** | |

### Good News
- ✅ All `SeasonId` foreign keys are **CORRECT**
- ✅ All `GroupSeasonId` foreign keys are **CORRECT**
- ✅ Source data in external feeds is **CORRECT**
- ✅ Only `SeasonYear` denormalized field is wrong

---

## Remediation Strategy

### Fix Order (Cascade Down Hierarchy)
```
1. GroupSeason (root)    ← FROM Season.Year
   ↓
2. GroupSeason (children) ← FROM parent GroupSeason.SeasonYear
   ↓
3. FranchiseSeason        ← FROM GroupSeason.SeasonYear
   ↓
4. Contest                ← FROM FranchiseSeason.SeasonYear
5. FranchiseSeasonRanking ← FROM FranchiseSeason.SeasonYear
6. FranchiseSeasonRecord  ← FROM FranchiseSeason.SeasonYear
7. FranchiseSeasonProjection ← FROM FranchiseSeason.SeasonYear
8. SeasonPoll             ← FROM Season.Year (if exists)
```

### Safety Measures
- ✅ SQL script includes verification queries before each step
- ✅ Can be run as transaction (BEGIN/COMMIT/ROLLBACK)
- ✅ Preview counts before executing updates
- ✅ Final verification queries confirm all mismatches resolved
- ✅ Working on local backup of production data

---

## Files Created

1. **`_FIX_SeasonYear_Corruption.sql`**
   - Complete remediation script with 8 steps
   - Includes verification queries
   - Transaction-safe
   - ~500 lines with detailed comments

2. **`_ISSUE_SUMMARY_SeasonYear_Bug.md`** (this file)
   - Problem analysis and documentation

---

## Next Steps

### 1. Review & Test (DO THIS FIRST!)
```sql
-- Connect to local backup database
-- Open _FIX_SeasonYear_Corruption.sql
-- Run verification queries at top (lines 27-65)
-- Review the data to confirm understanding
```

### 2. Test Fix in Transaction
```sql
BEGIN;

-- Run Steps 1-8 one at a time
-- Review results after each step
-- Run final verification (see 'Final Verification' step in script)

ROLLBACK; -- Undo to test again
-- OR
COMMIT;   -- Apply changes
```

### 3. Production Deployment Considerations

**Option A: Restore & Fix Offline**
- Restore prod backup to local/staging
- Run fix script
- Backup fixed database
- Restore to production during maintenance window

**Option B: Direct Production Fix**
- Schedule maintenance window
- Run script inside transaction
- Verify results
- Commit if successful, rollback if issues

**Recommendation:** Option A is safer for this volume of changes

### 4. Application Bug Fix
After data is remediated, **FIX THE APPLICATION BUG** to prevent recurrence:
- Find where GroupSeason.SeasonYear is being set
- Ensure it uses Season.Year, not hardcoded/cached value
- Add validation/constraint if possible
- Add unit test to verify SeasonYear matches Season.Year

---

## Questions for User

1. **How do you want to proceed?**
   - Test on local backup first? ✅ (Recommended)
   - Need help running the script?
   - Want to review specific sections first?

2. **Application Code**
   - Do you want help finding the bug in the application code?
   - What language/framework? (to help locate the issue)

3. **Testing**
   - After fix, which queries should we run to verify data integrity?
   - Any specific use cases to test?

---

## Risk Assessment

### Low Risk ✅
- Only fixing denormalized SeasonYear field
- Not touching FK relationships (all correct)
- Not deleting data
- Reversible via transaction
- Working on backup

### Validation
FK constraints protect referential integrity only — they do **not** validate `SeasonYear` consistency:
- `GroupSeason.SeasonId` → `Season.Id` ✅ FK is correct
- `FranchiseSeason.GroupSeasonId` → `GroupSeason.Id` ✅ FK is correct
- `Contest.HomeTeamFranchiseSeasonId` → `FranchiseSeason.Id` ✅ FK is correct

The bug is in the **denormalized** `SeasonYear` column. FK constraints remain valid even when
`SeasonYear` carries the wrong value — FK integrity **will not** catch a bad fix here.
Validate by recomputing `SeasonYear` from `Season.Year` (via `GroupSeason.SeasonId`) and
confirming all downstream tables agree.

---

## Success Criteria

✅ All `GroupSeason.SeasonYear` = `Season.Year` (via SeasonId)  
✅ All child GroupSeasons have SeasonYear = parent SeasonYear  
✅ All FranchiseSeason have SeasonYear = GroupSeason.SeasonYear  
✅ All Contest have SeasonYear = FranchiseSeason.SeasonYear  
✅ All other tables aligned with FranchiseSeason.SeasonYear  
✅ Final verification queries return 0 mismatches

---

## Created
- **Date:** March 2, 2026
- **Database:** sdProducer.FootballNcaa (Local Backup)
- **Author:** GitHub Copilot (Analysis & Remediation)
