# SeasonYear Data Fix - Execution Guide

## Overview
This folder contains scripts to fix SeasonYear corruption affecting ~13,000 records across multiple tables from years 1949-2026.

## Execution Order

Run these scripts **in sequence** using your PostgreSQL client connected to `sdProducer.FootballNcaa`:

### ✅ Step 1: Verify Problem (Read-Only)
**File:** `01_verify_problem.sql`
- Shows the 24 bad root records
- Counts total affected records (~13,000)
- Displays sample data
- **Action:** Review output to understand scope

### ✅ Step 2: Fix Root GroupSeason Records
**File:** `02_fix_groupseason_roots.sql`
- Fixes 24 NCAA Football root records
- **Expected:** UPDATE 24
- **Action:** Verify all updates successful (0 mismatches)

### ✅ Step 3: Fix Child GroupSeason Records
**File:** `03_fix_groupseason_children.sql`
- Fixes ~1,242 child GroupSeason records
- **⚠️ IMPORTANT:** Run the UPDATE statement **repeatedly until it returns `UPDATE 0`** to ensure full convergence
- Each run fixes one additional level of the hierarchy
- **Expected:** Decreasing counts with each run, converging to `UPDATE 0`
- **Action:** Verify hierarchy alignment (0 mismatches)

### ✅ Step 4: Fix FranchiseSeason Records
**File:** `04_fix_franchiseseason.sql`
- Fixes ~1,335 FranchiseSeason records
- **Expected:** UPDATE ~1,335
- **Action:** Verify 0 mismatches remain

### ✅ Step 5: Fix Contest Records
**File:** `05_fix_contest.sql`
- Fixes ~6,161 Contest records
- **Expected:** UPDATE ~6,161
- **Action:** Verify 0 mismatches remain

### ✅ Step 6: Fix Rankings & Records
**File:** `06_fix_rankings_records.sql`
- Fixes FranchiseSeasonRanking (~260)
- Fixes FranchiseSeasonRecord (~3,973)
- Fixes FranchiseSeasonProjection (~0)
- **Expected:** Total ~4,233 updates
- **Action:** Verify all 3 tables show 0 mismatches

### ✅ Step 7: Final Verification (Read-Only)
**File:** `99_final_verification.sql`
- Comprehensive mismatch check across all tables
- Spot-checks specific corrected records
- Shows data distribution by year
- Provides sample Contest IDs for UI testing
- **Expected:** All mismatch counts = 0

## Testing Strategy

### After Each Step:
1. Run the PREVIEW queries first
2. Review expected vs actual counts
3. Run the UPDATE statement(s)
4. Run the VERIFY queries
5. Confirm 0 mismatches before proceeding

### Transaction Option:
Each script can be wrapped in a transaction for safety:
```sql
BEGIN;
-- Run script contents
COMMIT;   -- Or ROLLBACK if issues found
```

### UI Verification:
After completing all steps, use Contest IDs from `99_final_verification.sql` to spot-check in the UI that years display correctly.

## Affected Years
1949, 1954, 1968, 1971, 1975, 1977, 1979, 1980, 1981, 1988, 1994, 2001, 2013, 2014, 2015, 2016, 2017, 2018, 2019, 2020, 2021, 2022, 2024, 2026

## Safety Notes
- ✅ Working on local backup of production data
- ✅ Only updating denormalized SeasonYear field
- ✅ All foreign keys remain unchanged
- ✅ Can restore from snapshot if needed
- ✅ Each step includes verification queries

## Reference Files
- **`_ISSUE_SUMMARY_SeasonYear_Bug.md`** - Detailed analysis and root cause
- **`_FIX_SeasonYear_Corruption.sql`** - Original comprehensive script
- **`_QUICKSTART_SeasonYear_Fix.sql`** - Quick reference version

## Questions?
Review the issue summary for detailed explanation of the problem and fix strategy.

---
**Created:** March 2, 2026  
**Database:** sdProducer.FootballNcaa (Local Backup)  
**Total Records Affected:** ~13,000
