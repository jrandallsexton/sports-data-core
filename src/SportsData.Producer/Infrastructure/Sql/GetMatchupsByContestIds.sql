SELECT
  c."SeasonWeekId" AS "SeasonWeekId",
  sw_contest."EndDate" AS "SeasonWeekEndDate",
  c."Id" AS "ContestId",
  c."StartDateUtc" AS "StartDateUtc",
  cn."Headline" AS "Headline",
  cs."StatusTypeName" AS "Status",
  cs."StatusDescription" AS "StatusDescription",
  STRING_AGG(cb."MediaName", ' | ') AS "Broadcasts",
  v."Name" AS "Venue", v."City" AS "VenueCity", v."State" AS "VenueState",
  fAway."DisplayName" AS "Away", fAway."Abbreviation" AS "AwayShort",
  fsAway."Id" AS "AwayFranchiseSeasonId",
  COALESCE(fslAway."Uri", flAway."Uri") AS "AwayLogoUri",
  COALESCE(fslDarkAway."Uri", flDarkAway."Uri") AS "AwayLogoUriDark",
  fAway."Slug" AS "AwaySlug", fAway."ColorCodeHex" AS "AwayColor",
  fsrdAway."Current" AS "AwayRank", gsAway."Slug" AS "AwayConferenceSlug",
  COALESCE(enterAway."Wins", 0) AS "AwayWins", COALESCE(enterAway."Losses", 0) AS "AwayLosses",
  COALESCE(enterAway."ConferenceWins", 0) AS "AwayConferenceWins", COALESCE(enterAway."ConferenceLosses", 0) AS "AwayConferenceLosses",
  fHome."DisplayName" AS "Home", fHome."Abbreviation" AS "HomeShort",
  fsHome."Id" AS "HomeFranchiseSeasonId",
  COALESCE(fslHome."Uri", flHome."Uri") AS "HomeLogoUri",
  COALESCE(fslDarkHome."Uri", flDarkHome."Uri") AS "HomeLogoUriDark",
  fHome."Slug" AS "HomeSlug", fHome."ColorCodeHex" AS "HomeColor",
  fsrdHome."Current" AS "HomeRank", gsHome."Slug" AS "HomeConferenceSlug",
  COALESCE(enterHome."Wins", 0) AS "HomeWins", COALESCE(enterHome."Losses", 0) AS "HomeLosses",
  COALESCE(enterHome."ConferenceWins", 0) AS "HomeConferenceWins", COALESCE(enterHome."ConferenceLosses", 0) AS "HomeConferenceLosses",
  co."Details" AS "SpreadCurrentDetails", co."Spread" AS "SpreadCurrent",
  cto."SpreadPointsOpen" AS "SpreadOpen",
  co."OverUnder" AS "OverUnderCurrent", co."TotalPointsOpen" AS "OverUnderOpen",
  co."OverOdds", co."UnderOdds",
  co."ProviderName" AS "ProviderName",
  c."AwayScore", c."HomeScore",
  c."WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseSeasonId",
  c."OverUnder" AS "OverUnderResult",
  c."EndDateUtc" AS "CompletedUtc"
FROM public."Contest" c
INNER JOIN public."SeasonWeek" sw_contest ON sw_contest."Id" = c."SeasonWeekId"
LEFT JOIN public."Venue" v ON v."Id" = c."VenueId"
INNER JOIN public."Competition" comp ON comp."ContestId" = c."Id"
LEFT JOIN public."CompetitionNote" cn ON cn."CompetitionId" = comp."Id" AND cn."Type" = 'event'
LEFT JOIN public."CompetitionBroadcast" cb ON cb."CompetitionId" = comp."Id"
LEFT JOIN public."CompetitionStatus" cs ON cs."CompetitionId" = comp."Id"
LEFT JOIN LATERAL (
  SELECT * FROM public."CompetitionOdds"
  WHERE "CompetitionId" = comp."Id" AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
  ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
  LIMIT 1
) co ON TRUE
LEFT JOIN public."CompetitionTeamOdds" cto ON cto."CompetitionOddsId" = co."Id" AND cto."Side" = 'Home'
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
-- Logo selection: FAIL-CLOSED (2026-07-18). Every lateral filters to
-- sportdeets-mark rows only (`Rel @> ARRAY['sportdeets-mark']`) and NEVER falls
-- through to an ESPN / untagged row — a null result renders a placeholder, never
-- a licensed logo. See docs/logo-license-audit.md. Order among marks: the
-- requested @Direction first, then any mark; CreatedUtc ASC breaks ties.
-- COALESCE(season, franchise) prefers a season mark, else the franchise mark;
-- because a mark-less season now yields NULL, the franchise mark is correctly
-- reached (the prior fail-open shadowing is gone). Dark laterals are identical —
-- marks are theme-agnostic, so the same mark serves both backgrounds.
LEFT JOIN LATERAL (
  SELECT fl.* FROM public."FranchiseLogo" fl
  WHERE fl."FranchiseId" = fAway."Id"
    AND fl."Rel" @> ARRAY['sportdeets-mark']::text[]
  ORDER BY
    CASE WHEN fl."Rel" @> ARRAY['sportdeets-mark', @Direction]::text[] THEN 0 ELSE 1 END,
    fl."CreatedUtc" ASC
  LIMIT 1
) flAway ON TRUE
LEFT JOIN LATERAL (
  SELECT fsl.* FROM public."FranchiseSeasonLogo" fsl
  WHERE fsl."FranchiseSeasonId" = fsAway."Id"
    AND fsl."Rel" @> ARRAY['sportdeets-mark']::text[]
  ORDER BY
    CASE WHEN fsl."Rel" @> ARRAY['sportdeets-mark', @Direction]::text[] THEN 0 ELSE 1 END,
    fsl."CreatedUtc" ASC
  LIMIT 1
) fslAway ON TRUE
LEFT JOIN LATERAL (
  SELECT fl.* FROM public."FranchiseLogo" fl
  WHERE fl."FranchiseId" = fAway."Id"
    AND fl."Rel" @> ARRAY['sportdeets-mark']::text[]
  ORDER BY
    CASE WHEN fl."Rel" @> ARRAY['sportdeets-mark', @Direction]::text[] THEN 0 ELSE 1 END,
    fl."CreatedUtc" ASC
  LIMIT 1
) flDarkAway ON TRUE
LEFT JOIN LATERAL (
  SELECT fsl.* FROM public."FranchiseSeasonLogo" fsl
  WHERE fsl."FranchiseSeasonId" = fsAway."Id"
    AND fsl."Rel" @> ARRAY['sportdeets-mark']::text[]
  ORDER BY
    CASE WHEN fsl."Rel" @> ARRAY['sportdeets-mark', @Direction]::text[] THEN 0 ELSE 1 END,
    fsl."CreatedUtc" ASC
  LIMIT 1
) fslDarkAway ON TRUE
INNER JOIN public."GroupSeason" gsAway ON gsAway."Id" = fsAway."GroupSeasonId"
LEFT JOIN LATERAL (
  SELECT fsr.* FROM public."FranchiseSeasonRanking" fsr
  INNER JOIN public."SeasonWeek" sw ON sw."Id" = fsr."SeasonWeekId"
  WHERE fsr."FranchiseSeasonId" = fsAway."Id"
    AND fsr."DefaultRanking" = true AND fsr."Type" IN ('ap', 'cfp')
    AND sw."StartDate" <= c."StartDateUtc"
  ORDER BY sw."StartDate" DESC LIMIT 1
) fsrAway ON TRUE
LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdAway ON fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
-- Entering record: the record the away team carried INTO this game = its record
-- THROUGH its most-recent prior competition that has a 'total' record (same
-- FranchiseSeason, earlier StartDate). Point-in-time, unlike the mutable
-- FranchiseSeason W/L. Parsed from ESPN's CompetitionCompetitorRecord Summary
-- ("6-2"). Null (→ 0 via COALESCE) at the season opener or an un-sourced gap.
-- See docs/features/point-in-time-team-records.md.
LEFT JOIN LATERAL (
  SELECT
    split_part(tot."Summary", '-', 1)::int  AS "Wins",
    split_part(tot."Summary", '-', 2)::int  AS "Losses",
    split_part(conf."Summary", '-', 1)::int AS "ConferenceWins",
    split_part(conf."Summary", '-', 2)::int AS "ConferenceLosses"
  FROM public."CompetitionCompetitor" prev_cc
  INNER JOIN public."Competition" prev_comp ON prev_comp."Id" = prev_cc."CompetitionId"
  INNER JOIN public."Contest" prev_ct ON prev_ct."Id" = prev_comp."ContestId"
  INNER JOIN public."CompetitionCompetitorRecord" tot
    ON tot."CompetitionCompetitorId" = prev_cc."Id" AND tot."Type" = 'total'
  -- LEFT (not INNER) on purpose: an FBS independent (Notre Dame, UConn, …) has
  -- no conference and carries no 'vsconf' record, so INNER would exclude ALL
  -- their games and blank the overall record. 'total' (the INNER join above) is
  -- the authoritative driver; a missing 'vsconf' correctly yields 0-0 conference.
  LEFT JOIN public."CompetitionCompetitorRecord" conf
    ON conf."CompetitionCompetitorId" = prev_cc."Id" AND conf."Type" = 'vsconf'
  WHERE prev_cc."FranchiseSeasonId" = fsAway."Id"
    AND prev_ct."StartDateUtc" < c."StartDateUtc"
  ORDER BY prev_ct."StartDateUtc" DESC
  LIMIT 1
) enterAway ON TRUE
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
LEFT JOIN LATERAL (
  SELECT fl.* FROM public."FranchiseLogo" fl
  WHERE fl."FranchiseId" = fHome."Id"
    AND fl."Rel" @> ARRAY['sportdeets-mark']::text[]
  ORDER BY
    CASE WHEN fl."Rel" @> ARRAY['sportdeets-mark', @Direction]::text[] THEN 0 ELSE 1 END,
    fl."CreatedUtc" ASC
  LIMIT 1
) flHome ON TRUE
LEFT JOIN LATERAL (
  SELECT fsl.* FROM public."FranchiseSeasonLogo" fsl
  WHERE fsl."FranchiseSeasonId" = fsHome."Id"
    AND fsl."Rel" @> ARRAY['sportdeets-mark']::text[]
  ORDER BY
    CASE WHEN fsl."Rel" @> ARRAY['sportdeets-mark', @Direction]::text[] THEN 0 ELSE 1 END,
    fsl."CreatedUtc" ASC
  LIMIT 1
) fslHome ON TRUE
LEFT JOIN LATERAL (
  SELECT fl.* FROM public."FranchiseLogo" fl
  WHERE fl."FranchiseId" = fHome."Id"
    AND fl."Rel" @> ARRAY['sportdeets-mark']::text[]
  ORDER BY
    CASE WHEN fl."Rel" @> ARRAY['sportdeets-mark', @Direction]::text[] THEN 0 ELSE 1 END,
    fl."CreatedUtc" ASC
  LIMIT 1
) flDarkHome ON TRUE
LEFT JOIN LATERAL (
  SELECT fsl.* FROM public."FranchiseSeasonLogo" fsl
  WHERE fsl."FranchiseSeasonId" = fsHome."Id"
    AND fsl."Rel" @> ARRAY['sportdeets-mark']::text[]
  ORDER BY
    CASE WHEN fsl."Rel" @> ARRAY['sportdeets-mark', @Direction]::text[] THEN 0 ELSE 1 END,
    fsl."CreatedUtc" ASC
  LIMIT 1
) fslDarkHome ON TRUE
INNER JOIN public."GroupSeason" gsHome ON gsHome."Id" = fsHome."GroupSeasonId"
LEFT JOIN LATERAL (
  SELECT fsr.* FROM public."FranchiseSeasonRanking" fsr
  INNER JOIN public."SeasonWeek" sw ON sw."Id" = fsr."SeasonWeekId"
  WHERE fsr."FranchiseSeasonId" = fsHome."Id"
    AND fsr."DefaultRanking" = true AND fsr."Type" IN ('ap', 'cfp')
    AND sw."StartDate" <= c."StartDateUtc"
  ORDER BY sw."StartDate" DESC LIMIT 1
) fsrHome ON TRUE
LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdHome ON fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
-- Entering record for the home team — same lag as enterAway above.
LEFT JOIN LATERAL (
  SELECT
    split_part(tot."Summary", '-', 1)::int  AS "Wins",
    split_part(tot."Summary", '-', 2)::int  AS "Losses",
    split_part(conf."Summary", '-', 1)::int AS "ConferenceWins",
    split_part(conf."Summary", '-', 2)::int AS "ConferenceLosses"
  FROM public."CompetitionCompetitor" prev_cc
  INNER JOIN public."Competition" prev_comp ON prev_comp."Id" = prev_cc."CompetitionId"
  INNER JOIN public."Contest" prev_ct ON prev_ct."Id" = prev_comp."ContestId"
  INNER JOIN public."CompetitionCompetitorRecord" tot
    ON tot."CompetitionCompetitorId" = prev_cc."Id" AND tot."Type" = 'total'
  LEFT JOIN public."CompetitionCompetitorRecord" conf
    ON conf."CompetitionCompetitorId" = prev_cc."Id" AND conf."Type" = 'vsconf'
  WHERE prev_cc."FranchiseSeasonId" = fsHome."Id"
    AND prev_ct."StartDateUtc" < c."StartDateUtc"
  ORDER BY prev_ct."StartDateUtc" DESC
  LIMIT 1
) enterHome ON TRUE
WHERE c."Id" = ANY(@ContestIds)
GROUP BY
  c."SeasonWeekId", sw_contest."EndDate", c."Id", c."StartDateUtc", cn."Headline", cs."StatusTypeName", cs."StatusDescription",
  v."Name", v."City", v."State",
  fAway."DisplayName", fAway."DisplayNameShort", fsAway."Id",
  flAway."Uri", fslAway."Uri", flDarkAway."Uri", fslDarkAway."Uri", fAway."Slug",
  fsrdAway."Current", gsAway."Slug",
  enterAway."Wins", enterAway."Losses", enterAway."ConferenceWins", enterAway."ConferenceLosses",
  fAway."Abbreviation", fAway."ColorCodeHex",
  fHome."Abbreviation", fHome."ColorCodeHex",
  fHome."DisplayName", fHome."DisplayNameShort", fsHome."Id",
  flHome."Uri", fslHome."Uri", flDarkHome."Uri", fslDarkHome."Uri", fHome."Slug",
  fsrdHome."Current", gsHome."Slug",
  enterHome."Wins", enterHome."Losses", enterHome."ConferenceWins", enterHome."ConferenceLosses",
  co."Details", co."Spread", co."OverUnder", co."OverOdds", co."UnderOdds",
  co."ProviderName",
  cto."SpreadPointsOpen", co."TotalPointsOpen",
  c."AwayScore", c."HomeScore", c."WinnerFranchiseSeasonId", c."SpreadWinnerFranchiseSeasonId",
  c."OverUnder", c."EndDateUtc"
ORDER BY c."StartDateUtc", fHome."Slug"
