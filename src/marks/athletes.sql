-- Per-athlete data dump for the marks batch.
--
-- Run against the per-sport Producer DB (sdProducer.BaseballMlb /
-- FootballNcaa / FootballNfl). Each row gives the marks batch
-- everything it needs to render one avatar:
--   - AthleteId        — the row in AthleteImage we'll insert against
--   - DisplayName      — used to derive FL initials (the default)
--   - Jersey           — fallback when initials can't be derived
--   - TeamSlug         — diagnostic only (which team's colors the avatar uses)
--   - Primary/Secondary — team palette for the disc/ring
--
-- The lateral join picks each athlete's MOST RECENT AthleteSeason that
-- has a team affiliation (FranchiseSeasonId IS NOT NULL). Historical-only
-- athletes get rendered in whatever team's colors they last played for —
-- there's nothing else to use.
--
-- INNER JOIN drops athletes that never had any AthleteSeason with a team
-- (no colors to render with → would scream anyway → skip).
--
-- Output: tab-separated with a header row (no SQL preamble in the file).

SELECT
    a."Id"               AS "AthleteId",
    a."DisplayName"      AS "DisplayName",
    most_recent."Jersey" AS "Jersey",
    f."Slug"             AS "TeamSlug",
    fs."ColorCodeHex"     AS "Primary",
    fs."ColorCodeAltHex"  AS "Secondary"
FROM public."Athlete" a
INNER JOIN LATERAL (
    SELECT ats."Jersey", ats."FranchiseSeasonId"
    FROM public."AthleteSeason" ats
    INNER JOIN public."FranchiseSeason" fs
        ON fs."Id" = ats."FranchiseSeasonId"
    WHERE ats."AthleteId" = a."Id"
      AND ats."FranchiseSeasonId" IS NOT NULL
      AND fs."SeasonYear" = 2026
    ORDER BY fs."SeasonYear" DESC, ats."CreatedUtc" DESC
    LIMIT 1
) most_recent ON TRUE
INNER JOIN public."FranchiseSeason" fs
    ON fs."Id" = most_recent."FranchiseSeasonId"
INNER JOIN public."Franchise" f
    ON f."Id" = fs."FranchiseId" and f."IsActive" = true
--WHERE a."IsActive" = true
ORDER BY a."LastName" ASC, a."FirstName" ASC;
