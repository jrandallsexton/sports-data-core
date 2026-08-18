SELECT
  c."Sport" AS "Sport",
  sp."Year" AS "SeasonYear",
  sp."Name" AS "SeasonPhase",
  sw."Number" AS "WeekNumber",
  c."Id" AS "ContestId",
  cn."Headline" AS "Headline",
  c."StartDateUtc" AS "StartDateUtc",
  cs."StatusTypeName" AS "Status",
  cs."StatusDescription" AS "StatusDescription",
  v."Name" AS "Venue", v."City" AS "VenueCity", v."State" AS "VenueState",
  fsAway."Id" AS "AwayFranchiseSeasonId", fAway."DisplayName" AS "Away",
  fAway."Slug" AS "AwaySlug", fsrdAway."Current" AS "AwayRank",
  gsAway."Slug" AS "AwayConferenceSlug", gsAwayParent."Slug" AS "AwayParentConferenceSlug",
  COALESCE(enterAway."Wins", 0) AS "AwayWins", COALESCE(enterAway."Losses", 0) AS "AwayLosses",
  COALESCE(enterAway."ConferenceWins", 0) AS "AwayConferenceWins", COALESCE(enterAway."ConferenceLosses", 0) AS "AwayConferenceLosses",
  fsHome."Id" AS "HomeFranchiseSeasonId", fHome."DisplayName" AS "Home",
  fHome."Slug" AS "HomeSlug", fsrdHome."Current" AS "HomeRank",
  gsHome."Slug" AS "HomeConferenceSlug", gsHomeParent."Slug" AS "HomeParentConferenceSlug",
  COALESCE(enterHome."Wins", 0) AS "HomeWins", COALESCE(enterHome."Losses", 0) AS "HomeLosses",
  COALESCE(enterHome."ConferenceWins", 0) AS "HomeConferenceWins", COALESCE(enterHome."ConferenceLosses", 0) AS "HomeConferenceLosses",
  co."Details" AS "Spread", (co."Spread" * -1) AS "AwaySpread",
  co."Spread" AS "HomeSpread", co."OverUnder", co."OverOdds", co."UnderOdds"
FROM public."Contest" c
INNER JOIN public."SeasonPhase" sp ON sp."Id" = c."SeasonPhaseId"
INNER JOIN public."SeasonWeek" sw ON sw."Id" = c."SeasonWeekId"
LEFT JOIN public."Venue" v ON v."Id" = c."VenueId"
INNER JOIN public."Competition" comp ON comp."ContestId" = c."Id"
INNER JOIN public."CompetitionStatus" cs ON cs."CompetitionId" = comp."Id"
LEFT JOIN public."CompetitionNote" cn ON cn."CompetitionId" = comp."Id" AND cn."Type" = 'event'
LEFT JOIN LATERAL (
  SELECT * FROM public."CompetitionOdds"
  WHERE "CompetitionId" = comp."Id" AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
  ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
  LIMIT 1
) co ON TRUE
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
INNER JOIN public."GroupSeason" gsAway ON gsAway."Id" = fsAway."GroupSeasonId"
LEFT JOIN public."GroupSeason" gsAwayParent ON gsAway."ParentId" = gsAwayParent."Id"
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
INNER JOIN public."GroupSeason" gsHome ON gsHome."Id" = fsHome."GroupSeasonId"
LEFT JOIN public."GroupSeason" gsHomeParent ON gsHome."ParentId" = gsHomeParent."Id"
LEFT JOIN LATERAL (
  -- Rank via poll_rank_asof — the single poll-rank definition (see the
  -- PollRankAsofFunction migration): the poll in effect at kickoff,
  -- this team's entry in it, or NULL = honestly unranked.
  SELECT public.poll_rank_asof(fsAway."Id", fsAway."SeasonYear", c."StartDateUtc") AS "Current"
) fsrdAway ON TRUE
LEFT JOIN LATERAL (
  -- Rank via poll_rank_asof — the single poll-rank definition (see the
  -- PollRankAsofFunction migration): the poll in effect at kickoff,
  -- this team's entry in it, or NULL = honestly unranked.
  SELECT public.poll_rank_asof(fsHome."Id", fsHome."SeasonYear", c."StartDateUtc") AS "Current"
) fsrdHome ON TRUE

-- Entering record for the away team: taken from the most recent
-- completed game's CompetitionCompetitorRecord (ESPN snapshots each
-- team's record ON every game document) — point-in-time by
-- construction, and the CURRENT record for an upcoming contest. The
-- FranchiseSeason Wins/Losses denorm columns are abandoned (never
-- populated for NFL, inconsistent for NCAAFB — see #617); same proven
-- lateral as GetMatchupsByContestIds.
LEFT JOIN LATERAL (
  SELECT
    split_part(tot."Summary", '-', 1)::int  AS "Wins",
    split_part(tot."Summary", '-', 2)::int  AS "Losses",
    split_part(conf."Summary", '-', 1)::int AS "ConferenceWins",
    split_part(conf."Summary", '-', 2)::int AS "ConferenceLosses"
  FROM public."CompetitionCompetitor" prev_cc
  INNER JOIN public."Competition" prev_comp ON prev_comp."Id" = prev_cc."CompetitionId"
  INNER JOIN public."Contest" prev_ct ON prev_ct."Id" = prev_comp."ContestId"
  INNER JOIN public."CompetitionStatus" prev_cs ON prev_cs."CompetitionId" = prev_comp."Id"
  LEFT JOIN public."SeasonPhase" prev_sp ON prev_sp."Id" = prev_ct."SeasonPhaseId"
  INNER JOIN public."CompetitionCompetitorRecord" tot
    ON tot."CompetitionCompetitorId" = prev_cc."Id" AND tot."Type" = 'total'
  LEFT JOIN public."CompetitionCompetitorRecord" conf
    ON conf."CompetitionCompetitorId" = prev_cc."Id" AND conf."Type" = 'vsconf'
  WHERE prev_cc."FranchiseSeasonId" = fsAway."Id"
    AND prev_ct."StartDateUtc" < c."StartDateUtc"
    -- Preview-safe semantics, same as every other preview query: only
    -- FINALIZED, non-cancelled games anchor the record...
    AND prev_cs."StatusTypeName" = 'STATUS_FINAL'
    AND prev_ct."CancelledUtc" IS NULL
    -- ...and never a PRESEASON game (policy: preseason is system-testing,
    -- never signal) — without this, an NFL week-1 preview would anchor on
    -- the team's last preseason game and show preseason W/L as the
    -- current record.
    AND (prev_sp."TypeCode" IS NULL OR prev_sp."TypeCode" <> 1)
  ORDER BY prev_ct."StartDateUtc" DESC
  LIMIT 1
) enterAway ON TRUE
-- Entering record for the home team: taken from the most recent
-- completed game's CompetitionCompetitorRecord (ESPN snapshots each
-- team's record ON every game document) — point-in-time by
-- construction, and the CURRENT record for an upcoming contest. The
-- FranchiseSeason Wins/Losses denorm columns are abandoned (never
-- populated for NFL, inconsistent for NCAAFB — see #617); same proven
-- lateral as GetMatchupsByContestIds.
LEFT JOIN LATERAL (
  SELECT
    split_part(tot."Summary", '-', 1)::int  AS "Wins",
    split_part(tot."Summary", '-', 2)::int  AS "Losses",
    split_part(conf."Summary", '-', 1)::int AS "ConferenceWins",
    split_part(conf."Summary", '-', 2)::int AS "ConferenceLosses"
  FROM public."CompetitionCompetitor" prev_cc
  INNER JOIN public."Competition" prev_comp ON prev_comp."Id" = prev_cc."CompetitionId"
  INNER JOIN public."Contest" prev_ct ON prev_ct."Id" = prev_comp."ContestId"
  INNER JOIN public."CompetitionStatus" prev_cs ON prev_cs."CompetitionId" = prev_comp."Id"
  LEFT JOIN public."SeasonPhase" prev_sp ON prev_sp."Id" = prev_ct."SeasonPhaseId"
  INNER JOIN public."CompetitionCompetitorRecord" tot
    ON tot."CompetitionCompetitorId" = prev_cc."Id" AND tot."Type" = 'total'
  LEFT JOIN public."CompetitionCompetitorRecord" conf
    ON conf."CompetitionCompetitorId" = prev_cc."Id" AND conf."Type" = 'vsconf'
  WHERE prev_cc."FranchiseSeasonId" = fsHome."Id"
    AND prev_ct."StartDateUtc" < c."StartDateUtc"
    -- Preview-safe semantics, same as every other preview query: only
    -- FINALIZED, non-cancelled games anchor the record...
    AND prev_cs."StatusTypeName" = 'STATUS_FINAL'
    AND prev_ct."CancelledUtc" IS NULL
    -- ...and never a PRESEASON game (policy: preseason is system-testing,
    -- never signal) — without this, an NFL week-1 preview would anchor on
    -- the team's last preseason game and show preseason W/L as the
    -- current record.
    AND (prev_sp."TypeCode" IS NULL OR prev_sp."TypeCode" <> 1)
  ORDER BY prev_ct."StartDateUtc" DESC
  LIMIT 1
) enterHome ON TRUE
WHERE c."Id" = ANY(@ContestIds)
