-- AS-OF training extraction (Option B, full as-of): completed games
-- STRICTLY BEFORE (:season_year, :week) — prior seasons fully, the
-- target season only weeks < :week, later seasons never.
--
-- Feature semantics preserved from the prototype: each game's OWN
-- per-game CompetitionMetric rows (immutable, inherently point-in-time).
-- The 12 Pts/Margin columns — which the live flow leaks from the
-- CURRENT FranchiseSeason row — are computed here as ENTERING-GAME
-- windows (that team's completed games earlier in the SAME season, by
-- kickoff time). Week-1 training rows have NULL windows (the pipeline
-- fillna(0)s, as the prototype always did for missing values).
--
-- Preseason excluded everywhere (policy 2026-08-08) — note the live
-- training SQL has no phase filter, so NFL preseason games with metrics
-- leak into live training; flagged in the design doc.
--
-- psql -v season_year=2025 -v week=6
WITH params AS (
    SELECT :season_year::int AS season_year,
           :week::int        AS week
),

-- One row per (team-perspective, completed non-preseason game) across
-- all seasons up to the cutoff, with entering-game score windows.
team_games AS (
    SELECT
        con."Id"  AS contest_id,
        fs."Id"   AS franchise_season_id,
        con."StartDateUtc" AS start_date_utc,
        CASE WHEN con."HomeTeamFranchiseSeasonId" = fs."Id" THEN con."HomeScore" ELSE con."AwayScore" END AS own_score,
        CASE WHEN con."HomeTeamFranchiseSeasonId" = fs."Id" THEN con."AwayScore" ELSE con."HomeScore" END AS opp_score
    FROM params p
    JOIN public."Contest" con      ON con."HomeScore" IS NOT NULL AND con."AwayScore" IS NOT NULL
    JOIN public."SeasonWeek" sw    ON sw."Id" = con."SeasonWeekId"
    JOIN public."Season" s         ON s."Id" = sw."SeasonId"
    LEFT JOIN public."SeasonPhase" sp ON sp."Id" = con."SeasonPhaseId"
    JOIN public."FranchiseSeason" fs
        ON fs."Id" IN (con."HomeTeamFranchiseSeasonId", con."AwayTeamFranchiseSeasonId")
    WHERE con."CancelledUtc" IS NULL
      AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1)
      AND (s."Year" < p.season_year OR (s."Year" = p.season_year AND sw."Number" < p.week))
),

entering AS (
    SELECT
        contest_id,
        franchise_season_id,
        AVG(own_score) OVER w AS pts_scored_avg,
        MIN(own_score) OVER w AS pts_scored_min,
        MAX(own_score) OVER w AS pts_scored_max,
        AVG(opp_score) OVER w AS pts_allowed_avg,
        MIN(opp_score) OVER w AS pts_allowed_min,
        MAX(opp_score) OVER w AS pts_allowed_max,
        AVG(CASE WHEN own_score > opp_score THEN own_score - opp_score END) OVER w AS margin_win_avg,
        MIN(CASE WHEN own_score > opp_score THEN own_score - opp_score END) OVER w AS margin_win_min,
        MAX(CASE WHEN own_score > opp_score THEN own_score - opp_score END) OVER w AS margin_win_max,
        AVG(CASE WHEN own_score < opp_score THEN opp_score - own_score END) OVER w AS margin_loss_avg,
        MIN(CASE WHEN own_score < opp_score THEN opp_score - own_score END) OVER w AS margin_loss_min,
        MAX(CASE WHEN own_score < opp_score THEN opp_score - own_score END) OVER w AS margin_loss_max
    FROM team_games
    WINDOW w AS (
        PARTITION BY franchise_season_id
        ORDER BY start_date_utc
        ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
    )
)

SELECT
    con."Id" AS "ContestId",
    comp."Id" AS "CompetitionId",
    sw."Number" AS "WeekNumber",
    con."HomeTeamFranchiseSeasonId",
    con."AwayTeamFranchiseSeasonId",

    cm_home."FranchiseSeasonId" AS "HomeFranchiseSeasonId",
    cm_home."Ypp" AS "HomeYpp", cm_home."SuccessRate" AS "HomeSuccessRate",
    cm_home."ExplosiveRate" AS "HomeExplosiveRate", cm_home."PointsPerDrive" AS "HomePointsPerDrive",
    cm_home."ThirdFourthRate" AS "HomeThirdFourthRate", cm_home."RzTdRate" AS "HomeRzTdRate",
    cm_home."RzScoreRate" AS "HomeRzScoreRate", cm_home."TimePossRatio" AS "HomeTimePossRatio",
    cm_home."OppYpp" AS "HomeOppYpp", cm_home."OppSuccessRate" AS "HomeOppSuccessRate",
    cm_home."OppExplosiveRate" AS "HomeOppExplosiveRate", cm_home."OppPointsPerDrive" AS "HomeOppPointsPerDrive",
    cm_home."OppThirdFourthRate" AS "HomeOppThirdFourthRate", cm_home."OppRzTdRate" AS "HomeOppRzTdRate",
    cm_home."OppScoreTdRate" AS "HomeOppScoreTdRate", cm_home."NetPunt" AS "HomeNetPunt",
    cm_home."FgPctShrunk" AS "HomeFgPctShrunk", cm_home."FieldPosDiff" AS "HomeFieldPosDiff",
    cm_home."TurnoverMarginPerDrive" AS "HomeTurnoverMarginPerDrive",
    cm_home."PenaltyYardsPerPlay" AS "HomePenaltyYardsPerPlay",

    eh.pts_scored_avg  AS "HomePtsScoredAvg",  eh.pts_scored_min  AS "HomePtsScoredMin",  eh.pts_scored_max  AS "HomePtsScoredMax",
    eh.pts_allowed_avg AS "HomePtsAllowedAvg", eh.pts_allowed_min AS "HomePtsAllowedMin", eh.pts_allowed_max AS "HomePtsAllowedMax",
    eh.margin_win_avg  AS "HomeMarginWinAvg",  eh.margin_win_min  AS "HomeMarginWinMin",  eh.margin_win_max  AS "HomeMarginWinMax",
    eh.margin_loss_avg AS "HomeMarginLossAvg", eh.margin_loss_min AS "HomeMarginLossMin", eh.margin_loss_max AS "HomeMarginLossMax",

    cm_away."FranchiseSeasonId" AS "AwayFranchiseSeasonId",
    cm_away."Ypp" AS "AwayYpp", cm_away."SuccessRate" AS "AwaySuccessRate",
    cm_away."ExplosiveRate" AS "AwayExplosiveRate", cm_away."PointsPerDrive" AS "AwayPointsPerDrive",
    cm_away."ThirdFourthRate" AS "AwayThirdFourthRate", cm_away."RzTdRate" AS "AwayRzTdRate",
    cm_away."RzScoreRate" AS "AwayRzScoreRate", cm_away."TimePossRatio" AS "AwayTimePossRatio",
    cm_away."OppYpp" AS "AwayOppYpp", cm_away."OppSuccessRate" AS "AwayOppSuccessRate",
    cm_away."OppExplosiveRate" AS "AwayOppExplosiveRate", cm_away."OppPointsPerDrive" AS "AwayOppPointsPerDrive",
    cm_away."OppThirdFourthRate" AS "AwayOppThirdFourthRate", cm_away."OppRzTdRate" AS "AwayOppRzTdRate",
    cm_away."OppScoreTdRate" AS "AwayOppScoreTdRate", cm_away."NetPunt" AS "AwayNetPunt",
    cm_away."FgPctShrunk" AS "AwayFgPctShrunk", cm_away."FieldPosDiff" AS "AwayFieldPosDiff",
    cm_away."TurnoverMarginPerDrive" AS "AwayTurnoverMarginPerDrive",
    cm_away."PenaltyYardsPerPlay" AS "AwayPenaltyYardsPerPlay",

    ea.pts_scored_avg  AS "AwayPtsScoredAvg",  ea.pts_scored_min  AS "AwayPtsScoredMin",  ea.pts_scored_max  AS "AwayPtsScoredMax",
    ea.pts_allowed_avg AS "AwayPtsAllowedAvg", ea.pts_allowed_min AS "AwayPtsAllowedMin", ea.pts_allowed_max AS "AwayPtsAllowedMax",
    ea.margin_win_avg  AS "AwayMarginWinAvg",  ea.margin_win_min  AS "AwayMarginWinMin",  ea.margin_win_max  AS "AwayMarginWinMax",
    ea.margin_loss_avg AS "AwayMarginLossAvg", ea.margin_loss_min AS "AwayMarginLossMin", ea.margin_loss_max AS "AwayMarginLossMax",

    con."HomeScore",
    con."AwayScore",
    CASE
        WHEN con."HomeScore" > con."AwayScore" THEN 'HOME'
        WHEN con."AwayScore" > con."HomeScore" THEN 'AWAY'
        ELSE 'TIE'
    END AS "Winner",
    odds."Spread"

FROM params p
JOIN public."Contest" con      ON con."HomeScore" IS NOT NULL AND con."AwayScore" IS NOT NULL
JOIN public."SeasonWeek" sw    ON sw."Id" = con."SeasonWeekId"
JOIN public."Season" s         ON s."Id" = sw."SeasonId"
LEFT JOIN public."SeasonPhase" sp ON sp."Id" = con."SeasonPhaseId"
JOIN public."Competition" comp ON comp."ContestId" = con."Id"
JOIN public."CompetitionMetric" cm_home
    ON cm_home."CompetitionId" = comp."Id" AND cm_home."FranchiseSeasonId" = con."HomeTeamFranchiseSeasonId"
JOIN public."CompetitionMetric" cm_away
    ON cm_away."CompetitionId" = comp."Id" AND cm_away."FranchiseSeasonId" = con."AwayTeamFranchiseSeasonId"
JOIN entering eh ON eh.contest_id = con."Id" AND eh.franchise_season_id = con."HomeTeamFranchiseSeasonId"
JOIN entering ea ON ea.contest_id = con."Id" AND ea.franchise_season_id = con."AwayTeamFranchiseSeasonId"
LEFT JOIN LATERAL (
  SELECT *
  FROM public."CompetitionOdds"
  WHERE "CompetitionId" = comp."Id"
    AND "ProviderId" IN ('58', '100')
  ORDER BY CASE WHEN "ProviderId" = '58' THEN 1 ELSE 2 END
  LIMIT 1
) odds ON TRUE
WHERE con."CancelledUtc" IS NULL
  AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1)
  AND (s."Year" < p.season_year OR (s."Year" = p.season_year AND sw."Number" < p.week))
ORDER BY s."Year", sw."Number";
