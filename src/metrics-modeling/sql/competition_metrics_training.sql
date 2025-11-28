SELECT
    con."Id" AS "ContestId",
    comp."Id" AS "CompetitionId",
    sw."Number" AS "WeekNumber",

    con."HomeTeamFranchiseSeasonId",
    con."AwayTeamFranchiseSeasonId",

    cm_home."FranchiseSeasonId" AS "HomeFranchiseSeasonId",
    cm_home."Ypp" AS "HomeYpp",
    cm_home."SuccessRate" AS "HomeSuccessRate",
    cm_home."ExplosiveRate" AS "HomeExplosiveRate",
    cm_home."PointsPerDrive" AS "HomePointsPerDrive",
    cm_home."ThirdFourthRate" AS "HomeThirdFourthRate",
    cm_home."RzTdRate" AS "HomeRzTdRate",
    cm_home."RzScoreRate" AS "HomeRzScoreRate",
    cm_home."TimePossRatio" AS "HomeTimePossRatio",
    cm_home."OppYpp" AS "HomeOppYpp",
    cm_home."OppSuccessRate" AS "HomeOppSuccessRate",
    cm_home."OppExplosiveRate" AS "HomeOppExplosiveRate",
    cm_home."OppPointsPerDrive" AS "HomeOppPointsPerDrive",
    cm_home."OppThirdFourthRate" AS "HomeOppThirdFourthRate",
    cm_home."OppRzTdRate" AS "HomeOppRzTdRate",
    cm_home."OppScoreTdRate" AS "HomeOppScoreTdRate",
    cm_home."NetPunt" AS "HomeNetPunt",
    cm_home."FgPctShrunk" AS "HomeFgPctShrunk",
    cm_home."FieldPosDiff" AS "HomeFieldPosDiff",
    cm_home."TurnoverMarginPerDrive" AS "HomeTurnoverMarginPerDrive",
    cm_home."PenaltyYardsPerPlay" AS "HomePenaltyYardsPerPlay",

    -- Home FranchiseSeason metrics (scoring/margin)
    fs_home."PtsScoredAvg" AS "HomePtsScoredAvg",
    fs_home."PtsScoredMin" AS "HomePtsScoredMin",
    fs_home."PtsScoredMax" AS "HomePtsScoredMax",
    fs_home."PtsAllowedAvg" AS "HomePtsAllowedAvg",
    fs_home."PtsAllowedMin" AS "HomePtsAllowedMin",
    fs_home."PtsAllowedMax" AS "HomePtsAllowedMax",
    fs_home."MarginWinAvg" AS "HomeMarginWinAvg",
    fs_home."MarginWinMin" AS "HomeMarginWinMin",
    fs_home."MarginWinMax" AS "HomeMarginWinMax",
    fs_home."MarginLossAvg" AS "HomeMarginLossAvg",
    fs_home."MarginLossMin" AS "HomeMarginLossMin",
    fs_home."MarginLossMax" AS "HomeMarginLossMax",

    cm_away."FranchiseSeasonId" AS "AwayFranchiseSeasonId",
    cm_away."Ypp" AS "AwayYpp",
    cm_away."SuccessRate" AS "AwaySuccessRate",
    cm_away."ExplosiveRate" AS "AwayExplosiveRate",
    cm_away."PointsPerDrive" AS "AwayPointsPerDrive",
    cm_away."ThirdFourthRate" AS "AwayThirdFourthRate",
    cm_away."RzTdRate" AS "AwayRzTdRate",
    cm_away."RzScoreRate" AS "AwayRzScoreRate",
    cm_away."TimePossRatio" AS "AwayTimePossRatio",
    cm_away."OppYpp" AS "AwayOppYpp",
    cm_away."OppSuccessRate" AS "AwayOppSuccessRate",
    cm_away."OppExplosiveRate" AS "AwayOppExplosiveRate",
    cm_away."OppPointsPerDrive" AS "AwayOppPointsPerDrive",
    cm_away."OppThirdFourthRate" AS "AwayOppThirdFourthRate",
    cm_away."OppRzTdRate" AS "AwayOppRzTdRate",
    cm_away."OppScoreTdRate" AS "AwayOppScoreTdRate",
    cm_away."NetPunt" AS "AwayNetPunt",
    cm_away."FgPctShrunk" AS "AwayFgPctShrunk",
    cm_away."FieldPosDiff" AS "AwayFieldPosDiff",
    cm_away."TurnoverMarginPerDrive" AS "AwayTurnoverMarginPerDrive",
    cm_away."PenaltyYardsPerPlay" AS "AwayPenaltyYardsPerPlay",

    -- Away FranchiseSeason metrics (scoring/margin)
    fs_away."PtsScoredAvg" AS "AwayPtsScoredAvg",
    fs_away."PtsScoredMin" AS "AwayPtsScoredMin",
    fs_away."PtsScoredMax" AS "AwayPtsScoredMax",
    fs_away."PtsAllowedAvg" AS "AwayPtsAllowedAvg",
    fs_away."PtsAllowedMin" AS "AwayPtsAllowedMin",
    fs_away."PtsAllowedMax" AS "AwayPtsAllowedMax",
    fs_away."MarginWinAvg" AS "AwayMarginWinAvg",
    fs_away."MarginWinMin" AS "AwayMarginWinMin",
    fs_away."MarginWinMax" AS "AwayMarginWinMax",
    fs_away."MarginLossAvg" AS "AwayMarginLossAvg",
    fs_away."MarginLossMin" AS "AwayMarginLossMin",
    fs_away."MarginLossMax" AS "AwayMarginLossMax",

    con."HomeScore",
    con."AwayScore",

    CASE
        WHEN con."HomeScore" > con."AwayScore" THEN 'HOME'
        WHEN con."AwayScore" > con."HomeScore" THEN 'AWAY'
        ELSE 'TIE'
    END AS "Winner",

    odds."Spread"

FROM public."Contest" con
JOIN public."Competition" comp ON comp."ContestId" = con."Id"

JOIN public."SeasonWeek" sw ON sw."Id" = con."SeasonWeekId"

-- Join both metrics
JOIN public."CompetitionMetric" cm_home ON cm_home."CompetitionId" = comp."Id"
    AND cm_home."FranchiseSeasonId" = con."HomeTeamFranchiseSeasonId"

JOIN public."CompetitionMetric" cm_away ON cm_away."CompetitionId" = comp."Id"
    AND cm_away."FranchiseSeasonId" = con."AwayTeamFranchiseSeasonId"

JOIN public."FranchiseSeason" fs_home
    ON fs_home."Id" = con."HomeTeamFranchiseSeasonId"

JOIN public."FranchiseSeason" fs_away
    ON fs_away."Id" = con."AwayTeamFranchiseSeasonId"

-- Join odds from provider 58
LEFT JOIN public."CompetitionOdds" odds
    ON odds."CompetitionId" = comp."Id" AND odds."ProviderId" = '58'

-- Only completed games
WHERE con."HomeScore" IS NOT NULL AND con."AwayScore" IS NOT NULL
ORDER BY sw."Number";
