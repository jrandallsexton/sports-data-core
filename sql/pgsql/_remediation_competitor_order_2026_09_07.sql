-- Remediation 2026-09-07: competitor rows stranded by the Order-index swap
-- deadlock (ESPN home/away + order re-designation; the HomeAway parking dance
-- committed, then the tail save died on IX_CompetitionCompetitor_CompetitionId_Order,
-- leaving rows at HomeAway='swap'). Three competitions affected — including
-- Wisconsin @ Notre Dame (Lambeau, played 09-06, UNFINALIZED because the
-- stranded row blocks enrichment: "Competition is missing away or home
-- competitor" — pick scoring for that game is stuck until this heals).
-- The deadlock began 2026-09-07 00:00:41Z, i.e. ESPN's post-game republish.
-- Run against sdProducer.FootballNcaa.
--
-- Verified state before writing this script (prod, 2026-09-07 ~17:15Z):
--   f0901b0b... WI/ND:            Badgers  swap/0 | Fighting Irish home/1
--   ddddc645... Howard@AlaA&M:    Bison    away/0 | Bulldogs swap/1   (08-29, FinalizedUtc NULL)
--   3a25ea0d... AlaSt@Southern:   Hornets  away/0 | Jaguars  swap/1   (08-29, FinalizedUtc NULL)
--
-- Target (per ESPN current designations): away team Order 1, home team Order 0
-- for WI/ND (ND home/0, Wisconsin away/1). The two 08-29 games only need the
-- 'swap' side healed — their orders are consistent, and the deployed code fix
-- converges any later ESPN order flip on its own.

BEGIN;

-- ── Wisconsin @ Notre Dame (competition f0901b0b-6578-5a8d-81fe-7f81ecdeafcb)
-- Step 1: park Notre Dame's Order out of the way (frees 1, -1 cannot collide)
UPDATE public."CompetitionCompetitor"
SET "Order" = -1
WHERE "CompetitionId" = 'f0901b0b-6578-5a8d-81fe-7f81ecdeafcb'
  AND "HomeAway" = 'home';           -- Fighting Irish (currently home/1)

-- Step 2: Wisconsin takes away/1 (both its side and order become correct)
UPDATE public."CompetitionCompetitor"
SET "HomeAway" = 'away', "Order" = 1
WHERE "CompetitionId" = 'f0901b0b-6578-5a8d-81fe-7f81ecdeafcb'
  AND "HomeAway" = 'swap';           -- Badgers (currently swap/0)

-- Step 3: Notre Dame takes Order 0 (slot vacated in step 2)
UPDATE public."CompetitionCompetitor"
SET "Order" = 0
WHERE "CompetitionId" = 'f0901b0b-6578-5a8d-81fe-7f81ecdeafcb'
  AND "HomeAway" = 'home';

-- ── Howard @ Alabama A&M (ddddc645-fa0b-bda1-ad2e-b78c0196cdd9): un-park only
UPDATE public."CompetitionCompetitor"
SET "HomeAway" = 'home'
WHERE "CompetitionId" = 'ddddc645-fa0b-bda1-ad2e-b78c0196cdd9'
  AND "HomeAway" = 'swap';           -- Bulldogs

-- ── Alabama State @ Southern (3a25ea0d-2e88-6af0-f3bc-4804440c0aed): un-park only
UPDATE public."CompetitionCompetitor"
SET "HomeAway" = 'home'
WHERE "CompetitionId" = '3a25ea0d-2e88-6af0-f3bc-4804440c0aed'
  AND "HomeAway" = 'swap';           -- Jaguars

-- Verify: 6 rows, no 'swap', no -1, each competition one home/0-or-1 + one away
SELECT cc."CompetitionId", f."Name", cc."HomeAway", cc."Order"
FROM public."CompetitionCompetitor" cc
JOIN public."FranchiseSeason" fs ON fs."Id" = cc."FranchiseSeasonId"
JOIN public."Franchise" f ON f."Id" = fs."FranchiseId"
WHERE cc."CompetitionId" IN (
  'f0901b0b-6578-5a8d-81fe-7f81ecdeafcb',
  'ddddc645-fa0b-bda1-ad2e-b78c0196cdd9',
  '3a25ea0d-2e88-6af0-f3bc-4804440c0aed')
ORDER BY cc."CompetitionId", cc."Order";

COMMIT;

-- Post-heal follow-ups (operator): all three contests show FinalizedUtc NULL.
-- After healing, reenrich (or let the 6-hourly audit catch them):
--   POST /admin/contest/dab8ad91-3464-583a-527b-667e61ef3e27/reenrich?sport=FootballNcaa  (Wisconsin @ Notre Dame, 09-06)
--   POST /admin/contest/1064ad09-1507-57f8-5851-2cccb72c3e67/reenrich?sport=FootballNcaa  (Howard @ Alabama A&M, 08-29)
--   POST /admin/contest/6a7c0e75-cfef-7b3e-e54d-a1922a3c6b99/reenrich?sport=FootballNcaa  (Alabama St @ Southern, 08-29)
