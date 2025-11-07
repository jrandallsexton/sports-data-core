WITH next_week AS (
  SELECT sw."Id" AS "SeasonWeekId",
         sw."Number" AS "WeekNumber",
         s."Id" AS "SeasonId",
         s."Year" AS "SeasonYear"
  FROM public."Season" s
  JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id"
  JOIN public."SeasonPhase" sp ON sp."Id" = sw."SeasonPhaseId"
  WHERE sp."Name" = 'Regular Season'
    AND sw."StartDate" <= NOW()
    AND sw."EndDate" > NOW()
  ORDER BY sw."StartDate"
  LIMIT 1
)

SELECT
  con."Id" AS "ContestId",
  comp."Id" AS "CompetitionId",
  nw."WeekNumber",

  con."HomeTeamFranchiseSeasonId",
  con."AwayTeamFranchiseSeasonId",

  fsm_home."FranchiseSeasonId" AS "HomeFranchiseSeasonId",
  fsm_home."Ypp" AS "HomeYpp",
  fsm_home."SuccessRate" AS "HomeSuccessRate",
  fsm_home."ExplosiveRate" AS "HomeExplosiveRate",
  fsm_home."PointsPerDrive" AS "HomePointsPerDrive",
  fsm_home."ThirdFourthRate" AS "HomeThirdFourthRate",
  fsm_home."RzTdRate" AS "HomeRzTdRate",
  fsm_home."RzScoreRate" AS "HomeRzScoreRate",
  fsm_home."TimePossRatio" AS "HomeTimePossRatio",
  fsm_home."OppYpp" AS "HomeOppYpp",
  fsm_home."OppSuccessRate" AS "HomeOppSuccessRate",
  fsm_home."OppExplosiveRate" AS "HomeOppExplosiveRate",
  fsm_home."OppPointsPerDrive" AS "HomeOppPointsPerDrive",
  fsm_home."OppThirdFourthRate" AS "HomeOppThirdFourthRate",
  fsm_home."OppRzTdRate" AS "HomeOppRzTdRate",
  fsm_home."OppScoreTdRate" AS "HomeOppScoreTdRate",
  fsm_home."NetPunt" AS "HomeNetPunt",
  fsm_home."FgPctShrunk" AS "HomeFgPctShrunk",
  fsm_home."FieldPosDiff" AS "HomeFieldPosDiff",
  fsm_home."TurnoverMarginPerDrive" AS "HomeTurnoverMarginPerDrive",
  fsm_home."PenaltyYardsPerPlay" AS "HomePenaltyYardsPerPlay",

  fsm_away."FranchiseSeasonId" AS "AwayFranchiseSeasonId",
  fsm_away."Ypp" AS "AwayYpp",
  fsm_away."SuccessRate" AS "AwaySuccessRate",
  fsm_away."ExplosiveRate" AS "AwayExplosiveRate",
  fsm_away."PointsPerDrive" AS "AwayPointsPerDrive",
  fsm_away."ThirdFourthRate" AS "AwayThirdFourthRate",
  fsm_away."RzTdRate" AS "AwayRzTdRate",
  fsm_away."RzScoreRate" AS "AwayRzScoreRate",
  fsm_away."TimePossRatio" AS "AwayTimePossRatio",
  fsm_away."OppYpp" AS "AwayOppYpp",
  fsm_away."OppSuccessRate" AS "AwayOppSuccessRate",
  fsm_away."OppExplosiveRate" AS "AwayOppExplosiveRate",
  fsm_away."OppPointsPerDrive" AS "AwayOppPointsPerDrive",
  fsm_away."OppThirdFourthRate" AS "AwayOppThirdFourthRate",
  fsm_away."OppRzTdRate" AS "AwayOppRzTdRate",
  fsm_away."OppScoreTdRate" AS "AwayOppScoreTdRate",
  fsm_away."NetPunt" AS "AwayNetPunt",
  fsm_away."FgPctShrunk" AS "AwayFgPctShrunk",
  fsm_away."FieldPosDiff" AS "AwayFieldPosDiff",
  fsm_away."TurnoverMarginPerDrive" AS "AwayTurnoverMarginPerDrive",
  fsm_away."PenaltyYardsPerPlay" AS "AwayPenaltyYardsPerPlay",

  NULL AS "HomeScore",
  NULL AS "AwayScore",
  NULL AS "Winner",

  odds."Spread"

FROM next_week nw
JOIN public."Contest" con ON con."SeasonWeekId" = nw."SeasonWeekId"
JOIN public."Competition" comp ON comp."ContestId" = con."Id"

JOIN public."FranchiseSeasonMetric" fsm_home
  ON fsm_home."FranchiseSeasonId" = con."HomeTeamFranchiseSeasonId"

JOIN public."FranchiseSeasonMetric" fsm_away
  ON fsm_away."FranchiseSeasonId" = con."AwayTeamFranchiseSeasonId"

LEFT JOIN public."CompetitionOdds" odds
  ON odds."CompetitionId" = comp."Id" AND odds."ProviderId" = '58'

WHERE con."StartDateUtc" >= NOW()
ORDER BY con."StartDateUtc";
