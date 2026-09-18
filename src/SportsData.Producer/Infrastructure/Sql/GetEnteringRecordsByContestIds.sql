-- Entering records for a batch of contests, derived from OUTCOMES.
--
-- "Entering record" = the record each team carried INTO this contest: its
-- wins/losses across prior finalized, non-preseason contests. Counted from
-- Contest.WinnerFranchiseSeasonId, which is the same source pick scoring and
-- FranchiseSeason enrichment agree on.
--
-- Deliberately NOT sourced from:
--   * FranchiseSeason.Wins   - mutable and CURRENT, so it answers "what is
--     this team's record now", not "what was it that day". Useless for any
--     week but the live one.
--   * CompetitionCompetitorRecord.Summary - ESPN's own record string, which
--     means "entering" before a game and "after" once re-sourced post-game.
--     The same column carries two different meanings and nothing marks which
--     (prod 2026-09-18: 20 of 32 NFL week-1 rows still held their pre-game
--     0-0 while 6 had been refreshed to post-game values).
--
-- A tie (WinnerFranchiseSeasonId IS NULL on a finalized contest) counts as
-- neither a win nor a loss, matching EnrichFranchiseSeasonHandler.
--
-- Conference record: prior opponents sharing the team's GroupSeasonId. Same
-- comparison EnrichFranchiseSeasonHandler makes.
SELECT
  c."Id"                       AS "ContestId",
  c."AwayTeamFranchiseSeasonId" AS "AwayFranchiseSeasonId",
  c."HomeTeamFranchiseSeasonId" AS "HomeFranchiseSeasonId",
  away."Wins"                  AS "AwayWins",
  away."Losses"                AS "AwayLosses",
  away."ConferenceWins"        AS "AwayConferenceWins",
  away."ConferenceLosses"      AS "AwayConferenceLosses",
  home."Wins"                  AS "HomeWins",
  home."Losses"                AS "HomeLosses",
  home."ConferenceWins"        AS "HomeConferenceWins",
  home."ConferenceLosses"      AS "HomeConferenceLosses"
FROM public."Contest" c
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
-- One lateral per side. Both walk the same prior-contest set; the only
-- difference is whose FranchiseSeason anchors it.
LEFT JOIN LATERAL (
  SELECT
    COUNT(*) FILTER (WHERE prev."WinnerFranchiseSeasonId" = fsAway."Id")                                              AS "Wins",
    COUNT(*) FILTER (WHERE prev."WinnerFranchiseSeasonId" IS NOT NULL
                       AND prev."WinnerFranchiseSeasonId" <> fsAway."Id")                                             AS "Losses",
    COUNT(*) FILTER (WHERE prev."WinnerFranchiseSeasonId" = fsAway."Id"     AND opp."GroupSeasonId" = fsAway."GroupSeasonId") AS "ConferenceWins",
    COUNT(*) FILTER (WHERE prev."WinnerFranchiseSeasonId" IS NOT NULL
                       AND prev."WinnerFranchiseSeasonId" <> fsAway."Id"
                       AND opp."GroupSeasonId" = fsAway."GroupSeasonId")                                              AS "ConferenceLosses"
  FROM public."Contest" prev
  -- The opponent in that prior contest, for the conference comparison.
  INNER JOIN public."FranchiseSeason" opp
    ON opp."Id" = CASE WHEN prev."AwayTeamFranchiseSeasonId" = fsAway."Id"
                       THEN prev."HomeTeamFranchiseSeasonId"
                       ELSE prev."AwayTeamFranchiseSeasonId" END
  LEFT JOIN public."SeasonPhase" prev_sp ON prev_sp."Id" = prev."SeasonPhaseId"
  WHERE (prev."AwayTeamFranchiseSeasonId" = fsAway."Id" OR prev."HomeTeamFranchiseSeasonId" = fsAway."Id")
    AND prev."StartDateUtc" < c."StartDateUtc"
    AND prev."FinalizedUtc" IS NOT NULL
    -- Preseason is system-testing, never signal. NULL/unmatched phase is KEPT,
    -- matching the entering-record laterals in the matchup queries.
    AND (prev_sp."TypeCode" IS NULL OR prev_sp."TypeCode" <> 1)
) away ON TRUE
LEFT JOIN LATERAL (
  SELECT
    COUNT(*) FILTER (WHERE prev."WinnerFranchiseSeasonId" = fsHome."Id")                                              AS "Wins",
    COUNT(*) FILTER (WHERE prev."WinnerFranchiseSeasonId" IS NOT NULL
                       AND prev."WinnerFranchiseSeasonId" <> fsHome."Id")                                             AS "Losses",
    COUNT(*) FILTER (WHERE prev."WinnerFranchiseSeasonId" = fsHome."Id"     AND opp."GroupSeasonId" = fsHome."GroupSeasonId") AS "ConferenceWins",
    COUNT(*) FILTER (WHERE prev."WinnerFranchiseSeasonId" IS NOT NULL
                       AND prev."WinnerFranchiseSeasonId" <> fsHome."Id"
                       AND opp."GroupSeasonId" = fsHome."GroupSeasonId")                                              AS "ConferenceLosses"
  FROM public."Contest" prev
  INNER JOIN public."FranchiseSeason" opp
    ON opp."Id" = CASE WHEN prev."AwayTeamFranchiseSeasonId" = fsHome."Id"
                       THEN prev."HomeTeamFranchiseSeasonId"
                       ELSE prev."AwayTeamFranchiseSeasonId" END
  LEFT JOIN public."SeasonPhase" prev_sp ON prev_sp."Id" = prev."SeasonPhaseId"
  WHERE (prev."AwayTeamFranchiseSeasonId" = fsHome."Id" OR prev."HomeTeamFranchiseSeasonId" = fsHome."Id")
    AND prev."StartDateUtc" < c."StartDateUtc"
    AND prev."FinalizedUtc" IS NOT NULL
    AND (prev_sp."TypeCode" IS NULL OR prev_sp."TypeCode" <> 1)
) home ON TRUE
WHERE c."Id" = ANY(@ContestIds);
