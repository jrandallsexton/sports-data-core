-- AS-OF slate extraction for backtests: the week-:week slate of season
-- :season_year with features computed ENTERING that week — aggregates of
-- CompetitionMetric rows from completed games in weeks < :week only.
-- Formula parity with ComputeFranchiseSeasonMetric (unweighted AVG per
-- game; RZ SafeAvg fields COALESCE to 0 when all-null, matching the C#
-- DefaultIfEmpty().Average() quirk). Pts/Margin columns aggregate from
-- Contest scores under the same cutoff.
--
-- :prior_tail (0 = off): top-up semantics — when a team has played fewer
-- than :prior_tail current-season games before the cutoff, its window is
-- supplemented with its most recent prior-season games (regular/post
-- season only; preseason is system-testing data, never signal) up to a
-- floor of :prior_tail total games where available.
--
-- :fbs_scope (0/1): 1 restricts the slate to games with at least one
-- FBS participant (split_part(GroupSeasonMap,'|',3) = 'fbs' — the v1.1
-- canonical segment predicate; NCAAFB product scope). 0 = every game
-- (NFL). Aggregation windows are NOT filtered — an FBS team's stats
-- include its games against anyone.
--
-- psql -v season_year=2025 -v week=6 -v prior_tail=5 -v fbs_scope=1
WITH params AS (
    SELECT :season_year::int AS season_year,
           :week::int        AS week,
           :prior_tail::int  AS prior_tail
),

-- One row per (team-perspective, completed game) for the CURRENT season
-- before the cutoff week. Carries the game's CompetitionMetric and the
-- team-relative scores.
current_team_games AS (
    SELECT
        fs."Id"            AS franchise_season_id,
        fs."FranchiseId"   AS franchise_id,
        con."StartDateUtc" AS start_date_utc,
        cm.*,
        CASE WHEN con."HomeTeamFranchiseSeasonId" = fs."Id" THEN con."HomeScore" ELSE con."AwayScore" END AS own_score,
        CASE WHEN con."HomeTeamFranchiseSeasonId" = fs."Id" THEN con."AwayScore" ELSE con."HomeScore" END AS opp_score
    FROM params p
    JOIN public."Season" s          ON s."Year" = p.season_year
    JOIN public."SeasonWeek" sw     ON sw."SeasonId" = s."Id" AND sw."Number" < p.week
    JOIN public."Contest" con       ON con."SeasonWeekId" = sw."Id"
    JOIN public."Competition" comp  ON comp."ContestId" = con."Id"
    LEFT JOIN public."SeasonPhase" sp ON sp."Id" = con."SeasonPhaseId"
    JOIN public."FranchiseSeason" fs
        ON fs."Id" IN (con."HomeTeamFranchiseSeasonId", con."AwayTeamFranchiseSeasonId")
    JOIN public."CompetitionMetric" cm
        ON cm."CompetitionId" = comp."Id" AND cm."FranchiseSeasonId" = fs."Id"
    WHERE con."HomeScore" IS NOT NULL AND con."AwayScore" IS NOT NULL
      AND con."CancelledUtc" IS NULL
      AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1)  -- preseason excluded (policy 2026-08-08)
),

current_counts AS (
    SELECT franchise_season_id, COUNT(*) AS games_played
    FROM current_team_games
    GROUP BY franchise_season_id
),

-- Prior-season (season_year - 1) team-games for the SAME franchises,
-- keyed to the CURRENT season's FranchiseSeasonId, newest first.
prior_team_games_ranked AS (
    SELECT
        fs_now."Id"        AS franchise_season_id,
        fs_prev."FranchiseId" AS franchise_id,
        con."StartDateUtc" AS start_date_utc,
        cm.*,
        CASE WHEN con."HomeTeamFranchiseSeasonId" = fs_prev."Id" THEN con."HomeScore" ELSE con."AwayScore" END AS own_score,
        CASE WHEN con."HomeTeamFranchiseSeasonId" = fs_prev."Id" THEN con."AwayScore" ELSE con."HomeScore" END AS opp_score,
        ROW_NUMBER() OVER (PARTITION BY fs_now."Id" ORDER BY con."StartDateUtc" DESC) AS recency_rank
    FROM params p
    JOIN public."FranchiseSeason" fs_now  ON fs_now."SeasonYear" = p.season_year
    JOIN public."FranchiseSeason" fs_prev ON fs_prev."FranchiseId" = fs_now."FranchiseId"
                                         AND fs_prev."SeasonYear" = p.season_year - 1
    JOIN public."Contest" con
        ON fs_prev."Id" IN (con."HomeTeamFranchiseSeasonId", con."AwayTeamFranchiseSeasonId")
    JOIN public."Competition" comp  ON comp."ContestId" = con."Id"
    LEFT JOIN public."SeasonPhase" sp ON sp."Id" = con."SeasonPhaseId"
    JOIN public."CompetitionMetric" cm
        ON cm."CompetitionId" = comp."Id" AND cm."FranchiseSeasonId" = fs_prev."Id"
    WHERE p.prior_tail > 0
      AND con."HomeScore" IS NOT NULL AND con."AwayScore" IS NOT NULL
      AND con."CancelledUtc" IS NULL
      AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1)
),

-- Top-up: prior games admitted only up to (prior_tail - current games).
window_games AS (
    SELECT franchise_season_id, start_date_utc, own_score, opp_score,
           "Ypp", "SuccessRate", "ExplosiveRate", "PointsPerDrive", "ThirdFourthRate",
           "RzTdRate", "RzScoreRate", "TimePossRatio",
           "OppYpp", "OppSuccessRate", "OppExplosiveRate", "OppPointsPerDrive",
           "OppThirdFourthRate", "OppRzTdRate", "OppScoreTdRate",
           "NetPunt", "FgPctShrunk", "FieldPosDiff", "TurnoverMarginPerDrive", "PenaltyYardsPerPlay"
    FROM current_team_games

    UNION ALL

    SELECT ptg.franchise_season_id, ptg.start_date_utc, ptg.own_score, ptg.opp_score,
           ptg."Ypp", ptg."SuccessRate", ptg."ExplosiveRate", ptg."PointsPerDrive", ptg."ThirdFourthRate",
           ptg."RzTdRate", ptg."RzScoreRate", ptg."TimePossRatio",
           ptg."OppYpp", ptg."OppSuccessRate", ptg."OppExplosiveRate", ptg."OppPointsPerDrive",
           ptg."OppThirdFourthRate", ptg."OppRzTdRate", ptg."OppScoreTdRate",
           ptg."NetPunt", ptg."FgPctShrunk", ptg."FieldPosDiff", ptg."TurnoverMarginPerDrive", ptg."PenaltyYardsPerPlay"
    FROM prior_team_games_ranked ptg
    CROSS JOIN params p
    LEFT JOIN current_counts cc ON cc.franchise_season_id = ptg.franchise_season_id
    WHERE ptg.recency_rank <= GREATEST(p.prior_tail - COALESCE(cc.games_played, 0), 0)
),

-- Entering-week aggregates, formula-parity with ComputeFranchiseSeasonMetric.
asof AS (
    SELECT
        franchise_season_id,
        AVG("Ypp")                     AS "Ypp",
        AVG("SuccessRate")             AS "SuccessRate",
        AVG("ExplosiveRate")           AS "ExplosiveRate",
        AVG("PointsPerDrive")          AS "PointsPerDrive",
        AVG("ThirdFourthRate")         AS "ThirdFourthRate",
        COALESCE(AVG("RzTdRate"), 0)   AS "RzTdRate",       -- SafeAvg quirk
        COALESCE(AVG("RzScoreRate"), 0) AS "RzScoreRate",   -- SafeAvg quirk
        AVG("TimePossRatio")           AS "TimePossRatio",
        AVG("OppYpp")                  AS "OppYpp",
        AVG("OppSuccessRate")          AS "OppSuccessRate",
        AVG("OppExplosiveRate")        AS "OppExplosiveRate",
        AVG("OppPointsPerDrive")       AS "OppPointsPerDrive",
        AVG("OppThirdFourthRate")      AS "OppThirdFourthRate",
        COALESCE(AVG("OppRzTdRate"), 0)    AS "OppRzTdRate",    -- SafeAvg quirk
        COALESCE(AVG("OppScoreTdRate"), 0) AS "OppScoreTdRate", -- SafeAvg quirk
        AVG("NetPunt")                 AS "NetPunt",
        AVG("FgPctShrunk")             AS "FgPctShrunk",
        AVG("FieldPosDiff")            AS "FieldPosDiff",
        AVG("TurnoverMarginPerDrive")  AS "TurnoverMarginPerDrive",
        AVG("PenaltyYardsPerPlay")     AS "PenaltyYardsPerPlay",
        AVG(own_score)                 AS "PtsScoredAvg",
        MIN(own_score)                 AS "PtsScoredMin",
        MAX(own_score)                 AS "PtsScoredMax",
        AVG(opp_score)                 AS "PtsAllowedAvg",
        MIN(opp_score)                 AS "PtsAllowedMin",
        MAX(opp_score)                 AS "PtsAllowedMax",
        AVG(CASE WHEN own_score > opp_score THEN own_score - opp_score END) AS "MarginWinAvg",
        MIN(CASE WHEN own_score > opp_score THEN own_score - opp_score END) AS "MarginWinMin",
        MAX(CASE WHEN own_score > opp_score THEN own_score - opp_score END) AS "MarginWinMax",
        AVG(CASE WHEN own_score < opp_score THEN opp_score - own_score END) AS "MarginLossAvg",
        MIN(CASE WHEN own_score < opp_score THEN opp_score - own_score END) AS "MarginLossMin",
        MAX(CASE WHEN own_score < opp_score THEN opp_score - own_score END) AS "MarginLossMax"
    FROM window_games
    GROUP BY franchise_season_id
)

SELECT
    con."Id" AS "ContestId",
    comp."Id" AS "CompetitionId",
    sw."Number" AS "WeekNumber",
    con."HomeTeamFranchiseSeasonId",
    con."AwayTeamFranchiseSeasonId",

    con."HomeTeamFranchiseSeasonId" AS "HomeFranchiseSeasonId",
    h."Ypp" AS "HomeYpp", h."SuccessRate" AS "HomeSuccessRate", h."ExplosiveRate" AS "HomeExplosiveRate",
    h."PointsPerDrive" AS "HomePointsPerDrive", h."ThirdFourthRate" AS "HomeThirdFourthRate",
    h."RzTdRate" AS "HomeRzTdRate", h."RzScoreRate" AS "HomeRzScoreRate", h."TimePossRatio" AS "HomeTimePossRatio",
    h."OppYpp" AS "HomeOppYpp", h."OppSuccessRate" AS "HomeOppSuccessRate", h."OppExplosiveRate" AS "HomeOppExplosiveRate",
    h."OppPointsPerDrive" AS "HomeOppPointsPerDrive", h."OppThirdFourthRate" AS "HomeOppThirdFourthRate",
    h."OppRzTdRate" AS "HomeOppRzTdRate", h."OppScoreTdRate" AS "HomeOppScoreTdRate",
    h."NetPunt" AS "HomeNetPunt", h."FgPctShrunk" AS "HomeFgPctShrunk", h."FieldPosDiff" AS "HomeFieldPosDiff",
    h."TurnoverMarginPerDrive" AS "HomeTurnoverMarginPerDrive", h."PenaltyYardsPerPlay" AS "HomePenaltyYardsPerPlay",
    h."PtsScoredAvg" AS "HomePtsScoredAvg", h."PtsScoredMin" AS "HomePtsScoredMin", h."PtsScoredMax" AS "HomePtsScoredMax",
    h."PtsAllowedAvg" AS "HomePtsAllowedAvg", h."PtsAllowedMin" AS "HomePtsAllowedMin", h."PtsAllowedMax" AS "HomePtsAllowedMax",
    h."MarginWinAvg" AS "HomeMarginWinAvg", h."MarginWinMin" AS "HomeMarginWinMin", h."MarginWinMax" AS "HomeMarginWinMax",
    h."MarginLossAvg" AS "HomeMarginLossAvg", h."MarginLossMin" AS "HomeMarginLossMin", h."MarginLossMax" AS "HomeMarginLossMax",

    con."AwayTeamFranchiseSeasonId" AS "AwayFranchiseSeasonId",
    a."Ypp" AS "AwayYpp", a."SuccessRate" AS "AwaySuccessRate", a."ExplosiveRate" AS "AwayExplosiveRate",
    a."PointsPerDrive" AS "AwayPointsPerDrive", a."ThirdFourthRate" AS "AwayThirdFourthRate",
    a."RzTdRate" AS "AwayRzTdRate", a."RzScoreRate" AS "AwayRzScoreRate", a."TimePossRatio" AS "AwayTimePossRatio",
    a."OppYpp" AS "AwayOppYpp", a."OppSuccessRate" AS "AwayOppSuccessRate", a."OppExplosiveRate" AS "AwayOppExplosiveRate",
    a."OppPointsPerDrive" AS "AwayOppPointsPerDrive", a."OppThirdFourthRate" AS "AwayOppThirdFourthRate",
    a."OppRzTdRate" AS "AwayOppRzTdRate", a."OppScoreTdRate" AS "AwayOppScoreTdRate",
    a."NetPunt" AS "AwayNetPunt", a."FgPctShrunk" AS "AwayFgPctShrunk", a."FieldPosDiff" AS "AwayFieldPosDiff",
    a."TurnoverMarginPerDrive" AS "AwayTurnoverMarginPerDrive", a."PenaltyYardsPerPlay" AS "AwayPenaltyYardsPerPlay",
    a."PtsScoredAvg" AS "AwayPtsScoredAvg", a."PtsScoredMin" AS "AwayPtsScoredMin", a."PtsScoredMax" AS "AwayPtsScoredMax",
    a."PtsAllowedAvg" AS "AwayPtsAllowedAvg", a."PtsAllowedMin" AS "AwayPtsAllowedMin", a."PtsAllowedMax" AS "AwayPtsAllowedMax",
    a."MarginWinAvg" AS "AwayMarginWinAvg", a."MarginWinMin" AS "AwayMarginWinMin", a."MarginWinMax" AS "AwayMarginWinMax",
    a."MarginLossAvg" AS "AwayMarginLossAvg", a."MarginLossMin" AS "AwayMarginLossMin", a."MarginLossMax" AS "AwayMarginLossMax",

    -- Scores stay NULL: the harness scores against Contest ground truth
    -- AFTER prediction; the pipeline must never see them here.
    NULL AS "HomeScore",
    NULL AS "AwayScore",
    NULL AS "Winner",

    odds."Spread"

FROM params p
JOIN public."Season" s         ON s."Year" = p.season_year
JOIN public."SeasonWeek" sw    ON sw."SeasonId" = s."Id" AND sw."Number" = p.week
JOIN public."Contest" con      ON con."SeasonWeekId" = sw."Id"
JOIN public."Competition" comp ON comp."ContestId" = con."Id"
LEFT JOIN public."SeasonPhase" sp ON sp."Id" = con."SeasonPhaseId"
JOIN public."FranchiseSeason" fs_h ON fs_h."Id" = con."HomeTeamFranchiseSeasonId"
JOIN public."FranchiseSeason" fs_a ON fs_a."Id" = con."AwayTeamFranchiseSeasonId"
JOIN asof h ON h.franchise_season_id = con."HomeTeamFranchiseSeasonId"
JOIN asof a ON a.franchise_season_id = con."AwayTeamFranchiseSeasonId"
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
  AND (:fbs_scope::int = 0
       OR split_part(fs_h."GroupSeasonMap", '|', 3) = 'fbs'
       OR split_part(fs_a."GroupSeasonMap", '|', 3) = 'fbs')
ORDER BY con."StartDateUtc";
